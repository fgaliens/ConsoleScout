namespace ConsoleScout.Scraper;

internal sealed class BrowserSettings
{
    /// <summary>"chrome" or "msedge" uses the installed browser; null uses Playwright's bundled
    /// Chromium, which is downloaded on first use. An unavailable channel falls back to Chromium.</summary>
    public string? Channel { get; set; } = "chrome";

    /// <summary>False opens a visible window: slower, but sites treat it as a regular browser.</summary>
    public bool Headless { get; set; } = true;

    public int MaxPages { get; set; } = 4;

    public int NavigationTimeoutSeconds { get; set; } = 30;
}
