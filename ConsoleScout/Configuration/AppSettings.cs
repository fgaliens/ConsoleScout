namespace ConsoleScout.Configuration;

// Classes rather than records: a record's generated ToString would print the API keys.
// Settable rather than init-only: the AOT binding generator silently skips init-only properties.

/// <summary>Bound from IConfiguration; see <see cref="ConfigurationLoader"/> for the sources.</summary>
internal sealed class AppSettings
{
    public ModelsSettings Models { get; set; } = new();

    public ToolsSettings Tools { get; set; } = new();

    public AgentSettings Agent { get; set; } = new();

    public OutputSettings Output { get; set; } = new();

    public PreferencesSettings Preferences { get; set; } = new();
}
