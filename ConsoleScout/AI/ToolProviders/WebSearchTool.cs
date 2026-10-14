using System.Text;
using System.Text.Json;
using ConsoleScout.AssistantTools.Search;
using OpenAI.Chat;

namespace ConsoleScout.AI.ToolProviders;

/// <summary>Bridges the model's tool call to IWebSearch: parses arguments, numbers sources and
/// renders the text the model sees. Keeping it here leaves Search free of any OpenAI SDK reference.</summary>
internal sealed class WebSearchTool(IWebSearch search, Action<string>? onSearching = null)
{
    public const string Name = "web_search";

    private const int MaxContentChars = 1200;

    private readonly List<string> _sources = [];

    /// <summary>Links from every search performed, in order of first appearance. Index + 1 is the citation number.</summary>
    public IReadOnlyList<string> Sources => _sources;

    public static ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: Name,
        functionDescription: "Search the web for current or factual information you do not reliably know: "
            + "recent events, prices, release versions, documentation, statistics.",
        functionParameters: BinaryData.FromString("""
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "Search query, 2-8 keywords, in the language most likely used by relevant sources"
            }
          },
          "required": ["query"]
        }
        """));

    public async Task<string> InvokeAsync(ChatToolCall call, CancellationToken ct = default)
    {
        if (call.FunctionName != Name)
            return $"Error: unknown tool '{call.FunctionName}'.";

        string? query;
        try
        {
            using var arguments = JsonDocument.Parse(call.FunctionArguments);
            query = arguments.RootElement.TryGetProperty("query", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return """Error: arguments were not valid JSON. Retry with {"query": "..."}.""";
        }

        if (string.IsNullOrWhiteSpace(query))
            return """Error: missing required argument 'query'. Retry with {"query": "..."}.""";

        onSearching?.Invoke(query);

        // Buffered before rendering: the model needs one finished string, and a mid-stream failure
        // must not leave Sources holding links from a half-processed response.
        List<SearchResult> results = [];
        try
        {
            await foreach (var result in search.SearchAsync(query, ct))
                results.Add(result);
        }
        catch (SearchException ex)
        {
            return Failure(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return Failure(ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failure("the search timed out");
        }
        catch (JsonException)
        {
            return Failure("the search backend returned malformed JSON");
        }

        return Render(results);
    }

    private string Render(List<SearchResult> results)
    {
        var builder = new StringBuilder();
        foreach (var result in results)
        {
            if (!_sources.Contains(result.Url))
                _sources.Add(result.Url);

            builder.Append('[').Append(_sources.IndexOf(result.Url) + 1).Append("] ").AppendLine(result.Url);
            builder.AppendLine(Truncate(result.Text));
            builder.AppendLine();
        }

        return builder.Length == 0 ? "No search results found." : builder.ToString();
    }

    private static string Truncate(string value) =>
        value.Length <= MaxContentChars ? value : string.Concat(value.AsSpan(0, MaxContentChars), "...");

    private static string Failure(string reason) =>
        $"Search failed: {reason}. Answer from your own knowledge and warn the user that the information may be outdated.";
}
