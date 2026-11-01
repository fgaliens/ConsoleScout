using ConsoleScout.Configuration;
using ConsoleScout.AI;
using ConsoleScout.Output;
using ConsoleScout.Tools;

namespace ConsoleScout.App;

/// <summary>One question, start to finish: let the assistant answer while the spinner runs, then show the
/// answer. Without a question, GuideCommand runs instead.</summary>
internal sealed class AskCommand(
    Assistant assistant,
    IConsoleUi ui,
    QuestionContext context,
    AppSettings settings)
{
    private const string DonateHint = "Like this tool? Help the developer: run ConsoleScout --donate for details.";

    public async Task RunAsync(string question, AnswerLength length, CancellationToken ct)
    {
        context.Question = question;
        var hint = settings.Preferences.ShowDonateHint ? DonateHint : null;
        var answer = await ui.RunWithStatusAsync("Thinking", () => assistant.AskAsync(question, length, ct), hint);

        ui.WriteAnswer(answer);
    }
}
