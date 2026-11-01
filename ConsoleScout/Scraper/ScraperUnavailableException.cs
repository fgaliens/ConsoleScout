namespace ConsoleScout.Scraper;

/// <summary>The scraper cannot be reached or started.</summary>
internal sealed class ScraperUnavailableException(string message) : Exception(message);
