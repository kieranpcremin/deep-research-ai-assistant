namespace WebResearchScraper.Models;

public sealed record SearchResult
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public required string MarkdownContent { get; init; }
    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool WasJsRendered { get; init; } = false;
}
