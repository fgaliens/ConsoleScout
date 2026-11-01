namespace ConsoleScout.Contracts;

/// <summary>Contents of scraper.json: written by a running scraper, read by the CLI to find it.</summary>
public sealed record ScraperEndpoint(string Url, string Token, int Pid);
