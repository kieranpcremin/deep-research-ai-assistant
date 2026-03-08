using DeepResearchApp.Models;

namespace DeepResearchApp.Services;

public interface IVideoSearchService
{
    Task<IReadOnlyList<VideoResult>> SearchVideosAsync(string query, int maxResults, CancellationToken ct = default);
}
