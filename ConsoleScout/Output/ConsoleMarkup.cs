using System.Buffers;

namespace ConsoleScout.Output;

internal static class ConsoleMarkup
{
    // A url carrying any of these would end the markup tag early, so such links stay plain text.
    private static readonly SearchValues<char> Unsupported = SearchValues.Create("[] ");

    public static string Styled(string markup, string? style) =>
        style is null ? markup : $"[{style}]{markup}[/]";

    public static string Link(string? url, string label, string style) =>
        string.IsNullOrEmpty(url) || url.AsSpan().IndexOfAny(Unsupported) >= 0
            ? Styled(label, style)
            : $"[{style} link={url}]{label}[/]";
}
