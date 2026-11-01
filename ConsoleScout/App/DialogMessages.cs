using ConsoleScout.Contracts;

namespace ConsoleScout.App;

/// <summary>What csc -d says, shared by the mode command and by -d with a question.</summary>
internal static class DialogMessages
{
    private const string How = "csc -n <question> starts a new dialog, csc -d turns the mode off.";

    public static string Off => "Dialog mode is off in this terminal.";

    public static string On(DialogModeResponse mode) => mode.DialogId is { } dialog
        ? $"Dialog mode is on in this terminal, continuing dialog d{dialog} (from x{mode.CurrentId}). {How}"
        : $"Dialog mode is on in this terminal: each question continues the conversation. {How}";
}
