using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using ReverseMarkdown;
using WebResearchScraper.Interfaces;

namespace WebResearchScraper.Services;

public sealed partial class AngleSharpContentExtractor(ILogger<AngleSharpContentExtractor> logger) : IContentExtractor
{
    private static readonly string[] BoilerplateSelectors =
    [
        "script", "style", "noscript", "nav", "header", "footer", "aside",
        "form", "iframe", "[role=navigation]", ".cookie-banner", ".ads",
        ".sidebar", ".comments", ".social-share"
    ];

    private static readonly string[] ContentSelectors =
    [
        "article",
        "[role=main]",
        "main",
        ".post-content",
        ".article-body",
        ".entry-content",
        "#content",
        "#main-content",
        "div.content",
        "body"
    ];

    private static readonly Converter MarkdownConverter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Drop,
        GithubFlavored = true,
        SmartHrefHandling = true
    });

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlines();

    public async Task<(string Title, string Markdown)> ExtractAsync(string html, string url)
    {
        try
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>()!;
            var document = await parser.ParseDocumentAsync(html);

            var title = ExtractTitle(document, url);

            // Remove boilerplate elements
            foreach (var selector in BoilerplateSelectors)
            {
                foreach (var el in document.QuerySelectorAll(selector).ToList())
                    el.Remove();
            }

            // Find main content element
            IElement? contentElement = null;
            foreach (var selector in ContentSelectors)
            {
                var el = document.QuerySelector(selector);
                if (el is not null && !string.IsNullOrWhiteSpace(el.InnerHtml))
                {
                    contentElement = el;
                    break;
                }
            }

            if (contentElement is null)
            {
                logger.LogDebug("No content element found for {Url}.", url);
                return (title, string.Empty);
            }

            var rawMarkdown = MarkdownConverter.Convert(contentElement.InnerHtml);
            var collapsed = MultipleNewlines().Replace(rawMarkdown, "\n\n").Trim();

            return (title, collapsed);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Content extraction failed for {Url}.", url);
            return (GetHostFromUrl(url), string.Empty);
        }
    }

    private static string ExtractTitle(AngleSharp.Html.Dom.IHtmlDocument document, string url)
    {
        if (!string.IsNullOrWhiteSpace(document.Title))
            return document.Title.Trim();

        var h1 = document.QuerySelector("h1");
        if (h1 is not null && !string.IsNullOrWhiteSpace(h1.TextContent))
            return h1.TextContent.Trim();

        return GetHostFromUrl(url);
    }

    private static string GetHostFromUrl(string url)
    {
        try { return new Uri(url).Host; }
        catch { return url; }
    }
}
