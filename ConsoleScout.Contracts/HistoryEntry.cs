namespace ConsoleScout.Contracts;

/// <summary>One answered question. <see cref="Id"/> is what the user refers to as m&lt;id&gt;.</summary>
public sealed record HistoryEntry(long Id, string Question, string Answer);
