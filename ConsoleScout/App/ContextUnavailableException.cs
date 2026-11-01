namespace ConsoleScout.App;

/// <summary>-c refers to an answer that cannot be found or read; the message says why.</summary>
internal sealed class ContextUnavailableException(string message) : Exception(message);
