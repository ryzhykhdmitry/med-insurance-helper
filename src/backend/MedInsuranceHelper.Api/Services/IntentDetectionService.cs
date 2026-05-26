namespace MedInsuranceHelper.Api.Services;

/// <summary>Intent types the unified chat surface can handle.</summary>
public enum ChatIntent { Compare, Recommend, Ask }

/// <summary>An intent detected in a user message, with any extracted parameters.</summary>
public class DetectedIntent
{
    public ChatIntent Name { get; set; }

    /// <summary>Plan/offer identifiers mentioned (e.g., "alpha", "beta").</summary>
    public List<string> PlanIds { get; set; } = new();

    /// <summary>Extra free-form parameters parsed from the message.</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>Detects one or more intents from a user message using keyword heuristics.</summary>
public interface IIntentDetectionService
{
    /// <summary>
    /// Analyses <paramref name="message"/> and returns all detected intents.
    /// Never throws — returns [Ask] as fallback.
    /// </summary>
    List<DetectedIntent> Detect(string message);
}

/// <inheritdoc />
public class IntentDetectionService : IIntentDetectionService
{
    // Keywords that signal a comparison intent
    private static readonly string[] CompareKeywords =
        ["compare", "comparison", "versus", " vs ", "difference", "differences", "contrast", "side by side"];

    // Keywords that signal a recommendation intent
    private static readonly string[] RecommendKeywords =
        ["recommend", "suggest", "best plan", "best for", "which plan", "which one", "ideal", "suitable", "pick", "choose"];

    /// <inheritdoc />
    public List<DetectedIntent> Detect(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return [new DetectedIntent { Name = ChatIntent.Ask }];

        var lower = message.ToLowerInvariant();
        var intents = new List<DetectedIntent>();

        if (CompareKeywords.Any(k => lower.Contains(k)))
            intents.Add(new DetectedIntent { Name = ChatIntent.Compare });

        if (RecommendKeywords.Any(k => lower.Contains(k)))
            intents.Add(new DetectedIntent { Name = ChatIntent.Recommend });

        if (intents.Count == 0)
            intents.Add(new DetectedIntent { Name = ChatIntent.Ask });

        return intents;
    }
}
