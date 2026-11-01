using System.Text;
using System.Text.Json;
using ConsoleScout.Configuration;
using ConsoleScout.Contracts;
using ConsoleScout.Output;
using ConsoleScout.Agent;

namespace ConsoleScout.Tools.WebSearch;

/// <summary>web_search: asks the configured providers in order and renders the hits with citation numbers.
/// A provider that is blocked (a CAPTCHA, say) is skipped for the rest of the run, and the next one answers.</summary>
internal sealed class WebSearchTool(
    IReadOnlyList<ISearchProvider> providers,
    WebSearchSettings settings,
    IActivityReporter activity) : IAssistantTool
{
    private const int MaxSnippetChars = 600;

    private readonly HashSet<string> _blocked = [];

    public string Name => "web_search";

    public string Description =>
        "Search the web for current or factual information you do not reliably know: "
        + "recent events, prices, release versions, documentation, statistics. Returns titles, links and short snippets.";

    public BinaryData ParametersSchema { get; } = BinaryData.FromString(
        """
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
        """);

    public string PromptGuidance =>
        """
        You have a web_search tool, and your training data is frozen in the past.
        Call web_search whenever the answer depends on something you cannot verify from memory:
        news, weather, prices, rates, schedules, statistics, release versions and dates,
        and anything phrased as latest / newest / current / today.
        "The most recent one I know of is X" is never an acceptable answer - search instead.
        Answer directly, without searching, only for general knowledge, reasoning, writing and coding questions.
        """;

    public async Task<string> InvokeAsync(JsonElement arguments, CancellationToken ct)
    {
        var query = ToolArguments.RequireString(arguments, "query");
        activity.Report("Searching", query);

        List<string> failures = [];
        foreach (var provider in providers)
        {
            if (_blocked.Contains(provider.Name))
            {
                continue;
            }

            try
            {
                var hits = await provider.SearchAsync(query, settings.MaxResults, ct);
                return Render(hits);
            }
            catch (AgentException ex)
            {
                if (ex.IsBlocked)
                {
                    _blocked.Add(provider.Name);
                }

                failures.Add($"{provider.Name}: {ex.Message}");
            }
            catch (AgentUnavailableException ex)
            {
                // Every provider goes through the same agent, so there is nobody else to ask.
                throw new ToolException(ex.Message);
            }
        }

        throw new ToolException(
            $"no search engine answered ({string.Join("; ", failures)}). "
            + "Answer from your own knowledge and warn the user that the information may be outdated");
    }

    private string Render(IReadOnlyList<SearchHit> hits)
    {
        if (hits.Count == 0)
        {
            return "No search results found.";
        }

        var builder = new StringBuilder();
        foreach (var hit in hits)
        {
            builder.Append("URL: ").AppendLine(hit.Url);
            if (hit.Title.Length > 0)
            {
                builder.AppendLine(hit.Title);
            }

            var snippet = Truncate(hit.Snippet);
            builder.AppendLine(snippet);
            builder.AppendLine();
        }

        builder.Append("Snippets are short: call web_fetch with the URLs of the results whose details you need, "
            + "several at once when more than one matters.");
        return builder.ToString();
    }

    private static string Truncate(string value)
    {
        if (value.Length <= MaxSnippetChars)
        {
            return value;
        }

        var head = value.AsSpan(0, MaxSnippetChars);
        return string.Concat(head, "...");
    }
}
