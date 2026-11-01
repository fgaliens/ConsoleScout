namespace ConsoleScout.Configuration;

/// <summary>The question history behind -c, kept by the agent.</summary>
internal sealed class HistorySettings
{
    /// <summary>When false nothing is saved, the agent is not started for it, and -c is refused.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How many earlier question-answer pairs -c hands the model, counting back from the referenced one.</summary>
    public int MaxContextTurns { get; set; } = 5;
}
