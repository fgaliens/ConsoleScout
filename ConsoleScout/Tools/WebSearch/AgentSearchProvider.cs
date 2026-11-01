using ConsoleScout.Contracts;
using ConsoleScout.Agent;

namespace ConsoleScout.Tools.WebSearch;

internal sealed class AgentSearchProvider(AgentClient agent, string engine) : ISearchProvider
{
    public string Name => engine;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct) =>
        (await agent.SearchAsync(new SearchRequest(engine, query, maxResults), ct)).Results;
}
