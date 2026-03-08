namespace WebResearchScraper.Interfaces;

public interface IContentExtractor
{
    Task<(string Title, string Markdown)> ExtractAsync(string html, string url);
}
