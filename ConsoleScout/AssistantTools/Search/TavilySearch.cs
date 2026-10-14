using System.Buffers;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ConsoleScout.AssistantTools.Search;

/// <summary>Tavily-backed web search: a single POST, results yielded as the response is parsed.</summary>
internal sealed class TavilySearch(string apiKey, int maxResults = 5, TimeSpan? timeout = null)
    : IWebSearch, IDisposable
{
    private const string Endpoint = "https://api.tavily.com/search";

    private readonly HttpClient _http = new() { Timeout = timeout ?? TimeSpan.FromSeconds(30) };

    public async IAsyncEnumerable<SearchResult> SearchAsync(
        string query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string payload;

        using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new ByteArrayContent(BuildBody(query));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new SearchException(Describe((int)response.StatusCode));

            payload = await response.Content.ReadAsStringAsync(ct);
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var result in results.EnumerateArray())
        {
            var url = Text(result, "url");
            if (url.Length == 0)
                continue;

            yield return new SearchResult(url, Compose(result));
        }
    }

    public void Dispose() => _http.Dispose();

    private byte[] BuildBody(string query)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("query", query);
            writer.WriteNumber("max_results", maxResults);
            writer.WriteString("search_depth", "basic");
            writer.WriteString("topic", "general");
            writer.WriteBoolean("include_answer", false);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>SearchResult has no title field, so the title becomes the first line of the text.</summary>
    private static string Compose(JsonElement result)
    {
        var title = Text(result, "title");
        var content = Text(result, "content");

        return title.Length == 0 ? content : $"{title}\n{content}";
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string Describe(int status) => status switch
    {
        401 or 403 => "the Tavily API key is missing or invalid",
        429 => "the Tavily rate limit was hit",
        432 or 433 => "the Tavily free quota is exhausted",
        _ => $"Tavily returned HTTP {status}",
    };
}
