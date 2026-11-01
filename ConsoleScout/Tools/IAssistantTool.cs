using System.Text.Json;

namespace ConsoleScout.Tools;

/// <summary>A capability offered to the model. Adding one takes this class and a DI registration:
/// the assistant loop and the system prompt pick it up from what is registered.</summary>
internal interface IAssistantTool
{
    /// <summary>The function name the model calls, e.g. "web_search".</summary>
    string Name { get; }

    string Description { get; }

    /// <summary>JSON Schema of the arguments object.</summary>
    BinaryData ParametersSchema { get; }

    /// <summary>Added to the system prompt: when to call the tool and how to use what it returns.</summary>
    string PromptGuidance { get; }

    /// <returns>The text the model reads as the call's result. Expected failures are thrown as
    /// <see cref="ToolException"/>, so the model gets to read them too.</returns>
    Task<string> InvokeAsync(JsonElement arguments, CancellationToken ct);
}
