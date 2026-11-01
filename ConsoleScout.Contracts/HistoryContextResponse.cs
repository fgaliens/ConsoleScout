namespace ConsoleScout.Contracts;

/// <param name="Entries">The conversation so far, oldest first; empty for a question that starts a dialog.</param>
/// <param name="DialogId">The dialog the question continues; null when it starts one.</param>
/// <param name="DialogMode">Whether dialog mode is on in the session.</param>
public sealed record HistoryContextResponse(IReadOnlyList<HistoryEntry> Entries, long? DialogId, bool DialogMode);
