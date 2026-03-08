using WebResearchScraper.Models;

namespace WebResearchScraper.Interfaces;

public interface IWebResearchService
{
    Task<ResearchResults> ResearchAsync(string query, ScraperOptions? options = null, CancellationToken ct = default);
}
