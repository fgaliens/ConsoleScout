using ConsoleScout.Donate;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ConsoleScout.Output;

/// <summary>The Spectre.Console implementation of <see cref="IConsoleUi"/>. It also reports tool
/// activity, by rewriting the line next to the spinner that is currently running.</summary>
internal sealed class ConsoleUi(IAnswerWriter answers, WalletWriter wallets, MarkdownRenderer markdown)
    : IConsoleUi, IActivityReporter
{
    private static readonly Spinner Spinner = Spinner.Known.Star;

    /// <summary>Secondary text: the theme's own foreground, dimmed. A fixed colour such as grey is close to
    /// the background in many dark themes; dim stays readable on dark and light ones alike.</summary>
    private const string Secondary = "dim";

    // Written by Report from tool code, read by the animation loop; a reference swap needs no lock.
    private volatile string? _statusLine;

    public bool CanAsk => AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;

    public string AskQuestion()
    {
        EnsureInteractive("Cannot ask for a question: the terminal is not interactive. Pass it as arguments instead.");

        return AnsiConsole.Prompt(
            new TextPrompt<string>("What is your question?")
                .DefaultValue("What is the weather for today?")
                .ShowDefaultValue());
    }

    public string AskSecret(string prompt)
    {
        EnsureInteractive($"Cannot ask for the {prompt.TrimEnd(':', ' ')}: the terminal is not interactive.");

        var escaped = Markup.Escape(prompt);
        var secret = new TextPrompt<string>(escaped).Secret();
        return AnsiConsole.Prompt(secret);
    }

    /// <remarks>A live display rather than Spectre's Status, which renders a single line only: the hint
    /// needs a line of its own below the activity.</remarks>
    public async Task<T> RunWithStatusAsync<T>(string status, Func<Task<T>> work, string? hint = null)
    {
        _statusLine = Line(status, null);
        try
        {
            if (!AnsiConsole.Profile.Capabilities.Interactive)
            {
                return await work();
            }

            var initial = Frame(0, hint);
            return await AnsiConsole.Live(initial)
                .AutoClear(true)
                .StartAsync(async context =>
                {
                    var running = work();
                    for (var frame = 1; !running.IsCompleted; frame++)
                    {
                        var delay = Task.Delay(Spinner.Interval);
                        await Task.WhenAny(running, delay);

                        var next = Frame(frame, hint);
                        context.UpdateTarget(next);
                    }

                    return await running;
                });
        }
        finally
        {
            _statusLine = null;
        }
    }

    public void Report(string activity, string detail)
    {
        if (_statusLine is not null)
        {
            _statusLine = Line(activity, detail);
        }
    }

    public void WriteAnswer(string answer) => answers.Write(answer);

    public void WriteWalletList(IReadOnlyList<Wallet> list) => wallets.WriteList(list);

    public void WriteWallet(Wallet wallet) => wallets.WriteDetails(wallet);

    public void WriteMarkdown(string document)
    {
        var rendered = markdown.Render(document);
        AnsiConsole.Write(rendered);
    }

    public void WriteInfo(string message) => AnsiConsole.WriteLine(message);

    public void WriteAnswerId(long id)
    {
        AnsiConsole.MarkupLine($"[bold]x{id}[/] [{Secondary}]· csc -c … follows up here, csc -c x{id} … from any terminal[/]");
    }

    public void WriteNote(string message)
    {
        var escaped = Markup.Escape(message);
        AnsiConsole.MarkupLine($"[{Secondary}]{escaped}[/]");
    }

    public void WriteError(string message) => ErrorConsole.Write(message);

    private void EnsureInteractive(string message)
    {
        if (!CanAsk)
        {
            throw new InputUnavailableException(message);
        }
    }

    /// <summary>One animation frame: the spinner with the current activity, and the hint dimmed below it.</summary>
    private IRenderable Frame(int frame, string? hint)
    {
        var symbol = Spinner.Frames[frame % Spinner.Frames.Count];
        var status = new Markup($"[yellow]{Markup.Escape(symbol)}[/] {_statusLine}");
        if (hint is null)
        {
            return status;
        }

        var dimmed = new Markup($"  [{Secondary}]{Markup.Escape(hint)}[/]");
        return new Rows(status, dimmed);
    }

    private static string Line(string activity, string? detail) =>
        detail is null
            ? $"[green]{Markup.Escape(activity)}[/]..."
            : $"[green]{Markup.Escape(activity)}[/]: {Markup.Escape(detail)}";
}
