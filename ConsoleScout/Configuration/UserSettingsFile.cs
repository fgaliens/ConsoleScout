using System.Text.Json;
using System.Text.Json.Nodes;

namespace ConsoleScout.Configuration;

/// <summary>Edits one value in a JSON settings file of ours (secrets.json, preferences.json) in place, as a
/// JSON tree, so whatever else is in the file survives untouched.</summary>
internal static class UserSettingsFile
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Sets the value at <paramref name="keys"/> (e.g. ["models", "main", "apiKey"]), creating the
    /// file and the sections on the way. Sections are matched case-insensitively, like the configuration binder.</summary>
    /// <param name="ownerOnly">Restricts the file to its owner (0600), for files that hold secrets.</param>
    public static void Set(string path, IReadOnlyList<string> keys, JsonNode value, bool ownerOnly)
    {
        var root = Read(path);

        var section = root;
        for (var i = 0; i < keys.Count - 1; i++)
        {
            section = Section(section, keys[i], path);
        }

        section[keys[^1]] = value;

        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        // Written next to the target and moved into place, so a crash cannot leave half a file; an
        // owner-only file is restricted before anything is written into it.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, string.Empty);
        if (ownerOnly && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        var json = root.ToJsonString(Indented) + Environment.NewLine;
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }

    private static JsonObject Read(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            };

            return JsonNode.Parse(json, documentOptions: options) as JsonObject
                ?? throw new ConfigurationException($"{path} does not contain a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"{path} is not valid JSON, so it was left as is.{Environment.NewLine}{ex.Message}");
        }
    }

    private static JsonObject Section(JsonObject parent, string name, string path)
    {
        var existing = parent.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase));
        if (existing.Value is JsonObject section)
        {
            return section;
        }

        if (existing.Key is not null)
        {
            throw new ConfigurationException($"{path}: \"{existing.Key}\" is not an object.");
        }

        section = new JsonObject();
        parent[name] = section;
        return section;
    }
}
