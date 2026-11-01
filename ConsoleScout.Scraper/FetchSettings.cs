namespace ConsoleScout.Scraper;

internal sealed class FetchSettings
{
    /// <summary>Lets web_fetch reach loopback and private addresses. Off by default: the URLs come
    /// from a model that reads untrusted pages, and must not be able to probe the local network.</summary>
    public bool AllowPrivateNetworks { get; set; } = false;

    public int HttpTimeoutSeconds { get; set; } = 20;

    /// <summary>Below this much extracted text, Auto mode assumes the page needs JavaScript.</summary>
    public int MinHttpTextChars { get; set; } = 500;

    public string UserAgent { get; set; } = "ConsoleScout/1.0 (personal console assistant)";
}
