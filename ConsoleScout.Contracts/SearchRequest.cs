namespace ConsoleScout.Contracts;

public sealed record SearchRequest(string Engine, string Query, int MaxResults = 5, string? Language = null);
