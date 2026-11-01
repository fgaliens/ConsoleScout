namespace ConsoleScout.Agent;

internal sealed class HistorySettings
{
    /// <summary>Entries older than this are removed when the agent starts.</summary>
    public int KeepDays { get; set; } = 30;
}
