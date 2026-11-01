using ConsoleScout.Contracts;

namespace ConsoleScout.Agent.Search;

/// <summary>One search engine, scraped through the browser. Registered in DI and looked up by
/// <see cref="Name"/>, which is what <see cref="SearchRequest.Engine"/> carries.</summary>
internal interface ISearchEngine
{
    string Name { get; }

    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct);
}
