using System.Text;
using ConsoleScout.Configuration;

namespace ConsoleScout.AI;

internal enum AnswerLength
{
    Normal,
    Short,
    Long,
}

/// <summary>Assembles the system prompt from the options in effect for one run, so that what the
/// model is told always matches what it was actually given.</summary>
internal sealed class SystemPromptBuilder
{
    private const string Role =
        """
        You are a helpful assistant.
        Always answer in the language the user wrote in.
        """;

    private const string SearchAvailable =
        """
        You have a web_search tool, and your training data is frozen in the past.
        Call web_search whenever the answer depends on something you cannot verify from memory:
        news, weather, prices, rates, schedules, statistics, release versions and dates,
        and anything phrased as latest / newest / current / today.
        "The most recent one I know of is X" is never an acceptable answer - search instead.
        Answer directly, without searching, only for general knowledge, reasoning, writing and coding questions.

        Cite sources as [1], [2] using the numbers from the search results, and only for results
        a search actually returned. If you did not search, cite nothing and never invent a URL.
        """;

    private const string SearchUnavailable =
        """
        You have no access to the internet, and your training data is frozen in the past.
        When the answer depends on something you cannot verify from memory - news, weather, prices,
        rates, schedules, statistics, release versions and dates, or anything phrased as
        latest / newest / current / today - answer from what you know and say plainly that you
        cannot check it and that the information may be outdated.
        Never invent a URL and never cite a source.
        """;

    private const string NormalAnswer = "Keep answers concise.";

    private const string ShortAnswer =
        """
        Answer in one or two sentences. No preamble, no restating the question,
        no closing offer of further help.
        """;

    private const string LongAnswer =
        """
        Give a thorough answer: cover the reasoning, the edge cases and the alternatives,
        with examples where they make the point clearer.
        """;

    private const string MarkdownOutput =
        """
        Write the answer in Markdown, using only what the console can render:
        ## headings, bullet and numbered lists, **bold**, *italic*, `inline code`,
        fenced code blocks tagged with their language, pipe tables, blockquotes and [links](url).
        Never use raw HTML, images or footnotes.
        Write mathematics as plain Unicode - √8 = 2√2 ≈ 2.83, x², H₂O - and never as LaTeX:
        no \[ \], no $$, no rac or \sqrt.
        """;

    private const string PlainOutput =
        """
        Write the answer in plain text. Use no Markdown at all: no #, *, _, ` or | formatting,
        no tables and no bracketed links. Separate ideas with blank lines and write a bare URL
        when a link is needed. Write mathematics as plain Unicode, never as LaTeX.
        """;

    private bool _searchAvailable;
    private AnswerLength _length = AnswerLength.Normal;
    private AnswerFormat _format = AnswerFormat.Markdown;

    public SystemPromptBuilder WithSearch(bool available)
    {
        _searchAvailable = available;
        return this;
    }

    public SystemPromptBuilder WithLength(AnswerLength length)
    {
        _length = length;
        return this;
    }

    public SystemPromptBuilder WithFormat(AnswerFormat format)
    {
        _format = format;
        return this;
    }

    public string Build()
    {
        var builder = new StringBuilder(Role);

        Append(builder, _searchAvailable ? SearchAvailable : SearchUnavailable);
        Append(builder, _length switch
        {
            AnswerLength.Short => ShortAnswer,
            AnswerLength.Long => LongAnswer,
            _ => NormalAnswer,
        });
        Append(builder, _format switch
        {
            AnswerFormat.Plain => PlainOutput,
            _ => MarkdownOutput,
        });

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string section) =>
        builder.Append('\n').Append('\n').Append(section);
}
