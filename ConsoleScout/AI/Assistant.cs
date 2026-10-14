using System.ClientModel;
using System.ClientModel.Primitives;
using ConsoleScout.AI.ToolProviders;
using ConsoleScout.Configuration;
using ConsoleScout.Other;
using OpenAI;
using OpenAI.Chat;

namespace ConsoleScout.AI;

/// <summary>Runs the question through the model, letting it call the search tool in between.
/// A null tool means search is off: none is offered, and the prompt says so.
/// Writes nothing to the console: failures surface as AiException.</summary>
internal sealed class Assistant : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly ModelSettings _settings;
    private readonly AnswerFormat _format;
    private readonly WebSearchTool? _tool;
    private readonly HttpClient _http;
    private readonly ChatClient _client;

    public Assistant(ModelSettings settings, AnswerFormat format, string apiKey, WebSearchTool? tool)
    {
        _settings = settings;
        _format = format;
        _tool = tool;
        _http = settings.FlattenContentArrays
            ? new HttpClient(new ContentFlatteningHandler())
            : new HttpClient();

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(settings.Endpoint),
            Transport = new HttpClientPipelineTransport(_http),
        };

        _client = new ChatClient(settings.Name, new ApiKeyCredential(apiKey), options);
    }

    public async Task<string> AskAsync(
        string question,
        AnswerLength length = AnswerLength.Normal,
        CancellationToken ct = default)
    {
        var prompt = new SystemPromptBuilder()
            .WithSearch(_tool is not null)
            .WithLength(length)
            .WithFormat(_format)
            .Build();

        List<ChatMessage> messages =
        [
            new SystemChatMessage(prompt),
            new UserChatMessage(question),
        ];

        var withTools = new ChatCompletionOptions { Tools = { WebSearchTool.Definition } };
        var withoutTools = new ChatCompletionOptions();

        // Without a tool the first round is already the last one, so the loop needs no special case.
        var toolRounds = _tool is null ? 0 : _settings.MaxToolRounds;

        for (int round = 0; ; round++)
        {
            // The final round offers no tools, so the model has to produce an answer instead of another call.
            bool finalRound = round == toolRounds;
            ChatCompletion completion = await CompleteAsync(messages, finalRound ? withoutTools : withTools, ct);

            if (finalRound || completion.FinishReason != ChatFinishReason.ToolCalls)
                return completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;

            messages.Add(new AssistantChatMessage(completion));
            foreach (ChatToolCall call in completion.ToolCalls)
                messages.Add(new ToolChatMessage(call.Id, await _tool!.InvokeAsync(call, ct)));
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task<ChatCompletion> CompleteAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken ct)
    {
        try
        {
            return (await _client.CompleteChatAsync(messages, options, ct)).Value;
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            await Task.Delay(RetryDelay, ct);
        }
        catch (ClientResultException ex)
        {
            throw Translate(ex);
        }

        try
        {
            return (await _client.CompleteChatAsync(messages, options, ct)).Value;
        }
        catch (ClientResultException ex)
        {
            throw Translate(ex);
        }
    }

    private static AiException Translate(ClientResultException ex) =>
        ex.Status == 429
            ? new AiException("Rate limit reached. Please wait a minute and try again.")
            : new AiException($"The model endpoint returned {ex.Status}:{Environment.NewLine}{ReadBody(ex)}");

    private static string ReadBody(ClientResultException ex)
    {
        using var body = ex.GetRawResponse()?.Content?.ToStream();
        return body is null ? "(empty body)" : new StreamReader(body).ReadToEnd();
    }
}

/// <summary>The model endpoint failed for a reason worth showing the user as is.</summary>
internal sealed class AiException(string message) : Exception(message);
