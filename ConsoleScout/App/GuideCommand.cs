using ConsoleScout.Output;

namespace ConsoleScout.App;

/// <summary>A run without arguments. When everything a question needs is set up, it goes straight to asking
/// for one. Otherwise - a missing key, a broken configuration - it shows what the program is, how to use it
/// and what is missing, and asks nothing. Needs no working configuration, so it helps on a fresh machine too.</summary>
internal sealed class GuideCommand(IConsoleUi ui)
{
    /// <returns>The question the user typed, or null when there is none to answer.</returns>
    public async Task<string?> RunAsync()
    {
        var readiness = await SetupStatus.CheckAsync(probeScraper: false);
        if (readiness.CanAnswer && ui.CanAsk)
        {
            var question = ui.AskQuestion();
            return string.IsNullOrWhiteSpace(question) ? null : question;
        }

        var guide = UsageGuide.Load();
        ui.WriteMarkdown(guide);

        var status = await SetupStatus.CheckAsync();
        ui.WriteMarkdown(status.Markdown);
        return null;
    }
}
