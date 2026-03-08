using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using WebResearchScraper.Models;

namespace WebResearchScraper.Services;

internal sealed class PlaywrightWebFetcher(
    IOptions<ScraperOptions> options,
    ILogger<PlaywrightWebFetcher> logger)
{
    private static readonly SemaphoreSlim InstallLock = new(1, 1);
    private static bool _browsersInstalled;

    public async Task<string?> FetchHtmlAsync(string url, CancellationToken ct = default)
    {
        await EnsureBrowsersInstalledAsync();

        IPlaywright? playwright = null;
        IBrowser? browser = null;
        try
        {
            var opts = options.Value;
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = !opts.PlaywrightHeaded
            });

            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                UserAgent = opts.UserAgent
            });

            try
            {
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = opts.PlaywrightTimeoutMs
                });
                await page.WaitForTimeoutAsync(2000);
            }
            catch (TimeoutException ex)
            {
                logger.LogDebug(ex, "Playwright navigation timed out for {Url} — attempting content extraction anyway.", url);
            }

            var html = await page.ContentAsync();
            logger.LogDebug("Playwright fetched {Chars} chars from {Url}.", html.Length, url);
            return html;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Playwright fetch failed for {Url}.", url);
            return null;
        }
        finally
        {
            if (browser is not null)
            {
                try { await browser.CloseAsync(); } catch { /* ignore */ }
            }
            playwright?.Dispose();
        }
    }

    private async Task EnsureBrowsersInstalledAsync()
    {
        if (_browsersInstalled) return;

        await InstallLock.WaitAsync();
        try
        {
            if (_browsersInstalled) return;
            logger.LogInformation("Installing Playwright browsers (first run)...");
            Microsoft.Playwright.Program.Main(["install", "chromium"]);
            _browsersInstalled = true;
            logger.LogInformation("Playwright browsers installed.");
        }
        finally
        {
            InstallLock.Release();
        }
    }
}
