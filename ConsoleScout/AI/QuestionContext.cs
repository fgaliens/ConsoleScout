namespace ConsoleScout.AI;

/// <summary>The question being answered, for components that need it but are not handed it directly,
/// such as the summarizer that condenses fetched pages. Set once per run, before the assistant starts.</summary>
internal sealed class QuestionContext
{
    public string Question { get; set; } = string.Empty;
}
