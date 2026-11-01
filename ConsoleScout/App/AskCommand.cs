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
    private async Task SaveAsync(string question, string answer, long? parent, CancellationToken ct)
    {
        var request = new AppendHistoryRequest(TerminalSession.Id, parent, question, answer);
        try
        {
            var id = await agent.AppendHistoryAsync(request, ct);
            ui.WriteAnswerId(id);
        }
        catch (Exception ex) when (ex is AgentException or AgentUnavailableException)
        {
            ui.WriteNote($"(not saved to the history: {ex.Message})");
        }
    }
}
