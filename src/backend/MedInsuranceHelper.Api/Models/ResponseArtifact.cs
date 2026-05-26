namespace MedInsuranceHelper.Api.Models;

/// <summary>
/// A single labelled section within a <see cref="ResponseArtifact"/>.
/// The <c>type</c> field indicates the kind of result: "comparison", "recommendation", "answer", or "clarification".
/// </summary>
public class ResponseSection
{
    /// <summary>Section type: "comparison" | "recommendation" | "answer" | "clarification".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Human-readable content for this section.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional structured payload (e.g. comparison table or ranked list).</summary>
    public object? Payload { get; set; }
}

/// <summary>
/// Structured output of the unified chat handler.
/// May contain one or more sections when multiple intents are detected.
/// </summary>
public class ResponseArtifact
{
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Ordered list of response sections — highest-priority first.</summary>
    public List<ResponseSection> Sections { get; set; } = new();

    /// <summary>User-visible error messages (empty on success).</summary>
    public List<string> Errors { get; set; } = new();
}
