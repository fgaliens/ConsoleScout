using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ConsoleScout.Agent.Fetching;

/// <summary>Turns a page into readable text: chrome such as navigation and footers is dropped, block
/// elements become line breaks, and whitespace is collapsed. The browser path does the same in the page
/// itself (<see cref="BrowserScript"/>), so both modes hand the model text of the same shape.</summary>
internal static class TextExtractor
{
    /// <summary>Elements that never carry the content a reader came for.</summary>
    public const string Noise = "script, style, noscript, template, svg, canvas, iframe, nav, footer, aside, header, form, [aria-hidden=true], [hidden]";

    /// <summary>The first match is taken as the content root; the body otherwise.</summary>
    public const string ContentRoots = "main, article, [role=main]";

    /// <summary>Same extraction as the static path, run inside a loaded page. Returns { title, text }.</summary>
    public const string BrowserScript =
        $$"""
        () => {
          document.querySelectorAll('{{Noise}}').forEach(e => e.remove());
          const root = document.querySelector('{{ContentRoots}}') || document.body;
          return { title: document.title || '', text: root ? root.innerText : '' };
        }
        """;

    private static readonly HashSet<string> Blocks = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "section", "article", "main", "li", "ul", "ol", "dl", "dt", "dd", "table", "tr",
        "h1", "h2", "h3", "h4", "h5", "h6", "pre", "blockquote", "figure", "figcaption", "hr", "br",
    };

    private static readonly HtmlParser Parser = new();

    public static (string Title, string Text) FromHtml(string html)
    {
        using var document = Parser.ParseDocument(html);

        foreach (var element in document.QuerySelectorAll(Noise).ToList())
        {
            element.Remove();
        }

        var root = document.QuerySelector(ContentRoots) ?? document.Body;
        var builder = new StringBuilder();
        if (root is not null)
        {
            Append(root, builder);
        }

        var title = document.Title?.Trim() ?? string.Empty;
        var raw = builder.ToString();
        var text = Clean(raw);
        return (title, text);
    }

    /// <summary>Trims every line, collapses runs of spaces and of blank lines.</summary>
    public static string Clean(string text)
    {
        var builder = new StringBuilder(text.Length);
        var blank = 0;

        foreach (var raw in text.Split('\n'))
        {
            var words = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var line = string.Join(' ', words);
            if (line.Length == 0)
            {
                blank++;
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(blank > 0 ? "\n\n" : "\n");
            }

            builder.Append(line);
            blank = 0;
        }

        return builder.ToString();
    }

    private static void Append(INode node, StringBuilder builder)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText text:
                    builder.Append(text.Data);
                    break;

                case IElement element when element.LocalName == "pre":
                    builder.Append('\n').Append(element.TextContent).Append('\n');
                    break;

                case IElement element:
                    var block = Blocks.Contains(element.LocalName);
                    if (block)
                    {
                        builder.Append('\n');
                    }

                    if (element.LocalName == "li")
                    {
                        builder.Append("- ");
                    }

                    Append(element, builder);

                    if (block)
                    {
                        builder.Append('\n');
                    }
                    else if (element.LocalName is "td" or "th")
                    {
                        builder.Append(" | ");
                    }

                    break;
            }
        }
    }
}
