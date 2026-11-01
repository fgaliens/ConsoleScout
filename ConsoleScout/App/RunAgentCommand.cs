using ConsoleScout.Output;
using ConsoleScout.Agent;

namespace ConsoleScout.App;

/// <summary>--run-agent: starts the agent (or finds the one running) and waits until its browser is up,
/// so the next questions start fast. On a new machine this is also the setup step: it is when Chromium
/// gets downloaded if no installed browser can be used.</summary>
internal sealed class RunAgentCommand(AgentLauncher launcher, AgentClient agent, IConsoleUi ui)
{
    /// <summary>Longer than for a question: a first launch can be slow while the OS checks the freshly
    /// installed files (macOS scans every library of a new, unsigned bundle).</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    public async Task RunAsync(CancellationToken ct)
    {
        var endpoint = await ui.RunWithStatusAsync("Starting the agent", async () =>
        {
            var endpoint = await launcher.GetEndpointAsync(StartupTimeout);
            await agent.WarmUpAsync(ct);
            return endpoint;
        });

        ui.WriteInfo($"The agent is running at {endpoint.Url} (pid {endpoint.Pid}), and its browser is ready.");
        ui.WriteInfo("It stops by itself after a while without requests.");
    }
}
