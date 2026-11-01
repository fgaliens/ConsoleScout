namespace ConsoleScout.Agent;

internal sealed class AgentSettings
{
    /// <summary>The service stops itself after this long without a request, so an auto-started
    /// instance does not linger forever.</summary>
    public int IdleMinutes { get; set; } = 15;

    public BrowserSettings Browser { get; set; } = new();

    public FetchSettings Fetch { get; set; } = new();
}
