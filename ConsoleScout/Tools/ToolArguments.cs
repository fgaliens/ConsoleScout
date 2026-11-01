using System.Text.Json;

namespace ConsoleScout.Tools;

internal static class ToolArguments
{
    public static string RequireString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        throw new ToolException($"missing required string argument '{name}'");
    }

    /// <summary>A non-empty array of strings. Blank entries are dropped; non-string ones are an error,
    /// since they mean the model misread the schema.</summary>
    public static IReadOnlyList<string> RequireStringArray(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            throw new ToolException($"missing required array argument '{name}'");
        }

        List<string> items = [];
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new ToolException($"'{name}' must contain only strings");
            }

            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                items.Add(text);
            }
        }

        if (items.Count == 0)
        {
            throw new ToolException($"'{name}' is empty");
        }

        return items;
    }
}
