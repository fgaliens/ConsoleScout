using ConsoleScout.AI;
using ConsoleScout.Agent;
using ConsoleScout.Configuration;
using ConsoleScout.Contracts;
using ConsoleScout.Output;
using ConsoleScout.Session;

namespace ConsoleScout.App;

/// <summary>One question, start to finish: recall the conversation it follows up on (-c), let the assistant
/// answer while the spinner runs, show the answer, and save it to the history with an id to refer back to.
/// Without a question, GuideCommand runs instead.</summary>
internal sealed class AskCommand(
    Assistant assistant,
    IConsoleUi ui,
    IActivityReporter activity,
    QuestionContext context,
    AgentClient agent,
    AppSettings settings)
{
    /// <summary>How long saving may take before a spinner says that it is still going on.</summary>
    private static readonly TimeSpan SpinnerDelay = TimeSpan.FromMilliseconds(300);

    private const string DonateHint = "Like this tool? Help the developer: run ConsoleScout --donate for details.";

    /// <param name="reference">The earlier answer to follow up on, or null for a standalone question.</param>
    public async Task RunAsync(string question, AnswerLength length, ContextReference? reference, CancellationToken ct)
    {
        if (reference is not null && !settings.History.Enabled)
        {
            throw new ContextUnavailableException("-c needs the history, which is turned off (history.enabled).");
        }

        context.Question = question;
        var hint = settings.Preferences.ShowDonateHint ? DonateHint : null;

        IReadOnlyList<HistoryEntry> earlier = [];
        var answer = await ui.RunWithStatusAsync("Thinking", async () =>
        {
            if (reference is not null)
            {
                earlier = await RecallAsync(reference, ct);
            }

            return await assistant.AskAsync(question, length, earlier, ct);
        }, hint);

        ui.WriteAnswer(answer);

        if (settings.History.Enabled && answer.Length > 0)
        {
            long? parent = earlier.Count > 0 ? earlier[^1].Id : null;
            await SaveAsync(question, answer, parent, ct);
        }
    }

    private async Task<IReadOnlyList<HistoryEntry>> RecallAsync(ContextReference reference, CancellationToken ct)
    {
        var description = reference.Id is { } id ? $"answer x{id}" : reference.Back == 0 ? "the last answer" : $"the answer {reference.Back} back";
        activity.Report("Recalling", description);

        var request = new HistoryThreadRequest(TerminalSession.Id, reference.Id, reference.Back, settings.History.MaxContextTurns);
        try
        {
            return await agent.GetHistoryThreadAsync(request, ct);
        }
        catch (AgentException ex)
        {
            throw new ContextUnavailableException(ex.Message);
        }
        catch (AgentUnavailableException ex)
        {
            throw new ContextUnavailableException($"The history cannot be read: {ex.Message}");
        }
    }

    /// <summary>A failure here costs only the follow-up, never the answer already on screen.</summary>
    /// <summary>A failure here costs only the follow-up, never the answer already on screen: even an agent
    /// that cannot be found at all (a configuration error) ends in a note, not in a failed run.</summary>
    private async Task SaveAsync(string question, string answer, long? parent, CancellationToken ct)
    {
        var request = new AppendHistoryRequest(TerminalSession.Id, parent, question, answer);
        try
        {
            var saving = agent.AppendHistoryAsync(request, ct);
            var id = await WaitVisiblyAsync(saving);
            ui.WriteAnswerId(id);
        }
        catch (Exception ex) when (ex is AgentException or AgentUnavailableException or ConfigurationException)
        {
            ui.WriteNote($"(not saved to the history: {ex.Message})");
        }
    }

    /// <summary>Usually the agent is up by now and saving takes milliseconds, so a spinner would only flash.
    /// When the agent is still starting, the wait can be long, and a silent terminal looks stuck.</summary>
    private async Task<long> WaitVisiblyAsync(Task<long> saving)
    {
        var grace = Task.Delay(SpinnerDelay);
        var first = await Task.WhenAny(saving, grace);
        if (first == saving)
        {
            return await saving;
        }

        return await ui.RunWithStatusAsync("Saving to history", () => saving);
    }
}
