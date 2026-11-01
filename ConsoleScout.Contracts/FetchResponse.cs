namespace ConsoleScout.Contracts;

/// <param name="Rendered">True when the text came from the browser rather than from plain HTTP.</param>
public sealed record FetchResponse(
    string Url,
    string FinalUrl,
    int Status,
    string Title,
    string Text,
    bool Truncated,
    bool Rendered);
