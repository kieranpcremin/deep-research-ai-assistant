using Microsoft.JSInterop;

namespace DeepResearchApp.Services;

/// <summary>
/// Thin wrapper around browser localStorage via JS interop.
/// All DeepResearch key names are centralised here.
/// </summary>
public sealed class LocalStorageService(IJSRuntime js)
{
    private static readonly string[] AllKeys =
    [
        "dr_serper", "dr_openai", "dr_claude", "dr_gemini", "dr_groq"
    ];

    public async Task<ApiKeyStore> LoadIntoStoreAsync(ApiKeyStore store)
    {
        var values = await js.InvokeAsync<Dictionary<string, string?>>("lsGetAll", [AllKeys]);

        // Additive merge: localStorage values take priority, but don't null out keys
        // that are already in memory (e.g. set via Config in the same session without saving).
        store.SerperApiKey = NullIfBlank(values.GetValueOrDefault("dr_serper")) ?? store.SerperApiKey;
        store.OpenAiKey    = NullIfBlank(values.GetValueOrDefault("dr_openai")) ?? store.OpenAiKey;
        store.ClaudeKey    = NullIfBlank(values.GetValueOrDefault("dr_claude")) ?? store.ClaudeKey;
        store.GeminiKey    = NullIfBlank(values.GetValueOrDefault("dr_gemini")) ?? store.GeminiKey;
        store.GroqKey      = NullIfBlank(values.GetValueOrDefault("dr_groq"))   ?? store.GroqKey;

        return store;
    }

    public async Task SaveFromStoreAsync(ApiKeyStore store)
    {
        await SetAsync("dr_serper", store.SerperApiKey);
        await SetAsync("dr_openai", store.OpenAiKey);
        await SetAsync("dr_claude", store.ClaudeKey);
        await SetAsync("dr_gemini", store.GeminiKey);
        await SetAsync("dr_groq",   store.GroqKey);
    }

    public async Task ClearAllAsync()
    {
        await js.InvokeVoidAsync("lsClear", [AllKeys]);
    }

    private async Task SetAsync(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            await js.InvokeVoidAsync("lsRemove", key);
        else
            await js.InvokeVoidAsync("lsSet", key, value);
    }

    private static string? NullIfBlank(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
