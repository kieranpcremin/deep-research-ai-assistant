# 🔬 Deep Research AI Assistant

> **Turn any question into a fully-researched, AI-elaborated report in minutes** — powered by live web search, intelligent content extraction, and a two-stage AI synthesis pipeline.

---

## ✨ What It Does

Most AI tools answer questions from training data alone. **Deep Research AI Assistant** goes further — it searches the live web, reads the actual source pages, then puts them through a two-stage AI pipeline to produce reports with real depth and practical value.

```
Your Question  →  Live Web Search + Video Search  →  Fetch & Extract Sources  →  Stage 1: Synthesise  →  Stage 2: Elaborate  →  Rich Report + Related Videos
```

---

## 🚀 Features

### 🌐 Live Web Research
- Searches the web in real-time via **Serper** (Google Search API)
- Fetches and extracts full content from top results — not just snippets
- Handles static pages and JavaScript-rendered SPAs via **Playwright** fallback
- Live source cards show fetch progress as it happens

### 🎬 Related YouTube Videos
- Automatically searches for top YouTube videos on the topic **in parallel** with web search — no extra wait
- Displays a responsive thumbnail grid with channel name, duration, and publish date
- Click any card to open and watch directly on YouTube

### 🧠 Two-Stage AI Pipeline
- **Stage 1 — Research Synthesis**: Structures and cross-references the raw sources into a coherent, cited report faithful to the source material
- **Stage 2 — Elaboration**: Enriches the synthesis with:
  - 📖 Detailed concept explanations
  - 💡 Real-world examples
  - 📊 In-depth case studies
  - ⚙️ Practical implications & actionable takeaways
  - 👥 Stakeholder analysis (businesses, consumers, policymakers, researchers)
  - 🔭 Forward-looking trends and future outlook

### 🤖 Multi-Provider LLM Support
Choose your AI provider — all support streaming output so you see the report build in real time:

| Provider | Model |
|---|---|
| 🟢 OpenAI | GPT-4o |
| 🟠 Claude | claude-sonnet-4-6 |
| 🔵 Gemini | Gemini 2.0 Flash |
| ⚡ Groq | Llama 3.3 70B |

### 🔑 Privacy-First Key Management
- API keys are stored **only in your browser's localStorage** — never sent to any server
- Keys persist across page refreshes and app restarts
- Per-provider configuration with live status badges
- One-click clear with confirmation prompt

### 📄 Report Export
- **Copy Markdown** to clipboard
- **Download as `.md`** file for use in Obsidian, Notion, or any editor
- **Export as PDF** — opens a clean, print-optimised view and triggers the browser's save-as-PDF dialog; no dependencies, perfect typography

---

## 🏗️ Architecture

The solution is split into two projects:

```
deep-research-ai-assistant/
├── DeepResearchApp/          # Blazor Server web app (the UI + orchestration)
│   ├── Components/Pages/     # Research.razor, Config.razor
│   ├── Services/             # LlmService, ApiKeyStore, LocalStorageService
│   └── Models/               # LlmProvider enum
│
└── WebResearchScraper/       # Reusable .NET 9 class library
    ├── Services/             # SerperSearchService, HybridWebFetcher, AngleSharpContentExtractor
    ├── Interfaces/           # ISearchService, IWebFetcher, IContentExtractor
    └── Models/               # ScraperOptions, SearchResult
```

**WebResearchScraper** is a standalone library that can be referenced by any .NET project needing web research capabilities.

---

## 🛠️ Tech Stack

- **[.NET 9](https://dotnet.microsoft.com/)** + **Blazor Server** (InteractiveServer render mode)
- **[Serper API](https://serper.dev)** — Google Search (2,500 free searches/month)
- **[AngleSharp](https://anglesharp.github.io/)** — HTML parsing and content extraction
- **[ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net)** — HTML → Markdown conversion
- **[Microsoft Playwright](https://playwright.dev/dotnet/)** — JS-rendered page fallback
- **[Markdig](https://github.com/xoofx/markdig)** — Markdown → HTML rendering
- **Server-Sent Events (SSE)** — Real-time streaming from all LLM providers

---

## ⚡ Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A [Serper API key](https://serper.dev) (free tier: 2,500 searches/month)
- At least one LLM provider API key (OpenAI, Anthropic, Google, or Groq)

### Run Locally

```bash
git clone https://github.com/kieranpcremin/deep-research-ai-assistant.git
cd deep-research-ai-assistant
dotnet run --project DeepResearchApp/DeepResearchApp.csproj
```

Then open **http://localhost:5106** in your browser.

### First-Time Setup
1. Navigate to **⚙️ API Keys** in the app
2. Enter your **Serper API key** and at least one **LLM provider key**
3. Click **Save Keys** — keys are stored in your browser and restored automatically
4. Head to **🔬 Research**, type your query, and hit **Start Research**

---

## 📸 How It Works

1. **Enter a research query** — anything from market analysis to scientific topics
2. **Choose your AI provider** and number of sources (3–15)
3. **Watch live** as sources are searched, fetched, and extracted in real time
4. **Stage 1** streams a structured synthesis grounded in the source material
5. **Stage 2** elaborates it into a rich, actionable report with examples and case studies
6. **Export** the final report as Markdown

---

## 📝 License

MIT — free to use, modify, and distribute.

---

<p align="center">Built with .NET 9 · Blazor Server · Serper · OpenAI · Claude · Gemini · Groq</p>
