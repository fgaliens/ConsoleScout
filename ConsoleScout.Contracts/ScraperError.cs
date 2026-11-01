namespace ConsoleScout.Contracts;

/// <summary>Body of every non-2xx response. <see cref="Code"/> is one of <see cref="ScraperErrorCodes"/>.</summary>
public sealed record ScraperError(string Code, string Message);
