using ConsoleScout.Configuration;
using ConsoleScout.Tools;
using OpenAI.Chat;

namespace ConsoleScout.AI;

/// <summary>Runs the question through the model, letting it call the registered tools in between.
/// With no tools registered none is offered, and the prompt says so.
/// Writes nothing to the console: failures surface as AiException.</summary>
internal sealed class Assistant(ModelClient model, AnswerFormat format, ToolInvoker tools, EnvironmentInfo environment)
{
    public async Task<string> AskAsync(
        string question,
        AnswerLength length = AnswerLength.Normal,
        CancellationToken ct = default)
    {
        var description = environment.Describe();
        var prompt = new SystemPromptBuilder()
            .WithEnvironment(description)
            .WithTools(tools.Tools)
            .WithLength(length)
            .WithFormat(format)
            .Build();

        List<ChatMessage> messages =
        [
            new SystemChatMessage(prompt),
            new UserChatMessage(question),
        ];

        var withTools = new ChatCompletionOptions();
        foreach (var definition in tools.Definitions)
        {
            withTools.Tools.Add(definition);
        }

        var withoutTools = new ChatCompletionOptions();

        // Without tools the first round is already the last one, so the loop needs no special case.
        var toolRounds = tools.Definitions.Count == 0 ? 0 : model.Settings.MaxToolRounds;

        for (int round = 0; ; round++)
        {
            // The final round offers no tools, so the model has to produce an answer instead of another call.
            bool finalRound = round == toolRounds;
            var options = finalRound ? withoutTools : withTools;
            ChatCompletion completion = await model.CompleteAsync(messages, options, ct);

            if (finalRound || completion.FinishReason != ChatFinishReason.ToolCalls)
            {
                return completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
            }

            messages.Add(new AssistantChatMessage(completion));
            foreach (ChatToolCall call in completion.ToolCalls)
            {
                var result = await tools.InvokeAsync(call, ct);
                messages.Add(new ToolChatMessage(call.Id, result));
            }
        }
    }
}
