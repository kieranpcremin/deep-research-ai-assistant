using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebResearchScraper.Interfaces;
using WebResearchScraper.Models;
using WebResearchScraper.Services;

namespace WebResearchScraper.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all WebResearchScraper services.
    /// </summary>
    /// <example>
    /// services.AddWebResearchScraper(o =>
    /// {
    ///     o.SerperApiKey = config["Serper:ApiKey"]!;
    ///     o.MaxResults = 8;
    /// });
    /// </example>
    public static IServiceCollection AddWebResearchScraper(
        this IServiceCollection services,
        Action<ScraperOptions>? configure = null)
    {
        // Options
        var optionsBuilder = services.AddOptions<ScraperOptions>()
            .BindConfiguration(ScraperOptions.SectionName);

        if (configure is not null)
            optionsBuilder.PostConfigure(configure);

        // Named HttpClient for Serper (10 s timeout)
        services.AddHttpClient("Serper", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Named HttpClient for static fetcher — timeout configured from options at request time
        services.AddHttpClient("Static", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<ScraperOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(opts.StaticTimeoutSeconds);
        });

        // Internal fetchers
        services.AddTransient<StaticWebFetcher>();
        services.AddTransient<PlaywrightWebFetcher>();

        // Public interfaces
        services.AddTransient<IWebFetcher, HybridWebFetcher>();
        services.AddTransient<ISearchService, SerperSearchService>();
        services.AddTransient<IContentExtractor, AngleSharpContentExtractor>();
        services.AddScoped<IWebResearchService, WebResearchService>();

        return services;
    }
}
