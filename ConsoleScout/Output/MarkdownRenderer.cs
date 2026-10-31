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

namespace ConsoleScout.Presentation;

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

    public IRenderable Render(string markdown) =>
        new Rows(RenderBlocks(Markdig.Markdown.Parse(MathTranscriber.Transcribe(markdown), Pipeline), null));

    private IEnumerable<IRenderable> RenderBlocks(ContainerBlock blocks, string? style)
    {
        var first = true;

        foreach (var block in blocks)
        {
            var rendered = RenderBlock(block, style);
            if (rendered is null)
                continue;

            if (!first)
                yield return Text.Empty;

            first = false;
            yield return rendered;
        }
    }

    private IRenderable? RenderBlock(Block block, string? style) => block switch
    {
        HeadingBlock heading => RenderHeading(heading),
        MarkdigTable table => RenderTable(table),
        ListBlock list => RenderList(list, style),
        QuoteBlock quote => new Padder(new Rows(RenderBlocks(quote, theme.Quote)), new Padding(2, 0, 0, 0)),
        CodeBlock code => RenderCode(code),
        ThematicBreakBlock => new Rule { Style = Style.Parse(theme.Rule) },
        HtmlBlock html => new Text(ReadLines(html)),
        LeafBlock leaf when leaf.Inline is not null => new Markup(ConsoleMarkup.Styled(RenderInlines(leaf.Inline), style)),
        _ => null,
    };

    private IRenderable RenderHeading(HeadingBlock heading)
    {
        var title = ConsoleMarkup.Styled(RenderInlines(heading.Inline), theme.Heading);

        return heading.Level <= 2
            ? new Rule(title) { Style = Style.Parse(theme.Rule), Justification = Justify.Left }
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
                continue;

            var marker = list.IsOrdered ? $"{number++}." : "•";
            grid.AddRow(
                new Markup(ConsoleMarkup.Styled(marker, theme.ListMarker)),
                new Rows(RenderBlocks(item, style)));
        }

        return grid;
    }

    private IRenderable RenderCode(CodeBlock code)
    {
        var panel = new Panel(new Text(ReadLines(code), Style.Parse(theme.CodeBlock)))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(theme.CodeBorder),
            Expand = false,
        };

        var language = (code as FencedCodeBlock)?.Info;

        return string.IsNullOrWhiteSpace(language)
            ? panel
            : panel.Header(ConsoleMarkup.Styled(Markup.Escape(language), theme.CodeLanguage));
    }

    private IRenderable RenderTable(MarkdigTable table)
    {
        var rows = new List<(bool IsHeader, IRenderable[] Cells)>();

        foreach (var block in table)
        {
            if (block is not MarkdigTableRow row)
                continue;

            var cells = new List<IRenderable>();
            foreach (var child in row)
            {
                if (child is MarkdigTableCell cell)
                {
                    var content = ConsoleMarkup.Styled(RenderCell(cell), row.IsHeader ? theme.TableHeader : null);
                    cells.Add(new Markup(content));
                }
            }

            rows.Add((row.IsHeader, cells.ToArray()));
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
            rendered.HideHeaders();

        foreach (var row in rows)
        {
            if (!row.IsHeader)
                rendered.AddRow(row.Cells);
        }

        return rendered;
    }

    private static Justify? Alignment(List<TableColumnDefinition> definitions, int index)
    {
        if (index >= definitions.Count)
            return null;

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
                continue;

            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append(RenderInlines(leaf.Inline));
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
            return;

        foreach (var inline in container)
            AppendInline(builder, inline);
    }

    private void AppendInline(StringBuilder builder, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(Markup.Escape(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                AppendEmphasis(builder, emphasis);
                break;

            case CodeInline code:
                builder.Append(ConsoleMarkup.Styled(Markup.Escape(code.Content), theme.Code));
                break;

            case LinkInline link:
                AppendLink(builder, link);
                break;

            case AutolinkInline autolink:
                builder.Append(ConsoleMarkup.Link(autolink.Url, Markup.Escape(autolink.Url), theme.Link));
                break;

            case LineBreakInline lineBreak:
                builder.Append(lineBreak.IsHard ? '\n' : ' ');
                break;

            case HtmlInline html:
                builder.Append(Markup.Escape(html.Tag));
                break;

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
            label = Markup.Escape(link.Url ?? string.Empty);

        builder.Append(link.IsImage
            ? ConsoleMarkup.Styled(label, theme.Link)
            : ConsoleMarkup.Link(link.Url, label, theme.Link));
    }

    private static string ReadLines(LeafBlock block)
    {
        var builder = new StringBuilder();

        for (int i = 0; i < block.Lines.Count; i++)
        {
            if (i > 0)
                builder.Append('\n');

            builder.Append(block.Lines.Lines[i].Slice.ToString());
        }

        return builder.ToString();
    }
}
