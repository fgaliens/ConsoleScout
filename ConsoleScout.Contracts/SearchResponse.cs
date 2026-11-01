namespace ConsoleScout.Contracts;

public sealed record SearchResponse(IReadOnlyList<SearchHit> Results);
