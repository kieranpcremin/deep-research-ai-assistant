namespace WebResearchScraper.Interfaces;

public interface IWebFetcher
{
    Task<string?> FetchHtmlAsync(string url, CancellationToken ct = default);
}
