namespace ConsoleScout.Configuration;

internal sealed class WebFetchSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Longer pages are cut off; the model is told so.</summary>
    public int MaxChars { get; set; } = 8000;

    /// <summary>The most URLs the model may pass in one web_fetch call. The agent accepts up to 10.</summary>
    public int MaxBatchSize { get; set; } = 5;

    /// <summary>Text budget for one call, shared by its pages: each gets an equal part, capped at
    /// <see cref="MaxChars"/> and never below <see cref="MinPageChars"/>. Keeps a batch from flooding the context.</summary>
    public int MaxBatchChars { get; set; } = 24000;

    public const int MinPageChars = 2000;

    /// <summary>Has the summarizer model condense each page to what bears on the question. Takes effect only
    /// when models:summarizer is configured; otherwise pages reach the main model as raw text.</summary>
    public bool Condense { get; set; } = true;

    /// <summary>Upper bound the summarizer is asked to keep each page's digest under.</summary>
    public int CondensedMaxWords { get; set; } = 250;
}
