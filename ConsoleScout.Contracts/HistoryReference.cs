namespace ConsoleScout.Contracts;

/// <summary>Which earlier answer a question follows up on: answer <see cref="Id"/> (x&lt;id&gt;), the latest
/// answer of dialog <see cref="DialogId"/> (d&lt;id&gt;), or, with neither, the answer <see cref="Back"/> steps
/// before the latest one of the terminal session (0 is the latest).</summary>
public sealed record HistoryReference(long? Id, long? DialogId, int Back);
