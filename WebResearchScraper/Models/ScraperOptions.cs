namespace WebResearchScraper.Models;

public sealed class ScraperOptions
{
    public const string SectionName = "WebResearchScraper";

    public string SerperApiKey { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 10;
    public int MaxConcurrency { get; set; } = 4;
    public int StaticTimeoutSeconds { get; set; } = 15;
    public int PlaywrightTimeoutMs { get; set; } = 30000;
    public int MinContentLengthChars { get; set; } = 400;

    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    public bool PlaywrightHeaded { get; set; } = false;
}
