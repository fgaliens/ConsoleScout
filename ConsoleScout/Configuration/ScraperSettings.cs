namespace ConsoleScout.Configuration;

/// <summary>Where to find the ConsoleScout.Scraper service, and whether to start it when it is not running.</summary>
internal sealed class ScraperSettings
{
    public bool AutoStart { get; set; } = true;

    /// <summary>Null looks in scraper/ next to the CLI executable (where dotnet publish puts it), next to the
    /// executable itself, and in Debug builds in the scraper's own build output.</summary>
    public string? ExecutablePath { get; set; }

    public int StartupTimeoutSeconds { get; set; } = 30;

    public int RequestTimeoutSeconds { get; set; } = 60;
}
