namespace DeepResearchApp.Models;

public sealed record VideoResult(
    string Title,
    string Url,
    string Channel,
    string Duration,
    string ThumbnailUrl,
    string PublishedDate);
