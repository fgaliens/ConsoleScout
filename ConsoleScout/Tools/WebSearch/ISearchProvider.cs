using ConsoleScout.Contracts;
using ConsoleScout.Scraper;

namespace ConsoleScout.Tools.WebSearch;

/// <summary>One search backend. Engines the scraper reads (Google, DuckDuckGo) share
/// <see cref="ScraperSearchProvider"/>; one with its own API would implement this directly.
/// Registered as a keyed service under <see cref="Name"/>, which is what Tools:WebSearch:Providers lists.</summary>
internal interface ISearchProvider
{
    string Name { get; }

    /// <exception cref="ScraperException">The backend answered with an error.</exception>
    /// <exception cref="ScraperUnavailableException">The backend could not be reached at all.</exception>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct);
}
