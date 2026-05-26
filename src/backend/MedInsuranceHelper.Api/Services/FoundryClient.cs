using Azure.AI.OpenAI;
using MedInsuranceHelper.Api.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.Runtime.CompilerServices;

namespace MedInsuranceHelper.Api.Services;

/// <summary>A single streamed token chunk from the Foundry chat completion API.</summary>
public record StreamToken(string Content, bool IsFinished);

/// <summary>Wraps Azure AI Foundry chat completions with streaming and retry support.</summary>
public interface IFoundryClient
{
    Task<string> CompleteChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default);
    IAsyncEnumerable<StreamToken> StreamChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default);
}

/// <summary>
/// Azure AI Foundry client using Azure.AI.OpenAI SDK.
/// Supports both synchronous chat completion and streaming SSE.
/// Includes basic retry logic with exponential back-off for transient failures.
/// </summary>
public class FoundryClient : IFoundryClient
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<FoundryClient> _logger;
    private const int MaxRetries = 3;

    public FoundryClient(IOptions<AppSettings> options, ILogger<FoundryClient> logger)
    {
        _logger = logger;
        var settings = options.Value;

        var azureClient = new AzureOpenAIClient(
            new Uri(settings.FoundryEndpoint),
            new System.ClientModel.ApiKeyCredential(settings.FoundryApiKey));
        _chatClient = azureClient.GetChatClient(settings.ChatDeployment);
    }

    /// <inheritdoc/>
    public async Task<string> CompleteChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
                return result.Value.Content[0].Text;
            }
            catch (Exception ex) when (attempt < MaxRetries && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Chat completion attempt {Attempt} failed. Retrying...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        // Final attempt — let exception propagate
        var finalResult = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        return finalResult.Value.Content[0].Text;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamToken> StreamChatAsync(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        CollectionResult<StreamingChatCompletionUpdate>? stream = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                stream = _chatClient.CompleteChatStreaming(messages);
                break;
            }
            catch (Exception ex) when (attempt < MaxRetries && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Streaming attempt {Attempt} failed. Retrying...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        if (stream is null) yield break;

        foreach (var update in stream)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return new StreamToken(part.Text, false);
            }

            if (update.FinishReason.HasValue)
                yield return new StreamToken(string.Empty, true);
        }

        await Task.CompletedTask;
    }
}
