namespace ConsoleScout.Contracts;

/// <summary>Several pages read in one call; the scraper loads them in parallel.</summary>
public sealed record FetchBatchRequest(IReadOnlyList<FetchRequest> Pages)
{
    /// <summary>The most pages the scraper accepts in one batch.</summary>
    public const int MaxPages = 10;
}
