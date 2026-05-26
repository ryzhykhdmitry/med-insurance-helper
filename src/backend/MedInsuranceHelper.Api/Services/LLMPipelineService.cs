using MedInsuranceHelper.Api.Models;
using OpenAI.Chat;
// Note: Citation type lives in MedInsuranceHelper.Api.Models

namespace MedInsuranceHelper.Api.Services;

/// <summary>Result of the LLM pipeline: streamed tokens + citations.</summary>
public record PipelineResult(IAsyncEnumerable<string> TokenStream, IReadOnlyList<Citation> Citations, bool NoResults);

/// <summary>Orchestrates RAG: retrieve → prompt → stream.</summary>
public interface ILLMPipelineService
{
    Task<PipelineResult> RunAsync(
        string query, int topK,
        IReadOnlyList<(string Role, string Text)>? history = null,
        CancellationToken ct = default);
}

/// <summary>
/// Composes a RAG prompt from retrieved chunks and streams the Foundry response.
/// T020: returns a "no relevant information" message when chunks are empty or low-confidence.
/// T034: accepts conversation history for multi-turn context.
/// </summary>
public class LLMPipelineService : ILLMPipelineService
{
    private readonly IRetrievalService _retrieval;
    private readonly IFoundryClient _foundry;
    private readonly ILogger<LLMPipelineService> _logger;

    private const string SystemPrompt = """
        You are a helpful medical insurance advisor.
        Answer questions about insurance offers using ONLY the provided context.
        Always cite your sources by referencing the document name and page number.
        If the context does not contain enough information to answer the question,
        say so clearly — do not make up information.
        """;

    public LLMPipelineService(
        IRetrievalService retrieval,
        IFoundryClient foundry,
        ILogger<LLMPipelineService> logger)
    {
        _retrieval = retrieval;
        _foundry = foundry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PipelineResult> RunAsync(
        string query, int topK,
        IReadOnlyList<(string Role, string Text)>? history = null,
        CancellationToken ct = default)
    {
        // 1. Retrieve relevant chunks
        var chunks = await _retrieval.RetrieveAsync(query, topK, ct: ct);

        // T020: No-results guard — skip Foundry call and return a clear message
        if (chunks.Count == 0)
        {
            _logger.LogInformation("No relevant chunks found for query '{Query}'. Returning not-found message.", query);
            return new PipelineResult(
                NoResultsStream(),
                Array.Empty<Citation>(),
                NoResults: true);
        }

        // 2. Build citations list
        var citations = chunks.Select(c => new Citation
        {
            DocumentId = c.OfferId,
            ChunkId = c.ChunkId,
            PageRef = c.StartPage == c.EndPage ? $"p.{c.StartPage}" : $"pp.{c.StartPage}-{c.EndPage}",
            Excerpt = c.Text.Length > 200 ? c.Text[..200] + "…" : c.Text
        }).ToList();

        // 3. Build context block
        var contextBlock = string.Join("\n\n---\n\n",
            chunks.Select((c, i) => $"[Source {i + 1}] OfferId={c.OfferId} Pages={c.StartPage}-{c.EndPage}\n{c.Text}"));

        // 4. Assemble messages
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new SystemChatMessage($"Context:\n{contextBlock}")
        };

        // T034: inject conversation history
        if (history is { Count: > 0 })
        {
            foreach (var (role, text) in history)
            {
                messages.Add(role.Equals("user", StringComparison.OrdinalIgnoreCase)
                    ? new UserChatMessage(text)
                    : new AssistantChatMessage(text));
            }
        }

        messages.Add(new UserChatMessage(query));

        // 5. Stream response — filter finished tokens inline
        var tokenStream = FilterTokenStream(_foundry.StreamChatAsync(messages, ct));

        return new PipelineResult(tokenStream, citations, NoResults: false);
    }

    private static async IAsyncEnumerable<string> FilterTokenStream(IAsyncEnumerable<StreamToken> source)
    {
        await foreach (var t in source)
        {
            if (!t.IsFinished && !string.IsNullOrEmpty(t.Content))
                yield return t.Content;
        }
    }

    private static async IAsyncEnumerable<string> NoResultsStream()
    {
        yield return "I could not find relevant information in the insurance documents to answer your question. " +
                     "Please try rephrasing, or ensure the relevant document has been ingested and processed.";
        await Task.CompletedTask;
    }
}
