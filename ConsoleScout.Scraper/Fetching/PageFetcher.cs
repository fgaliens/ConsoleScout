using System.Net;
using System.Text.Json;
using ConsoleScout.Contracts;
using ConsoleScout.Scraper.Browser;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace ConsoleScout.Scraper.Fetching;

/// <summary>Loads a page as text, over plain HTTP or in the browser. Auto mode starts with HTTP, which is
/// an order of magnitude cheaper, and falls back to the browser only when the result looks unusable.</summary>
internal sealed class PageFetcher(
    IHttpClientFactory httpClients,
    BrowserHost browser,
    NavigationGuard guard,
    IOptions<ScraperSettings> options,
    ILogger<PageFetcher> logger)
{
    public const string HttpClientName = "fetch";

    private const int MaxBodyBytes = 5 * 1024 * 1024;

    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(3);

    private readonly FetchSettings _settings = options.Value.Fetch;

    public async Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken ct)
    {
        var uri = await guard.ValidateAsync(request.Url, ct);
        var maxChars = Math.Clamp(request.MaxChars, 500, 100_000);

        Page page;
        switch (request.Mode)
        {
            case FetchMode.Http:
                page = await ViaHttpAsync(uri, ct);
                EnsureSuccess(page);
                break;

            case FetchMode.Browser:
                page = await ViaBrowserAsync(uri, ct);
                break;

            default:
                page = await ViaHttpAsync(uri, ct);
                if (NeedsBrowser(page))
                {
                    logger.LogInformation("Falling back to the browser for {Url} (HTTP {Status}, {Chars} chars)",
                        uri, page.Status, page.Text.Length);
                    page = await ViaBrowserAsync(uri, ct);
                }
                else
                {
                    EnsureSuccess(page);
                }
                break;
        }

        var truncated = page.Text.Length > maxChars;
        var text = truncated ? page.Text[..maxChars] : page.Text;

        return new FetchResponse(request.Url, page.FinalUrl, page.Status, page.Title, text, truncated, page.Rendered);
    }

    /// <summary>Loads every page in parallel (the browser's own page limit still applies). A page that
    /// fails becomes an error item instead of failing the batch; the order matches the request.</summary>
    public async Task<FetchBatchResponse> FetchBatchAsync(FetchBatchRequest request, CancellationToken ct)
    {
        if (request.Pages is not { Count: > 0 })
        {
            throw new ScraperFailure(ScraperErrorCodes.BadRequest, "The batch has no pages.");
        }

        if (request.Pages.Count > FetchBatchRequest.MaxPages)
        {
            throw new ScraperFailure(ScraperErrorCodes.BadRequest,
                $"A batch takes at most {FetchBatchRequest.MaxPages} pages, not {request.Pages.Count}.");
        }

        var fetches = request.Pages.Select(page => FetchItemAsync(page, ct)).ToArray();
        var items = await Task.WhenAll(fetches);
        return new FetchBatchResponse(items);
    }

    private async Task<FetchBatchItem> FetchItemAsync(FetchRequest request, CancellationToken ct)
    {
        try
        {
            var page = await FetchAsync(request, ct);
            return new FetchBatchItem(request.Url, page, null);
        }
        catch (ScraperFailure failure)
        {
            var error = new ScraperError(failure.Code, failure.Message);
            return new FetchBatchItem(request.Url, null, error);
        }
    }

    private bool NeedsBrowser(Page page) =>
        page.Status is 401 or 403 or 429 or 503
        || (page.HasScripts && page.Status < 400 && page.Text.Length < _settings.MinHttpTextChars);

    private static void EnsureSuccess(Page page)
    {
        if (page.Status >= 400)
        {
            throw new ScraperFailure(ScraperErrorCodes.FetchFailed, $"{page.FinalUrl} returned HTTP {page.Status}.");
        }
    }

    private async Task<Page> ViaHttpAsync(Uri uri, CancellationToken ct)
    {
        var http = httpClients.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(_settings.UserAgent);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,text/plain;q=0.9,*/*;q=0.5");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ScraperFailure(ScraperErrorCodes.Timeout, $"{uri} did not respond in time.");
        }
        catch (HttpRequestException ex)
        {
            throw new ScraperFailure(ScraperErrorCodes.FetchFailed, $"Cannot load {uri}: {ex.Message}");
        }

        using (response)
        {
            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? uri.ToString();
            var status = (int)response.StatusCode;
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "text/html";

            if (!IsText(mediaType))
            {
                throw new ScraperFailure(ScraperErrorCodes.FetchFailed, $"{finalUrl} is {mediaType}, not a text page.");
            }

            var body = await ReadLimitedAsync(response.Content, ct);

            if (!IsHtml(mediaType))
            {
                var plain = TextExtractor.Clean(body);
                return new Page(finalUrl, status, string.Empty, plain, HasScripts: false, Rendered: false);
            }

            // A short page without scripts is simply short; only scripts can be hiding the rest of it.
            var hasScripts = body.Contains("<script", StringComparison.OrdinalIgnoreCase);
            var (title, text) = TextExtractor.FromHtml(body);
            return new Page(finalUrl, status, title, text, hasScripts, Rendered: false);
        }
    }

    private Task<Page> ViaBrowserAsync(Uri uri, CancellationToken ct) =>
        browser.WithPageAsync(async tab =>
        {
            var address = uri.ToString();
            var response = await tab.GotoAsync(address, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            // Give scripts a moment to render content; a page that never goes quiet is read as it is.
            try
            {
                await tab.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = (float)SettleTime.TotalMilliseconds });
            }
            catch (TimeoutException)
            {
            }

            var extracted = await tab.EvaluateAsync<JsonElement>(TextExtractor.BrowserScript);
            var title = extracted.GetProperty("title").GetString()?.Trim() ?? string.Empty;
            var rawText = extracted.GetProperty("text").GetString() ?? string.Empty;
            var text = TextExtractor.Clean(rawText);
            var status = response?.Status ?? 200;

            if (status >= 400 && text.Length == 0)
            {
                throw new ScraperFailure(ScraperErrorCodes.FetchFailed, $"{tab.Url} returned HTTP {status}.");
            }

            return new Page(tab.Url, status, title, text, HasScripts: true, Rendered: true);
        }, ct);

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();

        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= MaxBodyBytes)
            {
                break;
            }
        }

        var charset = content.Headers.ContentType?.CharSet;
        var encoding = System.Text.Encoding.UTF8;
        if (charset is not null)
        {
            try
            {
                var name = charset.Trim('"');
                encoding = System.Text.Encoding.GetEncoding(name);
            }
            catch (ArgumentException)
            {
            }
        }

        var bytes = buffer.GetBuffer();
        return encoding.GetString(bytes, 0, (int)buffer.Length);
    }

    private static bool IsHtml(string mediaType) =>
        mediaType is "text/html" or "application/xhtml+xml";

    private static bool IsText(string mediaType) =>
        mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || mediaType is "application/xhtml+xml" or "application/json" or "application/xml" or "application/rss+xml" or "application/atom+xml";

    public static SocketsHttpHandler CreateHandler(NavigationGuard guard) => new()
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        ConnectCallback = guard.ConnectAsync,
    };

    private sealed record Page(string FinalUrl, int Status, string Title, string Text, bool HasScripts, bool Rendered);
}
