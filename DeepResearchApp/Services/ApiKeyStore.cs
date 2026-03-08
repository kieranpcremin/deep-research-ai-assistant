using DeepResearchApp.Models;

namespace DeepResearchApp.Services;

/// <summary>
/// Scoped service (one per Blazor circuit) that holds runtime API keys entered by the user.
/// </summary>
public sealed class ApiKeyStore
{
    public string? SerperApiKey { get; set; }
    public string? OpenAiKey    { get; set; }
    public string? ClaudeKey    { get; set; }
    public string? GeminiKey    { get; set; }
    public string? GroqKey      { get; set; }

    public LlmProvider SelectedProvider { get; set; } = LlmProvider.OpenAI;

    public string? GetKey(LlmProvider provider) => provider switch
    {
        LlmProvider.OpenAI => OpenAiKey,
        LlmProvider.Claude => ClaudeKey,
        LlmProvider.Gemini => GeminiKey,
        LlmProvider.Groq   => GroqKey,
        _ => null
    };

    public bool HasKey(LlmProvider provider) => !string.IsNullOrWhiteSpace(GetKey(provider));
    public bool HasSerperKey => !string.IsNullOrWhiteSpace(SerperApiKey);

    public IEnumerable<LlmProvider> ConfiguredProviders =>
        Enum.GetValues<LlmProvider>().Where(HasKey);
}
