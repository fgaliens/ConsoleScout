using ConsoleScout.Contracts;
using ConsoleScout.Scraper;

namespace ConsoleScout.Tools.WebSearch;

internal sealed class ScraperSearchProvider(ScraperClient scraper, string engine) : ISearchProvider
{
    public string Name => engine;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct) =>
        (await scraper.SearchAsync(new SearchRequest(engine, query, maxResults), ct)).Results;
}
