using ConsoleScout.Output;

namespace ConsoleScout.App;

/// <summary>--status: the configuration, the model keys and the scraper, each with what to do about it.</summary>
internal sealed class StatusCommand(IConsoleUi ui)
{
    /// <returns>The exit code: 1 when a question could not be answered as things stand.</returns>
    public async Task<int> RunAsync()
    {
        var status = await SetupStatus.CheckAsync();
        ui.WriteMarkdown(status.Markdown);
        return status.CanAnswer ? 0 : 1;
    }
}
