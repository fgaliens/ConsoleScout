using System.Text.Json;
using OpenAI.Chat;

namespace ConsoleScout.Tools;

/// <summary>Routes the model's tool calls to the registered tools, and turns everything the model may
/// get wrong - an unknown name, malformed arguments, a tool that failed - into text it can act on.
/// Keeps the tools themselves free of any OpenAI SDK type.</summary>
internal sealed class ToolInvoker
{
    private readonly Dictionary<string, IAssistantTool> _tools;

    public ToolInvoker(IEnumerable<IAssistantTool> tools)
    {
        Tools = tools.ToList();
        _tools = Tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        Definitions = Tools
            .Select(tool => ChatTool.CreateFunctionTool(tool.Name, tool.Description, tool.ParametersSchema))
            .ToList();
    }

    public IReadOnlyList<IAssistantTool> Tools { get; }

    public IReadOnlyList<ChatTool> Definitions { get; }

    public async Task<string> InvokeAsync(ChatToolCall call, CancellationToken ct)
    {
        if (!_tools.TryGetValue(call.FunctionName, out var tool))
        {
            return $"Error: unknown tool '{call.FunctionName}'. Available: {string.Join(", ", _tools.Keys)}.";
        }

        JsonElement arguments;
        try
        {
            using var document = JsonDocument.Parse(call.FunctionArguments);
            arguments = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return $"Error: the arguments for {tool.Name} were not valid JSON. Retry with a JSON object.";
        }

        try
        {
            return await tool.InvokeAsync(arguments, ct);
        }
        catch (ToolException ex)
        {
            return $"Error: {tool.Name} failed: {ex.Message}.";
        }
    }
}
