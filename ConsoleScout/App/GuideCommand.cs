using ConsoleScout.Output;

namespace ConsoleScout.App;

/// <summary>A run without arguments: shows what the program is and how to use it, and the state of this
/// machine's setup; then asks for a question, when the terminal allows it and a question could be answered.
/// Needs no working configuration, so it also helps on a fresh or misconfigured machine.</summary>
internal sealed class GuideCommand(IConsoleUi ui)
{
    /// <returns>The question the user typed, or null when there is none to answer.</returns>
    public async Task<string?> RunAsync()
    {
        var guide = UsageGuide.Load();
        ui.WriteMarkdown(guide);

        var status = await SetupStatus.CheckAsync();
        ui.WriteMarkdown(status.Markdown);

        if (!status.CanAnswer || !ui.CanAsk)
        {
            return null;
        }

        ui.WriteInfo(string.Empty);
        var question = ui.AskQuestion();
        return string.IsNullOrWhiteSpace(question) ? null : question;
    }
}
