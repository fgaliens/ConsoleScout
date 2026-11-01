using ConsoleScout.Contracts;
using ConsoleScout.Agent;

namespace ConsoleScout.Tools.WebSearch;

/// <summary>One search backend. Engines the agent reads (Google, DuckDuckGo) share
/// <see cref="AgentSearchProvider"/>; one with its own API would implement this directly.
/// Registered as a keyed service under <see cref="Name"/>, which is what Tools:WebSearch:Providers lists.</summary>
internal interface ISearchProvider
{
    string Name { get; }

    /// <exception cref="AgentException">The backend answered with an error.</exception>
    /// <exception cref="AgentUnavailableException">The backend could not be reached at all.</exception>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct);
}
