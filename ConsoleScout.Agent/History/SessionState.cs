namespace ConsoleScout.Agent.History;

/// <summary>A terminal session's dialog mode, and the answer its next question continues while the mode is on.</summary>
internal sealed record SessionState(bool DialogMode, long? CurrentId)
{
    public static SessionState Off { get; } = new(false, null);
}
