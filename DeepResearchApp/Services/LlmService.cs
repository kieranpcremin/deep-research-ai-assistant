using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DeepResearchApp.Models;
using Microsoft.Extensions.Logging;
using WebResearchScraper.Models;

namespace DeepResearchApp.Services;

public sealed class LlmService(
    IHttpClientFactory httpClientFactory,
    ILogger<LlmService> logger) : ILlmService
{
    // Truncate each source to stay within context limits
    private const int MaxSourceChars = 3000;

    public async IAsyncEnumerable<string> StreamReportAsync(
        string query,
        IReadOnlyList<SearchResult> sources,
        LlmProvider provider,
        string apiKey,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(query, sources);

        var stream = provider switch
        {
            LlmProvider.OpenAI => StreamOpenAiCompatibleAsync(
                "https://api.openai.com/v1/chat/completions",
                "gpt-4o", apiKey, systemPrompt, userPrompt, ct),

            LlmProvider.Groq => StreamOpenAiCompatibleAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                "llama-3.3-70b-versatile", apiKey, systemPrompt, userPrompt, ct),

            LlmProvider.Claude => StreamClaudeAsync(apiKey, systemPrompt, userPrompt, ct),

            LlmProvider.Gemini => StreamGeminiAsync(apiKey, systemPrompt, userPrompt, ct),

            _ => throw new NotSupportedException($"Provider {provider} is not supported.")
        };

        await foreach (var chunk in stream.WithCancellation(ct))
            yield return chunk;
    }

    // ── OpenAI-compatible (OpenAI + Groq share identical SSE format) ─────────

    private async IAsyncEnumerable<string> StreamOpenAiCompatibleAsync(
        string baseUrl,
        string model,
        string apiKey,
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model,
            stream = true,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt   }
            }
        });

        using var client = httpClientFactory.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("OpenAI-compatible request failed: {Status} — {Body}", (int)response.StatusCode, errBody);
                throw new HttpRequestException($"LLM API returned {(int)response.StatusCode}: {errBody}");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenAI-compatible request failed.");
            throw;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..].Trim();
            if (data == "[DONE]") break;

            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var content))
                {
                    text = content.GetString();
                }
            }
            catch { /* ignore malformed chunks */ }

            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    // ── Anthropic Claude ─────────────────────────────────────────────────────

    private async IAsyncEnumerable<string> StreamClaudeAsync(
        string apiKey,
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = "claude-sonnet-4-6",
            max_tokens = 8192,
            stream = true,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userPrompt } }
        });

        using var client = httpClientFactory.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("Claude request failed: {Status} — {Body}", (int)response.StatusCode, errBody);
                throw new HttpRequestException($"Claude API returned {(int)response.StatusCode}: {errBody}");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Claude request failed.");
            throw;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..].Trim();
            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("type", out var typeProp) &&
                    typeProp.GetString() == "content_block_delta" &&
                    doc.RootElement.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("text", out var textProp))
                {
                    text = textProp.GetString();
                }
            }
            catch { /* ignore */ }

            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    // ── Google Gemini ─────────────────────────────────────────────────────────

    private async IAsyncEnumerable<string> StreamGeminiAsync(
        string apiKey,
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:streamGenerateContent?key={apiKey}&alt=sse";

        var body = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            }
        });

        using var client = httpClientFactory.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("Gemini request failed: {Status} — {Body}", (int)response.StatusCode, errBody);
                throw new HttpRequestException($"Gemini API returned {(int)response.StatusCode}: {errBody}");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gemini request failed.");
            throw;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..].Trim();
            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textProp))
                {
                    text = textProp.GetString();
                }
            }
            catch { /* ignore */ }

            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    // ── Stage 2: Elaboration ─────────────────────────────────────────────────

    public async IAsyncEnumerable<string> StreamElaborationAsync(
        string query,
        string initialReport,
        LlmProvider provider,
        string apiKey,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var systemPrompt = BuildElaborationSystemPrompt();
        var userPrompt   = BuildElaborationUserPrompt(query, initialReport);

        var stream = provider switch
        {
            LlmProvider.OpenAI => StreamOpenAiCompatibleAsync(
                "https://api.openai.com/v1/chat/completions",
                "gpt-4o", apiKey, systemPrompt, userPrompt, ct),

            LlmProvider.Groq => StreamOpenAiCompatibleAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                "llama-3.3-70b-versatile", apiKey, systemPrompt, userPrompt, ct),

            LlmProvider.Claude => StreamClaudeAsync(apiKey, systemPrompt, userPrompt, ct),

            LlmProvider.Gemini => StreamGeminiAsync(apiKey, systemPrompt, userPrompt, ct),

            _ => throw new NotSupportedException($"Provider {provider} is not supported.")
        };

        await foreach (var chunk in stream.WithCancellation(ct))
            yield return chunk;
    }

    // ── Prompt builders ──────────────────────────────────────────────────────

    // Stage 1: tight synthesis — extract and structure facts from sources
    private static string BuildSystemPrompt() => """
        You are an expert research analyst. Your task is to synthesize web research sources into a structured, factual initial report.

        Structure your report exactly as follows (use these exact Markdown headers):

        # [Descriptive title based on the query]

        ## Executive Summary
        A concise 2–3 paragraph overview of the most important findings.

        ## Key Findings
        Bullet points of the most critical discoveries. Cite sources inline like [Source 1], [Source 3].

        ## Core Analysis
        Thematic sections covering the main topics from the sources. Use sub-headers for distinct themes.
        Stay close to what the sources say — do not elaborate beyond what they support.

        ## Conclusions
        What the sources collectively conclude. Note any gaps or conflicting information.

        ## References
        Numbered list: [Source N] Title — URL

        Rules:
        - Cite sources throughout using [Source N] notation
        - Be accurate and faithful to the source material — this is a synthesis, not an essay
        - Use proper Markdown (bold, bullet points, tables where useful)
        - Do not make up information not present in the sources
        """;

    private static string BuildUserPrompt(string query, IReadOnlyList<SearchResult> sources)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**Research Query:** {query}");
        sb.AppendLine();
        sb.AppendLine("## Research Sources");
        sb.AppendLine();

        for (int i = 0; i < sources.Count; i++)
        {
            var src     = sources[i];
            var content = src.MarkdownContent.Length > MaxSourceChars
                ? src.MarkdownContent[..MaxSourceChars] + "\n\n[...content truncated...]"
                : src.MarkdownContent;

            sb.AppendLine($"### Source {i + 1}: {src.Title}");
            sb.AppendLine($"URL: {src.Url}");
            sb.AppendLine();
            sb.AppendLine(content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        sb.AppendLine("Synthesise the sources into the structured initial report now.");
        return sb.ToString();
    }

    // Stage 2: elaborate — enrich the initial report with depth and practicality
    private static string BuildElaborationSystemPrompt() => """
        You are an expert content enhancer specialising in research elaboration. You will receive an initial research report and must produce a significantly richer, more actionable version of it.

        Your enhanced report MUST include all of the following for every major section:

        1. **Detailed concept explanations** — unpack complex ideas so a non-specialist can understand them
        2. **Concrete real-world examples** — illustrate each key finding with a specific, named example
        3. **Case studies** — at least one in-depth case study that brings the research to life
        4. **Practical implications** — what should practitioners, businesses, or individuals actually do with this information?
        5. **Stakeholder analysis** — how does this affect different groups (e.g. businesses, consumers, researchers, policymakers, society)?
        6. **Forward-looking analysis** — emerging trends, open questions, and what to watch for next

        Structure your enhanced report as:

        # [Same or improved title]

        ## Executive Summary
        ## Key Findings
        ## Detailed Analysis  ← expand each theme with examples and explanations
        ## Case Studies       ← one or more concrete case studies
        ## Practical Implications  ← actionable takeaways by stakeholder group
        ## Future Outlook
        ## Conclusions & Recommendations
        ## References         ← preserve all original citations

        Rules:
        - Preserve and expand on the original findings — never contradict them
        - Retain all [Source N] citations from the initial report
        - You may draw on well-known, widely accepted knowledge to add examples, but clearly distinguish inference from sourced facts
        - Use proper Markdown — headers, bold, bullet lists, tables where useful
        - Write with authority and depth — this is the final polished report
        """;

    private static string BuildElaborationUserPrompt(string query, string initialReport)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**Original Research Query:** {query}");
        sb.AppendLine();
        sb.AppendLine("## Initial Research Report (Stage 1 Synthesis)");
        sb.AppendLine();
        sb.AppendLine(initialReport);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Please produce the complete enhanced and elaborated research report now. Expand every section with detailed explanations, real-world examples, case studies, practical implications, stakeholder analysis, and forward-looking insights.");
        return sb.ToString();
    }
}
