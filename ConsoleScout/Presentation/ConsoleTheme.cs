namespace ConsoleScout.Presentation;

/// <summary>Spectre styles used when writing an answer. Every style is an init-only string so the
/// whole theme can later be bound from appsettings.json without touching the writers.</summary>
internal sealed record ConsoleTheme
{
    public static ConsoleTheme Default { get; } = new();

    public string Heading { get; init; } = "bold deepskyblue1";

    public string Rule { get; init; } = "grey42";

    public string Code { get; init; } = "wheat1";

    public string CodeBlock { get; init; } = "grey85";

    public string CodeBorder { get; init; } = "grey42";

    public string CodeLanguage { get; init; } = "grey62";

    public string Link { get; init; } = "blue underline";

    public string Quote { get; init; } = "italic grey70";

    public string ListMarker { get; init; } = "deepskyblue1";

    public string TableBorder { get; init; } = "grey42";

    public string TableHeader { get; init; } = "bold";
}
