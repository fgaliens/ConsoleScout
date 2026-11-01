using System.Text;
using ConsoleScout.Configuration;
using ConsoleScout.Tools;

namespace ConsoleScout.AI;

/// <summary>Assembles the system prompt from the options in effect for one run, so that what the
/// model is told always matches what it was actually given: each tool contributes its own guidance,
/// and a tool that is not registered is never mentioned.</summary>
internal sealed class SystemPromptBuilder
{
    private const string Role =
        """
        You are a helpful assistant.
        Always answer in the language the user wrote in.

        This is a single question from a command-line tool, not a conversation: the user cannot reply,
        and your answer is the last thing they see. So never ask the user anything - no clarifying or
        follow-up questions, and no closing offers such as "Want me to ...?". When the question is
        ambiguous or lacks details, choose the most likely interpretation, say in a few words which one
        you assumed, and answer it fully; mention briefly what would change under another reading when
        that matters.
        """;

    private const string Citations =
        """
        Do not add citation markers such as [1] and do not end with a list of sources. Give a link in the
        text only when the link itself helps the user, such as a download or documentation page, and only
        a URL a tool actually returned. Never invent a URL.
        """;

    private const string Offline =
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

    private IReadOnlyList<IAssistantTool> _tools = [];
    private string? _environment;
    private AnswerLength _length = AnswerLength.Normal;
    private AnswerFormat _format = AnswerFormat.Markdown;

    public SystemPromptBuilder WithTools(IReadOnlyList<IAssistantTool> tools)
    {
        _tools = tools;
        return this;
    }

    public SystemPromptBuilder WithEnvironment(string description)
    {
        _environment = description;
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

        if (_environment is not null)
        {
            Append(builder, _environment);
        }

        if (_tools.Count == 0)
        {
            Append(builder, Offline);
        }
        else
        {
            foreach (var tool in _tools)
            {
                Append(builder, tool.PromptGuidance);
            }

            Append(builder, Citations);
        }

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
