using DeepResearchApp.Components;
using DeepResearchApp.Services;
using WebResearchScraper.Extensions;
using WebResearchScraper.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// WebResearchScraper — registers IWebFetcher and IContentExtractor (no Serper key needed at startup)
builder.Services.AddWebResearchScraper(o =>
{
    o.MaxResults = 10;
    o.MaxConcurrency = 4;
});

// Runtime API key store (scoped = one per Blazor circuit/session)
builder.Services.AddScoped<ApiKeyStore>();

// Override the library's SerperSearchService with our runtime-keyed version
// (last registration wins for single-service resolution)
builder.Services.AddScoped<ISearchService, RuntimeSerperSearchService>();

// LLM streaming service
builder.Services.AddScoped<ILlmService, LlmService>();

// YouTube video search via Serper
builder.Services.AddScoped<IVideoSearchService, VideoSearchService>();

// localStorage persistence for API keys
builder.Services.AddScoped<LocalStorageService>();

// Named HttpClient for RuntimeSerperSearchService
builder.Services.AddHttpClient("SerperRuntime", c => c.Timeout = TimeSpan.FromSeconds(10));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
