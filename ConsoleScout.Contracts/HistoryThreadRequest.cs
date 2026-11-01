namespace ConsoleScout.Contracts;

/// <summary>The conversation an entry belongs to: the entry and the ones it follows up on.
/// The entry is either <paramref name="Id"/> (any session) or, without it, the one <paramref name="Back"/>
/// steps before the latest of <paramref name="Session"/> (0 is the latest).</summary>
/// <param name="MaxTurns">How many entries at most, counting back from the referenced one.</param>
public sealed record HistoryThreadRequest(string Session, long? Id, int Back, int MaxTurns);
