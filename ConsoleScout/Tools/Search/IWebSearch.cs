namespace ConsoleScout.AssistantTools.Search;

/// <summary>A web search backend. It neither numbers nor formats results, it only yields them.</summary>
internal interface IWebSearch
{
    IAsyncEnumerable<SearchResult> SearchAsync(string query, CancellationToken ct = default);
}

/// <summary>The search failed for a reason worth showing the model.</summary>
internal sealed class SearchException(string reason) : Exception(reason);
