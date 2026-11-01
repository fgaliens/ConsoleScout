using ConsoleScout.AI;
using ConsoleScout.Agent;
using ConsoleScout.Configuration;
using ConsoleScout.Contracts;
using ConsoleScout.Output;
using ConsoleScout.Session;

namespace ConsoleScout.App;

/// <summary>One question, start to finish: find the conversation it continues (-c, -n, dialog mode), let the
/// assistant answer while the spinner runs, show the answer, and save it to the history with its message and
/// dialog numbers. Without a question, GuideCommand runs instead.</summary>
internal sealed class AskCommand(
    Assistant assistant,
    IConsoleUi ui,
    IActivityReporter activity,
    QuestionContext context,
    AgentClient agent,
    DialogCommand dialogs,
    AppSettings settings)
{
    private const string DonateHint = "Like this tool? Help the developer: run ConsoleScout --donate for details.";

    /// <param name="reference">The earlier answer -c follows up on, or null.</param>
    /// <param name="dialog">-d: turn dialog mode on first (for <paramref name="reference"/>'s conversation, if given).</param>
    /// <param name="newDialog">-n: start a new dialog, even in dialog mode.</param>
    public async Task RunAsync(
        string question,
        AnswerLength length,
        ContextReference? reference,
        bool dialog,
        bool newDialog,
        CancellationToken ct)
    {
        if (!settings.History.Enabled && (reference is not null || dialog))
        {
            throw new ContextUnavailableException("-c and -d need the history, which is turned off (history.enabled).");
        }

        // -d with a question always turns the mode on; it never turns it off and drops the context.
        // With -c, the reference becomes the mode's current answer, so the context below continues it.
        if (dialog)
        {
            var enable = new DialogModeRequest(TerminalSession.Id, true, reference?.ToHistory());
            var mode = await dialogs.SetAsync(enable, settings, ct);
            if (!mode.WasOn || reference is not null)
            {
                var message = DialogMessages.On(mode);
                ui.WriteInfo(message);
            }

            reference = null;
        }

        context.Question = question;
        var hint = settings.Preferences.ShowDonateHint ? DonateHint : null;

        var conversation = new HistoryContextResponse([], null, false);
        string? unavailable = null;
        var answer = await ui.RunWithStatusAsync("Thinking", async () =>
        {
            if (settings.History.Enabled)
            {
                (conversation, unavailable) = await RecallAsync(reference, newDialog, ct);
            }

            return await assistant.AskAsync(question, length, conversation.Entries, ct);
        }, hint);

        ui.WriteAnswer(answer);

        if (unavailable is not null)
        {
            ui.WriteNote($"(answered without the history: {unavailable})");
        }
        else if (settings.History.Enabled && answer.Length > 0)
        {
            long? parent = conversation.Entries.Count > 0 ? conversation.Entries[^1].Id : null;
            await SaveAsync(question, answer, parent, conversation.DialogMode, ct);
        }
    }

    /// <summary>The conversation to continue. Without an explicit -c, a history that cannot be reached is no
    /// reason to refuse the question: it is answered on its own, and the reason is returned for a note.</summary>
    /// <exception cref="ContextUnavailableException">An explicit -c that cannot be followed.</exception>
    private async Task<(HistoryContextResponse Context, string? Unavailable)> RecallAsync(
        ContextReference? reference,
        bool newDialog,
        CancellationToken ct)
    {
        if (reference is not null)
        {
            activity.Report("Recalling", reference.Description);
        }

        var request = new HistoryContextRequest(TerminalSession.Id, reference?.ToHistory(), newDialog, settings.History.MaxContextTurns);
        try
        {
            var resolved = await agent.ResolveContextAsync(request, ct);
            return (resolved, null);
        }
        catch (AgentException ex) when (reference is not null)
        {
            throw new ContextUnavailableException(ex.Message);
        }
        catch (Exception ex) when (ex is AgentException or AgentUnavailableException or ConfigurationException)
        {
            if (reference is not null)
            {
                throw new ContextUnavailableException($"The history cannot be read: {ex.Message}");
            }

            var standalone = new HistoryContextResponse([], null, false);
            return (standalone, ex.Message);
        }
    }

    /// <summary>A failure here costs only the follow-up, never the answer already on screen: even an agent that
    /// cannot be found at all (a configuration error) ends in a note, not in a failed run. Usually the agent is
    /// up by now and saving takes milliseconds; while it is still starting, a spinner says so.</summary>
    private async Task SaveAsync(string question, string answer, long? parent, bool dialogMode, CancellationToken ct)
    {
        var request = new AppendHistoryRequest(TerminalSession.Id, parent, question, answer);
        try
        {
            var saving = agent.AppendHistoryAsync(request, ct);
            var saved = await ui.WaitWithStatusAsync("Saving to history", saving);
            ui.WriteAnswerId(saved.Id, saved.DialogId, dialogMode);
        }
        catch (Exception ex) when (ex is AgentException or AgentUnavailableException or ConfigurationException)
        {
            ui.WriteNote($"(not saved to the history: {ex.Message})");
        }
    }
}
