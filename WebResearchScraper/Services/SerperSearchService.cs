using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebResearchScraper.Interfaces;
using WebResearchScraper.Models;

namespace WebResearchScraper.Services;

public sealed class SerperSearchService(
    IHttpClientFactory httpClientFactory,
    IOptions<ScraperOptions> options,
    ILogger<SerperSearchService> logger) : ISearchService
{
    private const string ClientName = "Serper";
    private const string Endpoint   = "https://google.serper.dev/search";

    public async Task<IEnumerable<string>> SearchAsync(string query, int maxResults, CancellationToken ct = default)
    {
        var apiKey = options.Value.SerperApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Serper API key not configured.");
            return [];
        }

        try
        {
            var client  = httpClientFactory.CreateClient(ClientName);
            var body    = JsonSerializer.Serialize(new { q = query, num = maxResults, gl = "us", hl = "en" });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("Serper returned HTTP {Status} for '{Query}'. Body: {Body}",
                    (int)response.StatusCode, query, errorBody);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var urls = new List<string>();
            if (doc.RootElement.TryGetProperty("organic", out var organic))
            {
                foreach (var item in organic.EnumerateArray())
                {
                    if (item.TryGetProperty("link", out var link) && link.GetString() is { } url)
                        urls.Add(url);
                }
            }

            logger.LogInformation("Serper returned {Count} URLs for '{Query}'.", urls.Count, query);
            return urls;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Serper search failed for query '{Query}'.", query);
            return [];
        }
    }
}
