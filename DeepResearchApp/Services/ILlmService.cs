using DeepResearchApp.Models;
using WebResearchScraper.Models;

namespace DeepResearchApp.Services;

public interface ILlmService
{
    /// <summary>Stage 1 — Synthesise web sources into a structured initial report.</summary>
    IAsyncEnumerable<string> StreamReportAsync(
        string query,
        IReadOnlyList<SearchResult> sources,
        LlmProvider provider,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>Stage 2 — Enrich the initial report with examples, case studies, and practical implications.</summary>
    IAsyncEnumerable<string> StreamElaborationAsync(
        string query,
        string initialReport,
        LlmProvider provider,
        string apiKey,
        CancellationToken ct = default);
}
