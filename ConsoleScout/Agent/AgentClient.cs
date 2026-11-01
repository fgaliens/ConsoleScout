using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ConsoleScout.Configuration;
using ConsoleScout.Contracts;

namespace ConsoleScout.Agent;

/// <summary>Typed client for the agent's HTTP API. Serialization goes through the source-generated
/// ContractsJsonContext, which keeps the CLI AOT-safe and in step with the service.</summary>
internal sealed class AgentClient(AgentLauncher launcher, AgentSettings settings) : IDisposable
{
    /// <summary>Warm-up may include downloading Chromium, which takes far longer than any page.</summary>
    private static readonly TimeSpan WarmUpTimeout = TimeSpan.FromMinutes(10);

    // Timeouts are applied per request instead, since they differ between requests.
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private TimeSpan RequestTimeout => TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);

    public Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken ct)
    {
        var content = Json(request, ContractsJsonContext.Default.FetchRequest);
        return SendAsync("/fetch", content, ContractsJsonContext.Default.FetchResponse, RequestTimeout, ct);
    }

    /// <summary>Several pages in one request; each item carries its page or its own error.</summary>
    public Task<FetchBatchResponse> FetchBatchAsync(FetchBatchRequest request, CancellationToken ct)
    {
        var content = Json(request, ContractsJsonContext.Default.FetchBatchRequest);
        return SendAsync("/fetch/batch", content, ContractsJsonContext.Default.FetchBatchResponse, RequestTimeout, ct);
    }

    /// <returns>The id of the saved entry.</returns>
    public async Task<long> AppendHistoryAsync(AppendHistoryRequest request, CancellationToken ct)
    {
        var content = Json(request, ContractsJsonContext.Default.AppendHistoryRequest);
        var response = await SendAsync("/history", content, ContractsJsonContext.Default.AppendHistoryResponse, RequestTimeout, ct);
        return response.Id;
    }

    /// <summary>The referenced entry and the ones it followed up on, oldest first.</summary>
    /// <exception cref="AgentException">With code not_found when there is no such entry.</exception>
    public async Task<IReadOnlyList<HistoryEntry>> GetHistoryThreadAsync(HistoryThreadRequest request, CancellationToken ct)
    {
        var content = Json(request, ContractsJsonContext.Default.HistoryThreadRequest);
        var response = await SendAsync("/history/thread", content, ContractsJsonContext.Default.HistoryThreadResponse, RequestTimeout, ct);
        return response.Entries;
    }

    public Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var content = Json(request, ContractsJsonContext.Default.SearchRequest);
        return SendAsync("/search", content, ContractsJsonContext.Default.SearchResponse, RequestTimeout, ct);
    }

    /// <summary>Returns once the agent's browser is up, installing Chromium first if it has to.</summary>
    public Task WarmUpAsync(CancellationToken ct) =>
        SendAsync<object>("/warmup", content: null, responseType: null, WarmUpTimeout, ct);

    public void Dispose() => _http.Dispose();

    private static Func<HttpContent> Json<T>(T body, JsonTypeInfo<T> type) => () => JsonContent.Create(body, type);

    /// <param name="content">Creates the body anew for each attempt; null sends none.</param>
    /// <param name="responseType">Null for endpoints that answer with an empty 200.</param>
    private async Task<TResponse> SendAsync<TResponse>(
        string path,
        Func<HttpContent>? content,
        JsonTypeInfo<TResponse>? responseType,
        TimeSpan timeout,
        CancellationToken ct)
    {
        // Two attempts: the agent may have stopped for idleness since it was located.
        for (var attempt = 1; ; attempt++)
        {
            var endpoint = await launcher.GetEndpointAsync();
            try
            {
                return await SendOnceAsync(endpoint, path, content, responseType, timeout, ct);
            }
            catch (HttpRequestException) when (attempt == 1)
            {
                launcher.Invalidate(endpoint);
            }
            catch (HttpRequestException ex)
            {
                throw new AgentUnavailableException($"The agent at {endpoint.Url} is unreachable: {ex.Message}");
            }
        }
    }

    private async Task<TResponse> SendOnceAsync<TResponse>(
        AgentEndpoint endpoint,
        string path,
        Func<HttpContent>? content,
        JsonTypeInfo<TResponse>? responseType,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url + path) { Content = content?.Invoke() };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.Token);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AgentException(AgentErrorCodes.Timeout, $"no answer within {timeout.TotalSeconds:0} s");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                if (responseType is null)
                {
                    return default!;
                }

                return await response.Content.ReadFromJsonAsync(responseType, ct)
                    ?? throw new AgentException(AgentErrorCodes.FetchFailed, "the agent returned an empty body");
            }

            AgentError? error = null;
            try
            {
                error = await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.AgentError, ct);
            }
            catch (JsonException)
            {
            }

            throw new AgentException(
                error?.Code ?? AgentErrorCodes.FetchFailed,
                error?.Message ?? $"the agent returned HTTP {(int)response.StatusCode}");
        }
    }
}
