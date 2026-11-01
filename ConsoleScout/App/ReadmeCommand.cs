using ConsoleScout.Output;

namespace ConsoleScout.App;

/// <summary>--readme: the whole README, embedded in the executable, rendered for the console.</summary>
internal sealed class ReadmeCommand(IConsoleUi ui)
{
    public void Run()
    {
        var readme = UsageGuide.LoadReadme();
        ui.WriteMarkdown(readme);
    }
}
