using System.Text;
using ConsoleScout.Configuration;
using ConsoleScout.Contracts;
using ConsoleScout.Agent;
using ConsoleScout.Session;

namespace ConsoleScout.App;

/// <summary>What --status and the usage guide report about this machine: whether the configuration loads,
/// which model keys are set and whether the agent is running. Checks only - nothing is started or written.</summary>
/// <param name="CanAnswer">False when a question would fail before reaching the model: a broken configuration
/// or a missing key. The guide then does not ask for one.</param>
internal sealed record SetupStatus(string Markdown, bool CanAnswer)
{
    /// <param name="probeAgent">False skips asking the agent whether it runs, which can take a moment;
    /// <see cref="CanAnswer"/> does not depend on it.</param>
    public static async Task<SetupStatus> CheckAsync(bool probeAgent = true)
    {
        var builder = new StringBuilder("## Status\n\n");

        AppSettings settings;
        try
        {
            var configuration = ConfigurationLoader.Build();
            settings = ConfigurationLoader.Bind(configuration);
        }
        catch (ConfigurationException ex)
        {
            builder.Append("- ✗ **Configuration** cannot be loaded:\n\n```\n").Append(ex.Message).Append("\n```\n");
            return new SetupStatus(builder.ToString(), CanAnswer: false);
        }

        AppendUserFiles(builder);

        var missing = 0;
        if (!AppendKey(builder, "main", settings.Models.Main.ApiKey))
        {
            missing++;
        }

        // Without a summarizer section the model is simply not used; with one, its key is required to start.
        var summarizerUsed = settings.Models.Summarizer is not null
            && settings.Tools.WebFetch.Enabled
            && settings.Tools.WebFetch.Condense;
        if (summarizerUsed)
        {
            var summarizerKey = settings.Models.Summarizer?.ApiKey;
            if (!AppendKey(builder, "summarizer", summarizerKey))
            {
                missing++;
            }
        }
        else if (settings.Models.Summarizer is null)
        {
            builder.Append("- ○ **Summarizer model**: not configured; fetched pages reach the main model as they are.\n");
        }
        else
        {
            builder.Append("- ○ **Summarizer model**: configured but not used, page condensing is off.\n");
        }

        if (probeAgent)
        {
            await AppendAgentAsync(builder, settings);
        }

        AppendSession(builder, settings);

        if (missing > 0)
        {
            var keys = missing == 1 ? "key" : "keys";
            builder.Append("\nSet the missing ").Append(keys).Append(", then run `csc` again to ask a question.\n");
        }

        return new SetupStatus(builder.ToString(), CanAnswer: missing == 0);
    }

    /// <summary>Which of the user's own files are in use; they override the shipped appsettings.json.</summary>
    private static void AppendUserFiles(StringBuilder builder)
    {
        string[] paths = [ConfigurationLoader.UserSettingsPath, ConfigurationLoader.PreferencesPath, ConfigurationLoader.SecretsPath];
        var found = paths.Where(File.Exists).Select(Path.GetFileName).ToList();
        var list = found.Count == 0 ? "none yet" : string.Join(", ", found);
        var directory = Path.GetDirectoryName(ConfigurationLoader.UserSettingsPath);
        builder.Append("- ○ **User files** in ").Append(directory).Append(": ").Append(list).Append(".\n");
    }

    /// <summary>Asked only of a running agent: --status never starts one.</summary>
    private static async Task AppendDialogModeAsync(StringBuilder builder, AgentLauncher launcher, AppSettings settings)
    {
        if (!settings.History.Enabled)
        {
            return;
        }

        using var client = new AgentClient(launcher, settings.Agent);
        var request = new HistoryContextRequest(TerminalSession.Id, null, false, 1);
        try
        {
            var context = await client.ResolveContextAsync(request, CancellationToken.None);
            var state = context.DialogMode
                ? "on in this terminal (`csc -d` turns it off)"
                : "off in this terminal (`csc -d` turns it on)";
            builder.Append("- ○ **Dialog mode** is ").Append(state).Append(".\n");
        }
        catch (Exception ex) when (ex is AgentException or AgentUnavailableException)
        {
            builder.Append("- ○ **Dialog mode** is unknown: ").Append(ex.Message).Append('\n');
        }
    }

    private static void AppendSession(StringBuilder builder, AppSettings settings)
    {
        if (!settings.History.Enabled)
        {
            builder.Append("- ○ **History** is turned off (history.enabled); -c is not available.\n");
            return;
        }

        if (TerminalSession.IsShared)
        {
            builder.Append("- ○ **Terminal session** cannot be told apart here, so all terminals share one history for -c. ")
                .Append("Set ").Append(TerminalSession.Variable).Append(" per terminal to separate them.\n");
            return;
        }

        builder.Append("- ✓ **Terminal session** ").Append(TerminalSession.Id).Append(": -c follows up on questions asked in it.\n");
    }

    /// <returns>Whether the key is set.</returns>
    private static bool AppendKey(StringBuilder builder, string model, string? key)
    {
        var title = Title(model);
        if (!string.IsNullOrWhiteSpace(key))
        {
            builder.Append("- ✓ **").Append(title).Append(" model key** is set.\n");
            return true;
        }

        builder.Append("- ✗ **").Append(title).Append(" model key** is missing: run `csc --set-key ")
            .Append(model).Append("`.\n");
        return false;
    }

    private static async Task AppendAgentAsync(StringBuilder builder, AppSettings settings)
    {
        if (!settings.UsesAgent())
        {
            builder.Append("- ○ **Agent**: not needed, the web tools and the history are turned off.\n");
            return;
        }

        using var launcher = new AgentLauncher(settings.Agent);
        var running = await launcher.FindRunningAsync();
        if (running is not null)
        {
            builder.Append("- ✓ **Agent** is running at ").Append(running.Url).Append(" (pid ").Append(running.Pid).Append(").\n");
            await AppendDialogModeAsync(builder, launcher, settings);
            return;
        }

        var hint = settings.Agent.AutoStart
            ? "it starts by itself on the first question that needs the web, or now with `csc -r`"
            : "start it with `csc -r` (Agent:AutoStart is off)";
        builder.Append("- ○ **Agent** is not running: ").Append(hint).Append(".\n");
    }

    private static string Title(string model) => char.ToUpperInvariant(model[0]) + model[1..];
}
