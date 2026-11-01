namespace ConsoleScout.Output;

/// <summary>Writes errors to stderr. Static because it is needed before the services that would
/// otherwise provide it exist: a broken configuration has to be reported too.</summary>
internal static class ErrorConsole
{
    public static void Write(string message) => Console.Error.WriteLine(message);
}
