namespace WebResearchScraper.Interfaces;

public interface ISearchService
{
    Task<IEnumerable<string>> SearchAsync(string query, int maxResults, CancellationToken ct = default);
}
