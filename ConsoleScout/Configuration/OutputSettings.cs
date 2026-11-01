namespace ConsoleScout.Configuration;

internal sealed class OutputSettings
{
    /// <summary>Picks the answer writer, and is also told to the model so that what it writes
    /// matches what the console can display.</summary>
    public AnswerFormat Format { get; set; } = AnswerFormat.Markdown;
}
