using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebResearchScraper.Interfaces;
using WebResearchScraper.Models;

namespace WebResearchScraper.Services;

public sealed class WebResearchService(
    ISearchService searchService,
    IWebFetcher webFetcher,
    IContentExtractor contentExtractor,
    IOptions<ScraperOptions> defaultOptions,
    ILogger<WebResearchService> logger) : IWebResearchService
{
    public async Task<ResearchResults> ResearchAsync(
        string query,
        ScraperOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? defaultOptions.Value;
        var searchedAt = DateTimeOffset.UtcNow;

        var urls = (await searchService.SearchAsync(query, opts.MaxResults, ct)).ToList();
        logger.LogInformation("Researching '{Query}': {UrlCount} URLs to process.", query, urls.Count);

        var semaphore = new SemaphoreSlim(opts.MaxConcurrency);
        var bag = new ConcurrentBag<(int Index, SearchResult Result)>();
        var failedCount = 0;

        var tasks = urls.Select((url, index) => ProcessUrlAsync(url, index, semaphore, bag, opts, ct,
            onFailed: () => Interlocked.Increment(ref failedCount)));

        await Task.WhenAll(tasks);

        var ordered = bag
            .OrderBy(x => x.Index)
            .Select(x => x.Result)
            .ToList();

        return new ResearchResults
        {
            Query = query,
            SearchedAt = searchedAt,
            Results = ordered,
            FailedCount = failedCount
        };
    }

    private async Task ProcessUrlAsync(
        string url,
        int index,
        SemaphoreSlim semaphore,
        ConcurrentBag<(int, SearchResult)> bag,
        ScraperOptions opts,
        CancellationToken ct,
        Action onFailed)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            if (ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
            }

            var html = await webFetcher.FetchHtmlAsync(url, ct);
            if (string.IsNullOrWhiteSpace(html))
            {
                logger.LogWarning("No HTML fetched for {Url}.", url);
                onFailed();
                return;
            }

            var (title, markdown) = await contentExtractor.ExtractAsync(html, url);
            if (string.IsNullOrWhiteSpace(markdown))
            {
                logger.LogWarning("No content extracted from {Url}.", url);
                onFailed();
                return;
            }

            var wasJsRendered = html.Contains("__playwright__") ||
                                html.Length > 50_000 && markdown.Length > opts.MinContentLengthChars;

            bag.Add((index, new SearchResult
            {
                Url = url,
                Title = title,
                MarkdownContent = markdown,
                WasJsRendered = wasJsRendered
            }));

            logger.LogInformation("Processed [{Index}] {Url} — {Chars} markdown chars.", index, url, markdown.Length);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {Url}.", url);
            onFailed();
        }
        finally
        {
            semaphore.Release();
        }
    }
}
