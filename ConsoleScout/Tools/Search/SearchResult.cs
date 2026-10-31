namespace ConsoleScout.AssistantTools.Search;

/// <summary>One hit: the link and the text extracted from it (title plus content).</summary>
internal sealed record SearchResult(string Url, string Text);
