namespace ConsoleScout.Configuration;

internal sealed class WebSearchSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Tried in order: when one is blocked (a CAPTCHA, say), the next one answers instead.
    /// Empty here because the binder appends configured items to a default list instead of replacing it;
    /// <see cref="GetProviders"/> supplies the default.</summary>
    public List<string> Providers { get; set; } = [];

    public IReadOnlyList<string> GetProviders() => Providers.Count > 0 ? Providers : ["google", "duckduckgo"];

    public int MaxResults { get; set; } = 5;
}
