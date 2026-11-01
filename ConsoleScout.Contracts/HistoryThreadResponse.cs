namespace ConsoleScout.Contracts;

/// <summary>Oldest first; the last entry is the one that was referenced.</summary>
public sealed record HistoryThreadResponse(IReadOnlyList<HistoryEntry> Entries);
