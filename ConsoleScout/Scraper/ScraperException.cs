using ConsoleScout.Contracts;

namespace ConsoleScout.Scraper;

/// <summary>The scraper answered, with an error. <see cref="Code"/> is one of <see cref="ScraperErrorCodes"/>.</summary>
internal sealed class ScraperException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>The engine or site refused this client; another one may still answer.</summary>
    public bool IsBlocked => Code is ScraperErrorCodes.Captcha or ScraperErrorCodes.Blocked;
}
