namespace ConsoleScout.Output;

/// <summary>Something had to be asked, but there is no interactive terminal to ask it in
/// (input is redirected, or the run is scripted). The message says how to pass it instead.</summary>
internal sealed class InputUnavailableException(string message) : Exception(message);
