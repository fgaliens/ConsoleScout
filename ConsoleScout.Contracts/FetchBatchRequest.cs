namespace ConsoleScout.Contracts;

/// <summary>Several pages read in one call; the agent loads them in parallel.</summary>
public sealed record FetchBatchRequest(IReadOnlyList<FetchRequest> Pages)
{
    /// <summary>The most pages the agent accepts in one batch.</summary>
    public const int MaxPages = 10;
}
