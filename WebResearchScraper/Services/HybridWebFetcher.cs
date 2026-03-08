using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebResearchScraper.Interfaces;
using WebResearchScraper.Models;

namespace WebResearchScraper.Services;

internal sealed class HybridWebFetcher(
    StaticWebFetcher staticFetcher,
    PlaywrightWebFetcher playwrightFetcher,
    IOptions<ScraperOptions> options,
    ILogger<HybridWebFetcher> logger) : IWebFetcher
{
    // Selectors that indicate a SPA shell with no server-rendered content
    private static readonly string[] SpaMarkers = ["#__next", "#root", "#app", "[data-reactroot]"];

    public async Task<string?> FetchHtmlAsync(string url, CancellationToken ct = default)
    {
        var staticHtml = await staticFetcher.FetchHtmlAsync(url, ct);

        if (ShouldEscalateToPlaywright(staticHtml))
        {
            logger.LogInformation("Escalating {Url} to Playwright (thin static content or SPA detected).", url);
            var pwHtml = await playwrightFetcher.FetchHtmlAsync(url, ct);
            // Fall back to static if Playwright also failed
            return pwHtml ?? staticHtml;
        }

        return staticHtml;
    }

    private bool ShouldEscalateToPlaywright(string? html)
    {
        if (html is null) return true;

        var minLen = options.Value.MinContentLengthChars;

        // Only escalate if document is small-ish (large pages are usually server-rendered)
        if (html.Length >= 50_000) return false;

        // Check visible text length via a quick parse
        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);
        var visibleText = doc.Body?.TextContent ?? string.Empty;
        var trimmedTextLength = visibleText.Trim().Length;

        if (trimmedTextLength < minLen) return true;

        // Check for empty SPA root containers
        foreach (var marker in SpaMarkers)
        {
            var element = doc.QuerySelector(marker);
            if (element is not null && string.IsNullOrWhiteSpace(element.TextContent))
                return true;
        }

        return false;
    }
}
