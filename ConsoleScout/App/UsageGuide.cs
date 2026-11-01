namespace ConsoleScout.App;

/// <summary>The part of README.md shown by a run without arguments: the introduction, getting started and
/// usage. The README is embedded in the executable, so the guide and the documentation cannot drift apart.</summary>
internal static class UsageGuide
{
    private const string ResourceName = "ConsoleScout.README.md";

    /// <summary>Second-level sections kept besides the introduction above the first of them.</summary>
    private static readonly string[] Sections = ["Getting started", "Usage"];

    private const string Footer =
        "Configuration, follow-up questions and troubleshooting: `csc --readme`. Setup state: `csc --status`.";

    /// <summary>The introduction, quick start and usage, with a pointer to the rest.</summary>
    public static string Load()
    {
        var readme = LoadReadme();
        var selected = Select(readme);
        return $"{selected.TrimEnd()}\n\n{Footer}\n";
    }

    /// <summary>The whole README, for --readme.</summary>
    public static string LoadReadme()
    {
        using var stream = typeof(UsageGuide).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Select(string readme)
    {
        var kept = new List<string>();
        var keep = true;

        foreach (var line in readme.Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var heading = line[3..].Trim();
                keep = Sections.Contains(heading, StringComparer.OrdinalIgnoreCase);
            }

            if (keep)
            {
                kept.Add(line);
            }
        }

        return string.Join('\n', kept);
    }
}
