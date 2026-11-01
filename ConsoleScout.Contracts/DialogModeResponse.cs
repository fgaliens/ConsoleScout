namespace ConsoleScout.Contracts;

/// <param name="WasOn">The mode before the change.</param>
/// <param name="CurrentId">The answer the next question continues; null when it starts a new dialog.</param>
/// <param name="DialogId">The dialog of <paramref name="CurrentId"/>.</param>
public sealed record DialogModeResponse(bool DialogMode, bool WasOn, long? CurrentId, long? DialogId);
