using ConsoleScout.Agent;
using ConsoleScout.Configuration;
using ConsoleScout.Contracts;
using ConsoleScout.Output;
using ConsoleScout.Session;

namespace ConsoleScout.App;

/// <summary>-d without a question: turns dialog mode on or off in this terminal, or, with -c, turns it on to
/// continue that conversation. The mode is kept by the agent, next to the history it relies on.</summary>
internal sealed class DialogCommand(AgentClient agent, IConsoleUi ui, AppSettings settings)
{
    public async Task RunAsync(ContextReference? reference, CancellationToken ct)
    {
        var request = new DialogModeRequest(TerminalSession.Id, null, reference?.ToHistory());
        var mode = await SetAsync(request, settings, ct);
        var message = mode.DialogMode ? DialogMessages.On(mode) : DialogMessages.Off;
        ui.WriteInfo(message);
    }

    /// <summary>Shared with -d plus a question.</summary>
    /// <exception cref="ContextUnavailableException">The history is off, the reference matches nothing, or the
    /// agent cannot be reached.</exception>
    public async Task<DialogModeResponse> SetAsync(DialogModeRequest request, AppSettings current, CancellationToken ct)
    {
        if (!current.History.Enabled)
        {
            throw new ContextUnavailableException("Dialog mode needs the history, which is turned off (history.enabled).");
        }

        try
        {
            var changing = agent.SetDialogModeAsync(request, ct);
            return await ui.WaitWithStatusAsync("Starting the agent", changing);
        }
        catch (AgentException ex)
        {
            throw new ContextUnavailableException(ex.Message);
        }
        catch (Exception ex) when (ex is AgentUnavailableException or ConfigurationException)
        {
            throw new ContextUnavailableException($"Dialog mode cannot be changed: {ex.Message}");
        }
    }
}
