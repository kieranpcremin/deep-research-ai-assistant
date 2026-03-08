namespace WebResearchScraper.Models;

public sealed record ResearchResults
{
    public required string Query { get; init; }
    public required DateTimeOffset SearchedAt { get; init; }
    public required IReadOnlyList<SearchResult> Results { get; init; }
    public int FailedCount { get; init; }
}
