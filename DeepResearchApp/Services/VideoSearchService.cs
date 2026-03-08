using System.Text;
using System.Text.Json;
using DeepResearchApp.Models;
using Microsoft.Extensions.Logging;

namespace DeepResearchApp.Services;

public sealed class VideoSearchService(
    IHttpClientFactory httpClientFactory,
    ApiKeyStore apiKeyStore,
    ILogger<VideoSearchService> logger) : IVideoSearchService
{
    private const string Endpoint = "https://google.serper.dev/videos";

    public async Task<IReadOnlyList<VideoResult>> SearchVideosAsync(
        string query, int maxResults, CancellationToken ct = default)
    {
        var apiKey = apiKeyStore.SerperApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Serper API key not configured — skipping video search.");
            return [];
        }

        try
        {
            var client  = httpClientFactory.CreateClient("SerperRuntime");
            var body    = JsonSerializer.Serialize(new { q = query, num = maxResults, gl = "us", hl = "en" });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Serper video search returned {Status}: {Body}", (int)response.StatusCode, errBody);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var results = new List<VideoResult>();
            if (doc.RootElement.TryGetProperty("videos", out var videos))
            {
                foreach (var item in videos.EnumerateArray())
                {
                    var title     = item.TryGetProperty("title",    out var t)  ? t.GetString()  ?? "" : "";
                    var link      = item.TryGetProperty("link",     out var l)  ? l.GetString()  ?? "" : "";
                    var channel   = item.TryGetProperty("channel",  out var c)  ? c.GetString()  ?? "" : "";
                    var duration  = item.TryGetProperty("duration", out var d)  ? d.GetString()  ?? "" : "";
                    var date      = item.TryGetProperty("date",     out var dt) ? dt.GetString() ?? "" : "";

                    // Serper may use either imageUrl or thumbnailUrl — try both, then fall back
                    // to the YouTube image CDN using the video ID extracted from the URL.
                    var thumbnail = "";
                    if (item.TryGetProperty("imageUrl",     out var img) && !string.IsNullOrEmpty(img.GetString()))
                        thumbnail = img.GetString()!;
                    else if (item.TryGetProperty("thumbnailUrl", out var th) && !string.IsNullOrEmpty(th.GetString()))
                        thumbnail = th.GetString()!;
                    else
                        thumbnail = ExtractYouTubeThumbnail(link);

                    if (!string.IsNullOrWhiteSpace(link))
                        results.Add(new VideoResult(title, link, channel, duration, thumbnail, date));

                    if (results.Count >= maxResults) break;
                }
            }

            logger.LogInformation("Serper video search returned {Count} videos for '{Query}'.", results.Count, query);
            return results;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Video search failed for '{Query}'.", query);
            return [];
        }
    }

    /// <summary>
    /// Derives a YouTube thumbnail URL from a watch link, e.g.
    /// https://www.youtube.com/watch?v=ABC → https://img.youtube.com/vi/ABC/hqdefault.jpg
    /// </summary>
    private static string ExtractYouTubeThumbnail(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var videoId = query["v"];
            if (!string.IsNullOrEmpty(videoId))
                return $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
        }
        catch { /* ignore malformed URLs */ }
        return "";
    }
}
