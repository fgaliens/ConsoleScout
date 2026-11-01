namespace ConsoleScout.Configuration;

/// <summary>Every tool can be turned off; a disabled tool is neither offered to the model nor mentioned
/// in its prompt. With all of them off the agent is never started.</summary>
internal sealed class ToolsSettings
{
    public WebSearchSettings WebSearch { get; set; } = new();

    public WebFetchSettings WebFetch { get; set; } = new();
}
