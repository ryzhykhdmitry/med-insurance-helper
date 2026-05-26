using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Models;
using Microsoft.Extensions.Options;

namespace MedInsuranceHelper.Api.Services;

/// <summary>
/// Routes an incoming user message to the appropriate pipeline(s) and assembles
/// a <see cref="ResponseArtifact"/> with one section per detected intent.
/// </summary>
public interface IChatOrchestrationService
{
    Task<ResponseArtifact> HandleAsync(
        string message,
        ConversationSession session,
        CancellationToken ct = default);
}

/// <inheritdoc />
public class ChatOrchestrationService : IChatOrchestrationService
{
    private readonly IIntentDetectionService _intents;
    private readonly ILLMPipelineService _pipeline;
    private readonly IComparisonService _comparison;
    private readonly IRecommendationService _recommendation;
    private readonly IOfferRepository _offers;
    private readonly ISessionService _sessions;
    private readonly AppSettings _settings;
    private readonly ILogger<ChatOrchestrationService> _logger;

    public ChatOrchestrationService(
        IIntentDetectionService intents,
        ILLMPipelineService pipeline,
        IComparisonService comparison,
        IRecommendationService recommendation,
        IOfferRepository offers,
        ISessionService sessions,
        IOptions<AppSettings> options,
        ILogger<ChatOrchestrationService> logger)
    {
        _intents = intents;
        _pipeline = pipeline;
        _comparison = comparison;
        _recommendation = recommendation;
        _offers = offers;
        _sessions = sessions;
        _settings = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ResponseArtifact> HandleAsync(
        string message,
        ConversationSession session,
        CancellationToken ct = default)
    {
        var artifact = new ResponseArtifact { SessionId = session.Id };

        // --- Persist user message ---
        _sessions.AppendMessage(session.Id, new Message
        {
            Role = MessageRole.User,
            Text = message
        });

        // --- Build conversation history for LLM context (T009 / FR-004) ---
        var history = session.Messages
            .Where(m => m.Role is MessageRole.User or MessageRole.Assistant)
            .Select(m => (m.Role.ToString().ToLowerInvariant(), m.Text))
            .ToList();

        // --- Detect intents (FR-003) ---
        var detectedIntents = _intents.Detect(message);
        _logger.LogInformation("Session {SessionId}: detected intents [{Intents}].",
            session.Id, string.Join(", ", detectedIntents.Select(i => i.Name)));

        // --- Handle each intent and build sections (FR-005a) ---
        foreach (var intent in detectedIntents)
        {
            switch (intent.Name)
            {
                case ChatIntent.Compare:
                    await HandleCompareAsync(message, artifact, history, ct);
                    break;

                case ChatIntent.Recommend:
                    await HandleRecommendAsync(message, artifact, ct);
                    break;

                default:
                    await HandleAskAsync(message, artifact, history, ct);
                    break;
            }
        }

        // --- Persist assistant response ---
        var combinedText = string.Join("\n\n", artifact.Sections.Select(s => s.Content));
        _sessions.AppendMessage(session.Id, new Message
        {
            Role = MessageRole.Assistant,
            Text = combinedText
        });

        return artifact;
    }

    // -----------------------------------------------------------------------
    // Private intent handlers
    // -----------------------------------------------------------------------

    private async Task HandleCompareAsync(
        string message,
        ResponseArtifact artifact,
        IReadOnlyList<(string, string)> history,
        CancellationToken ct)
    {
        // Resolve known offer IDs that appear in the message
        var allOffers = _offers.GetAll();
        var mentioned = allOffers
            .Where(o => message.Contains(o.Id, StringComparison.OrdinalIgnoreCase)
                     || (!string.IsNullOrWhiteSpace(o.Title)
                         && message.Contains(o.Title, StringComparison.OrdinalIgnoreCase)))
            .Select(o => o.Id)
            .Distinct()
            .ToList();

        // FR-011: insufficient-input → clarification (per configured policy)
        if (mentioned.Count < 2)
        {
            var policy = _settings.SinglePlanComparePolicy;
            if (policy.Equals("recommend", StringComparison.OrdinalIgnoreCase))
            {
                // Auto-redirect to recommendation
                await HandleRecommendAsync(message, artifact, ct);
                return;
            }

            // Default: ask for clarification (FR-005)
            artifact.Sections.Add(new ResponseSection
            {
                Type = "clarification",
                Content = "To compare plans I need at least two plan names. " +
                          "Could you specify which plans you'd like me to compare? " +
                          "Available plans: " + string.Join(", ", allOffers.Select(o => string.IsNullOrWhiteSpace(o.Title) ? o.Id : o.Title))
            });
            return;
        }

        // Default aspects for comparison
        var aspects = new List<string> { "premiums", "deductibles", "coverage", "network", "copay" };

        try
        {
            var table = await _comparison.CompareAsync(mentioned, aspects, ct);
            var content = BuildComparisonText(mentioned, aspects, table);

            artifact.Sections.Add(new ResponseSection
            {
                Type = "comparison",
                Content = content,
                Payload = table
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Comparison failed for offers [{Offers}].", string.Join(", ", mentioned));
            artifact.Errors.Add("Comparison could not be completed. Please try again.");
        }
    }

    private async Task HandleRecommendAsync(
        string message,
        ResponseArtifact artifact,
        CancellationToken ct)
    {
        try
        {
            var results = await _recommendation.RecommendAsync(message, ct);
            if (results.Count == 0)
            {
                artifact.Sections.Add(new ResponseSection
                {
                    Type = "answer",
                    Content = "No plans were found that match your criteria. " +
                              "Please ensure insurance documents have been ingested."
                });
                return;
            }

            var content = "**Recommended plans:**\n" + string.Join("\n",
                results.Select((r, i) => $"{i + 1}. **{r.OfferId}** — {r.Reason}"));

            artifact.Sections.Add(new ResponseSection
            {
                Type = "recommendation",
                Content = content,
                Payload = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recommendation failed.");
            artifact.Errors.Add("Recommendation could not be completed. Please try again.");
        }
    }

    private async Task HandleAskAsync(
        string message,
        ResponseArtifact artifact,
        IReadOnlyList<(string, string)> history,
        CancellationToken ct)
    {
        try
        {
            var result = await _pipeline.RunAsync(message, _settings.DefaultTopK, history, ct);

            // Materialise the token stream into a string
            var sb = new System.Text.StringBuilder();
            await foreach (var token in result.TokenStream.WithCancellation(ct))
                sb.Append(token);

            artifact.Sections.Add(new ResponseSection
            {
                Type = "answer",
                Content = sb.ToString(),
                Payload = result.Citations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM pipeline failed.");
            artifact.Errors.Add("Could not generate an answer. Please try again.");
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string BuildComparisonText(
        IReadOnlyList<string> offerIds,
        IReadOnlyList<string> aspects,
        Dictionary<string, Dictionary<string, string?>> table)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"**Comparison: {string.Join(" vs ", offerIds)}**");
        sb.AppendLine();
        foreach (var aspect in aspects)
        {
            if (!table.TryGetValue(aspect, out var row)) continue;
            sb.AppendLine($"**{aspect}**");
            foreach (var offerId in offerIds)
            {
                var snippet = row.TryGetValue(offerId, out var v) ? v : null;
                sb.AppendLine($"  - {offerId}: {snippet ?? "No data found"}");
            }
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
