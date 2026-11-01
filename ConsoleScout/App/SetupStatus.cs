using System.Text;
using ConsoleScout.Configuration;
using ConsoleScout.Scraper;

namespace ConsoleScout.App;

/// <summary>What --status and the usage guide report about this machine: whether the configuration loads,
/// which model keys are set and whether the scraper is running. Checks only - nothing is started or written.</summary>
/// <param name="CanAnswer">False when a question would fail before reaching the model: a broken configuration
/// or a missing key. The guide then does not ask for one.</param>
internal sealed record SetupStatus(string Markdown, bool CanAnswer)
{
    /// <param name="probeScraper">False skips asking the scraper whether it runs, which can take a moment;
    /// <see cref="CanAnswer"/> does not depend on it.</param>
    public static async Task<SetupStatus> CheckAsync(bool probeScraper = true)
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

        if (probeScraper)
        {
            await AppendScraperAsync(builder, settings);
        }

        if (missing > 0)
        {
            var keys = missing == 1 ? "key" : "keys";
            builder.Append("\nSet the missing ").Append(keys).Append(", then run `csc` again to ask a question.\n");
        }

        return new SetupStatus(builder.ToString(), CanAnswer: missing == 0);
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

    private static async Task AppendScraperAsync(StringBuilder builder, AppSettings settings)
    {
        if (!settings.UsesScraper())
        {
            builder.Append("- ○ **Scraper**: not needed, the web tools are turned off.\n");
            return;
        }

        using var launcher = new ScraperLauncher(settings.Scraper);
        var running = await launcher.FindRunningAsync();
        if (running is not null)
        {
            builder.Append("- ✓ **Scraper** is running at ").Append(running.Url).Append(" (pid ").Append(running.Pid).Append(").\n");
            return;
        }

        var hint = settings.Scraper.AutoStart
            ? "it starts by itself on the first question that needs the web, or now with `csc -r`"
            : "start it with `csc -r` (Scraper:AutoStart is off)";
        builder.Append("- ○ **Scraper** is not running: ").Append(hint).Append(".\n");
    }

    private static string Title(string model) => char.ToUpperInvariant(model[0]) + model[1..];
}
