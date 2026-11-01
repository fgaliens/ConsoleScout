using ConsoleScout.Donate;

namespace ConsoleScout.Output;

/// <summary>Everything the application shows or asks, so nothing outside Output touches the console.</summary>
internal interface IConsoleUi
{
    /// <summary>False when input is redirected or the terminal is not interactive; asking would then fail.</summary>
    bool CanAsk { get; }

    string AskQuestion();

    /// <summary>Asks for a value without echoing it, e.g. an API key.</summary>
    string AskSecret(string prompt);

    /// <summary>Shows <paramref name="status"/> with a spinner while <paramref name="work"/> runs; tools
    /// update it through <see cref="IActivityReporter"/> in the meantime. A <paramref name="hint"/> is shown
    /// dimmed on the line below for as long as the spinner runs, and disappears with it.</summary>
    Task<T> RunWithStatusAsync<T>(string status, Func<Task<T>> work, string? hint = null);

    /// <summary>Waits for <paramref name="work"/>, showing <paramref name="status"/> with a spinner only once it has
    /// taken longer than a moment: a fast call shows nothing, so the spinner does not flash.</summary>
    Task<T> WaitWithStatusAsync<T>(string status, Task<T> work);

    void WriteAnswer(string answer);

    /// <summary>A Markdown document rendered for the console, e.g. the usage guide.</summary>
    void WriteMarkdown(string markdown);

    /// <summary>The donation networks with their codes; a notice when there are none.</summary>
    void WriteWalletList(IReadOnlyList<Wallet> wallets);

    /// <summary>One donation wallet: address, QR code and network warning.</summary>
    void WriteWallet(Wallet wallet);

    /// <summary>The numbers of a saved answer and its dialog under it, with how to go on: how to follow up, or,
    /// in <paramref name="dialogMode"/>, that the mode is on and how to start a new dialog or turn it off.</summary>
    void WriteAnswerId(long id, long dialogId, bool dialogMode);

    /// <summary>A dimmed line of secondary information.</summary>
    void WriteNote(string message);

    /// <summary>A plain status line, e.g. confirming that a key was saved.</summary>
    void WriteInfo(string message);

    void WriteError(string message);
}
