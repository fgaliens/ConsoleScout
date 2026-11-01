namespace ConsoleScout.Tools;

/// <summary>A failure the model should read and react to (retry, try another source, or tell the user),
/// rather than one that ends the run.</summary>
internal sealed class ToolException(string message) : Exception(message);
