namespace MedInsuranceHelper.Api.Models;

/// <summary>A single recommendation result produced by <c>IRecommendationService</c>.</summary>
public record RecommendationItem(string OfferId, string Reason, IReadOnlyList<RecommendCitation> Citations);

/// <summary>A source citation attached to a recommendation result.</summary>
public record RecommendCitation(string OfferId, int Page, string Excerpt);
