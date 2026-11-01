using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Spectre.Console;
using Spectre.Console.Rendering;
using MarkdigTable = Markdig.Extensions.Tables.Table;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using SpectreTable = Spectre.Console.Table;

namespace ConsoleScout.Output;

/// <summary>Turns the Markdown a model produces into Spectre widgets. Markdig owns the parsing, so
/// this type only maps syntax nodes to renderables and escapes every piece of literal text - the
/// answers carry citations like [1], which Spectre would otherwise read as markup.</summary>
internal sealed class MarkdownRenderer(ConsoleTheme theme)
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .Build();

    public IRenderable Render(string markdown)
    {
        var transcribed = MathTranscriber.Transcribe(markdown);
        var document = Markdig.Markdown.Parse(transcribed, Pipeline);
        var blocks = RenderBlocks(document, null);
        return new Rows(blocks);
    }

    private IEnumerable<IRenderable> RenderBlocks(ContainerBlock blocks, string? style)
    {
        var first = true;

        foreach (var block in blocks)
        {
            var rendered = RenderBlock(block, style);
            if (rendered is null)
            {
                continue;
            }

            if (!first)
            {
                yield return Text.Empty;
            }

            first = false;
            yield return rendered;
        }
    }

    private IRenderable? RenderBlock(Block block, string? style) => block switch
    {
        HeadingBlock heading => RenderHeading(heading),
        MarkdigTable table => RenderTable(table),
        ListBlock list => RenderList(list, style),
        QuoteBlock quote => RenderQuote(quote),
        CodeBlock code => RenderCode(code),
        ThematicBreakBlock => RenderBreak(),
        HtmlBlock html => RenderHtml(html),
        LeafBlock leaf when leaf.Inline is not null => RenderLeaf(leaf.Inline, style),
        _ => null,
    };

    private IRenderable RenderQuote(QuoteBlock quote)
    {
        var blocks = RenderBlocks(quote, theme.Quote);
        return new Padder(new Rows(blocks), new Padding(2, 0, 0, 0));
    }

    private IRenderable RenderBreak()
    {
        var style = Style.Parse(theme.Rule);
        return new Rule { Style = style };
    }

    private static IRenderable RenderHtml(HtmlBlock html)
    {
        var lines = ReadLines(html);
        return new Text(lines);
    }

    private IRenderable RenderLeaf(ContainerInline inline, string? style)
    {
        var markup = RenderInlines(inline);
        var styled = ConsoleMarkup.Styled(markup, style);
        return new Markup(styled);
    }

    private IRenderable RenderHeading(HeadingBlock heading)
    {
        var inlines = RenderInlines(heading.Inline);
        var title = ConsoleMarkup.Styled(inlines, theme.Heading);
        var ruleStyle = Style.Parse(theme.Rule);

        return heading.Level <= 2
            ? new Rule(title) { Style = ruleStyle, Justification = Justify.Left }
            : new Markup(title);
    }

    private IRenderable RenderList(ListBlock list, string? style)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn { NoWrap = true, Padding = new Padding(0, 0, 1, 0) });
        grid.AddColumn(new GridColumn { Padding = new Padding(0, 0, 0, 0) });

        var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var block in list)
        {
            if (block is not ListItemBlock item)
            {
                continue;
            }

            var marker = list.IsOrdered ? $"{number++}." : "•";
            var styledMarker = ConsoleMarkup.Styled(marker, theme.ListMarker);
            var content = RenderBlocks(item, style);
            grid.AddRow(new Markup(styledMarker), new Rows(content));
        }

        return grid;
    }

    private IRenderable RenderCode(CodeBlock code)
    {
        var lines = ReadLines(code);
        var textStyle = Style.Parse(theme.CodeBlock);
        var borderStyle = Style.Parse(theme.CodeBorder);
        var panel = new Panel(new Text(lines, textStyle))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = borderStyle,
            Expand = false,
        };

        var language = (code as FencedCodeBlock)?.Info;
        if (string.IsNullOrWhiteSpace(language))
        {
            return panel;
        }

        var escaped = Markup.Escape(language);
        var header = ConsoleMarkup.Styled(escaped, theme.CodeLanguage);
        return panel.Header(header);
    }

    private IRenderable RenderTable(MarkdigTable table)
    {
        var rows = new List<(bool IsHeader, IRenderable[] Cells)>();

        foreach (var block in table)
        {
            if (block is not MarkdigTableRow row)
            {
                continue;
            }

            var cells = new List<IRenderable>();
            foreach (var child in row)
            {
                if (child is MarkdigTableCell cell)
                {
                    var text = RenderCell(cell);
                    var content = ConsoleMarkup.Styled(text, row.IsHeader ? theme.TableHeader : null);
                    cells.Add(new Markup(content));
                }
            }

            var renderedCells = cells.ToArray();
            rows.Add((row.IsHeader, renderedCells));
        }

        var rendered = new SpectreTable
        {
            Border = TableBorder.Rounded,
            BorderStyle = Style.Parse(theme.TableBorder),
        };

        var header = rows.Find(row => row.IsHeader).Cells;
        var columns = rows.Count == 0 ? 0 : rows.Max(row => row.Cells.Length);

        for (int i = 0; i < columns; i++)
        {
            var column = header is not null && i < header.Length
                ? new TableColumn(header[i])
                : new TableColumn(Text.Empty);

            column.Alignment = Alignment(table.ColumnDefinitions, i);
            rendered.AddColumn(column);
        }

        if (header is null)
        {
            rendered.HideHeaders();
        }

        foreach (var row in rows)
        {
            if (!row.IsHeader)
            {
                rendered.AddRow(row.Cells);
            }
        }

        return rendered;
    }

    private static Justify? Alignment(List<TableColumnDefinition> definitions, int index)
    {
        if (index >= definitions.Count)
        {
            return null;
        }

        return definitions[index].Alignment switch
        {
            TableColumnAlign.Center => Justify.Center,
            TableColumnAlign.Right => Justify.Right,
            TableColumnAlign.Left => Justify.Left,
            _ => null,
        };
    }

    private string RenderCell(MarkdigTableCell cell)
    {
        var builder = new StringBuilder();

        foreach (var block in cell)
        {
            if (block is not LeafBlock { Inline: not null } leaf)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            var inlines = RenderInlines(leaf.Inline);
            builder.Append(inlines);
        }

        return builder.ToString();
    }

    private string RenderInlines(ContainerInline? container)
    {
        var builder = new StringBuilder();
        AppendInlines(builder, container);
        return builder.ToString();
    }

    private void AppendInlines(StringBuilder builder, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AppendInline(builder, inline);
        }
    }

    private void AppendInline(StringBuilder builder, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
            {
                var text = literal.Content.ToString();
                var escaped = Markup.Escape(text);
                builder.Append(escaped);
                break;
            }

            case EmphasisInline emphasis:
                AppendEmphasis(builder, emphasis);
                break;

            case CodeInline code:
            {
                var escaped = Markup.Escape(code.Content);
                var styled = ConsoleMarkup.Styled(escaped, theme.Code);
                builder.Append(styled);
                break;
            }

            case LinkInline link:
                AppendLink(builder, link);
                break;

            case AutolinkInline autolink:
            {
                var label = Markup.Escape(autolink.Url);
                var linked = ConsoleMarkup.Link(autolink.Url, label, theme.Link);
                builder.Append(linked);
                break;
            }

            case LineBreakInline lineBreak:
                builder.Append(lineBreak.IsHard ? '\n' : ' ');
                break;

            case HtmlInline html:
            {
                var escaped = Markup.Escape(html.Tag);
                builder.Append(escaped);
                break;
            }

            case ContainerInline nested:
                AppendInlines(builder, nested);
                break;
        }
    }

    private void AppendEmphasis(StringBuilder builder, EmphasisInline emphasis)
    {
        var style = emphasis.DelimiterChar switch
        {
            '*' or '_' => emphasis.DelimiterCount switch
            {
                1 => "italic",
                2 => "bold",
                _ => "bold italic",
            },
            '~' when emphasis.DelimiterCount == 2 => "strikethrough",
            _ => null,
        };

        if (style is null)
        {
            AppendInlines(builder, emphasis);
            return;
        }

        builder.Append('[').Append(style).Append(']');
        AppendInlines(builder, emphasis);
        builder.Append("[/]");
    }

    private void AppendLink(StringBuilder builder, LinkInline link)
    {
        var label = RenderInlines(link);
        if (label.Length == 0)
        {
            label = Markup.Escape(link.Url ?? string.Empty);
        }

        var rendered = link.IsImage
            ? ConsoleMarkup.Styled(label, theme.Link)
            : ConsoleMarkup.Link(link.Url, label, theme.Link);
        builder.Append(rendered);
    }

    private static string ReadLines(LeafBlock block)
    {
        var builder = new StringBuilder();

        for (int i = 0; i < block.Lines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            var line = block.Lines.Lines[i].Slice.ToString();
            builder.Append(line);
        }

        return builder.ToString();
    }
}
