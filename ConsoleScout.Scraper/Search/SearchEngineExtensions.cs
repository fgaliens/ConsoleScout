using System.Text.Json;
using ConsoleScout.Contracts;

namespace ConsoleScout.Scraper.Search;

internal static class SearchEngineExtensions
{
    /// <summary>The page scripts return a JSON array of { url, title, snippet }: Playwright's own
    /// deserialization of anonymous objects is looser than this, and the engines share the shape.</summary>
    public static IReadOnlyList<SearchHit> ParseHits(string json) =>
        JsonSerializer.Deserialize<List<SearchHit>>(json, JsonSerializerOptions.Web) ?? [];
}
