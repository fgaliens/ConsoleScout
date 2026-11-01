using System.ClientModel;
using System.ClientModel.Primitives;
using ConsoleScout.Configuration;
using ConsoleScout.Other;
using OpenAI;
using OpenAI.Chat;

namespace ConsoleScout.AI;

/// <summary>One OpenAI-compatible chat endpoint, configured from <see cref="ModelSettings"/>. Shared by every
/// model the app talks to, so all of them retry a rate limit once and fail with the same AiException.</summary>
internal sealed class ModelClient : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly ModelSettings _settings;
    private readonly HttpClient _http;
    private readonly ChatClient _client;

    public ModelClient(ModelSettings settings, string apiKey)
    {
        _settings = settings;
        _http = settings.FlattenContentArrays
            ? new HttpClient(new ContentFlatteningHandler())
            : new HttpClient();

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(settings.Endpoint),
            Transport = new HttpClientPipelineTransport(_http),
        };

        var credential = new ApiKeyCredential(apiKey);
        _client = new ChatClient(settings.Name, credential, options);
    }

    public ModelSettings Settings => _settings;

    /// <exception cref="AiException">The endpoint failed or could not be reached.</exception>
    public async Task<ChatCompletion> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken ct)
    {
        try
        {
            var result = await _client.CompleteChatAsync(messages, options, ct);
            return result.Value;
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            await Task.Delay(RetryDelay, ct);
        }
        catch (Exception ex) when (Translate(ex) is { } translated)
        {
            throw translated;
        }

        try
        {
            var retried = await _client.CompleteChatAsync(messages, options, ct);
            return retried.Value;
        }
        catch (Exception ex) when (Translate(ex) is { } translated)
        {
            throw translated;
        }
    }

    /// <summary>A single exchange without tools; returns the reply's text, empty when there is none.</summary>
    public async Task<string> CompleteTextAsync(string system, string user, CancellationToken ct)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(system),
            new UserChatMessage(user),
        ];

        var options = new ChatCompletionOptions();
        var completion = await CompleteAsync(messages, options, ct);
        return completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Null for anything that is not an endpoint failure, which then propagates as the bug it is.
    /// The SDK retries transport failures itself and reports them as an AggregateException.</summary>
    private AiException? Translate(Exception ex) => ex switch
    {
        ClientResultException { Status: > 0 } result => Translate(result),
        ClientResultException or HttpRequestException => Unreachable(ex.Message),
        AggregateException aggregate when aggregate.InnerExceptions.All(e => e is ClientResultException or HttpRequestException) =>
            Unreachable(aggregate.InnerExceptions[^1].Message),
        _ => null,
    };

    private AiException Unreachable(string reason) =>
        new($"Cannot reach the model endpoint {_settings.Endpoint}: {reason}");

    private static AiException Translate(ClientResultException ex)
    {
        if (ex.Status == 429)
        {
            return new AiException("Rate limit reached. Please wait a minute and try again.");
        }

        var body = ReadBody(ex);
        return new AiException($"The model endpoint returned {ex.Status}:{Environment.NewLine}{body}");
    }

    private static string ReadBody(ClientResultException ex)
    {
        using var body = ex.GetRawResponse()?.Content?.ToStream();
        if (body is null)
        {
            return "(empty body)";
        }

        using var reader = new StreamReader(body);
        return reader.ReadToEnd();
    }
}
