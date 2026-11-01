using ConsoleScout.Contracts;

namespace ConsoleScout.Scraper;

/// <summary>An expected failure, returned to the caller as a <see cref="ScraperError"/>.
/// <see cref="Code"/> is one of <see cref="ScraperErrorCodes"/>.</summary>
internal sealed class ScraperFailure(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode => Code switch
    {
        ScraperErrorCodes.BadRequest => StatusCodes.Status400BadRequest,
        ScraperErrorCodes.UnsupportedEngine => StatusCodes.Status404NotFound,
        ScraperErrorCodes.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway,
    };
}
