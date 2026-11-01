namespace ConsoleScout.Contracts;

/// <summary>What the next question of <paramref name="Session"/> follows up on. The agent decides, so a question
/// costs one request: an explicit <paramref name="Reference"/> (-c), a new dialog (-n), the session's dialog mode,
/// or nothing.</summary>
/// <param name="MaxTurns">How many entries at most, counting back from the one followed up on.</param>
public sealed record HistoryContextRequest(string Session, HistoryReference? Reference, bool NewDialog, int MaxTurns);
