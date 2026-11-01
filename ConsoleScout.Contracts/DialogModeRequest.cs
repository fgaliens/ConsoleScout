namespace ConsoleScout.Contracts;

/// <summary>Changes the dialog mode of <paramref name="Session"/>.</summary>
/// <param name="Enable">True turns it on (keeping it on if it is); null toggles it.</param>
/// <param name="Reference">Turns it on to continue this answer's conversation instead of starting a new one.</param>
public sealed record DialogModeRequest(string Session, bool? Enable, HistoryReference? Reference);
