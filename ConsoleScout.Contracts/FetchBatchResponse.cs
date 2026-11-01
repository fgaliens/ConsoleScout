namespace ConsoleScout.Contracts;

/// <summary>One item per requested page, in request order.</summary>
public sealed record FetchBatchResponse(IReadOnlyList<FetchBatchItem> Pages);
