using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebResearchScraper.Models;

namespace WebResearchScraper.Services;

internal sealed class StaticWebFetcher(
    IHttpClientFactory httpClientFactory,
    IOptions<ScraperOptions> options,
    ILogger<StaticWebFetcher> logger)
{
    private const string ClientName = "Static";

    public async Task<string?> FetchHtmlAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Static fetch got HTTP {Status} for {Url}.", (int)response.StatusCode, url);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogDebug("Skipping non-text content type '{ContentType}' for {Url}.", contentType, url);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            logger.LogDebug("Static fetched {Bytes} chars from {Url}.", html.Length, url);
            return html;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Static fetch failed for {Url}.", url);
            return null;
        }
    }
}
