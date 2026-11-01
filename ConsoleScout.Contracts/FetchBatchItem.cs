namespace ConsoleScout.Contracts;

/// <summary>The outcome for one page of a batch: exactly one of <paramref name="Page"/> and
/// <paramref name="Error"/> is set, so one unreadable page does not fail the others.</summary>
/// <param name="Url">The URL as requested.</param>
public sealed record FetchBatchItem(string Url, FetchResponse? Page, ScraperError? Error);
