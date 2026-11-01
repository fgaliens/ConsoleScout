using AngleSharp.Html.Parser;
using ConsoleScout.Contracts;
using ConsoleScout.Scraper.Fetching;
using Microsoft.Extensions.Options;

namespace ConsoleScout.Scraper.Search;

/// <summary>DuckDuckGo's script-free HTML endpoint, over plain HTTP: it serves results to a plain client
/// but challenges a headless browser. Links point at a /l/?uddg= redirect, which is unwrapped so the model
/// cites the real page. Ads are skipped.</summary>
internal sealed class DuckDuckGoEngine(IHttpClientFactory httpClients, IOptions<ScraperSettings> options) : ISearchEngine
{
    private static readonly Uri Base = new("https://html.duckduckgo.com/html/");

    private static readonly HtmlParser Parser = new();

    public string Name => "duckduckgo";

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct)
    {
        var http = httpClients.CreateClient(PageFetcher.HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Base, $"?q={Uri.EscapeDataString(query)}"));
        request.Headers.UserAgent.ParseAdd(options.Value.Fetch.UserAgent);

        string html;
        try
        {
            using var response = await http.SendAsync(request, ct);

            // 202 is how DuckDuckGo serves its bot challenge.
            if ((int)response.StatusCode is 202 or 403 or 429)
            {
                throw Challenge();
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ScraperFailure(ScraperErrorCodes.FetchFailed, $"DuckDuckGo returned HTTP {(int)response.StatusCode}.");
            }

            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ScraperFailure(ScraperErrorCodes.Timeout, "DuckDuckGo did not respond in time.");
        }
        catch (HttpRequestException ex)
        {
            throw new ScraperFailure(ScraperErrorCodes.FetchFailed, $"Cannot reach DuckDuckGo: {ex.Message}");
        }

        using var document = await Parser.ParseDocumentAsync(html, ct);
        if (document.QuerySelector("#challenge-form, .anomaly-modal__modal") is not null)
        {
            throw Challenge();
        }

        List<SearchHit> hits = [];
        foreach (var result in document.QuerySelectorAll(".result:not(.result--ad)"))
        {
            var link = result.QuerySelector("a.result__a");
            var href = link?.GetAttribute("href");
            var url = Unwrap(href);
            if (link is null || url is null)
            {
                continue;
            }

            var snippet = result.QuerySelector(".result__snippet")?.TextContent ?? string.Empty;
            var title = TextExtractor.Clean(link.TextContent);
            var description = TextExtractor.Clean(snippet);
            hits.Add(new SearchHit(url, title, description));

            if (hits.Count >= maxResults)
            {
                break;
            }
        }

        return hits;
    }

    private static string? Unwrap(string? href)
    {
        if (string.IsNullOrEmpty(href) || !Uri.TryCreate(Base, href, out var uri))
        {
            return null;
        }

        if (uri.AbsolutePath == "/l/")
        {
            var target = System.Web.HttpUtility.ParseQueryString(uri.Query)["uddg"];
            if (target is null || !Uri.TryCreate(target, UriKind.Absolute, out uri))
            {
                return null;
            }
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : null;
    }

    private static ScraperFailure Challenge() =>
        new(ScraperErrorCodes.Captcha, "DuckDuckGo answered with a bot check. Try again later or use another engine.");
}
