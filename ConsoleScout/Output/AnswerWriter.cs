using Spectre.Console;

namespace ConsoleScout.Presentation;

/// <summary>Writes a finished answer to the console. The implementation is chosen from
/// OutputSettings.Format, so the model's text and the way it is displayed stay independent.</summary>
internal interface IAnswerWriter
{
    void Write(string answer, IReadOnlyList<string> sources);
}

/// <summary>Shared frame around an answer: the blank line before it, the empty-answer notice and
/// the numbered source list. Only the answer body itself differs between formats.</summary>
internal abstract class AnswerWriter(ConsoleTheme theme) : IAnswerWriter
{
    public void Write(string answer, IReadOnlyList<string> sources)
    {
        AnsiConsole.WriteLine();

        if (answer.Length > 0)
            WriteAnswer(answer);
        else
            AnsiConsole.WriteLine("(модель вернула пустой ответ)");

        if (sources.Count == 0)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(ConsoleMarkup.Styled("Источники:", theme.Heading));

        for (int i = 0; i < sources.Count; i++)
        {
            var number = ConsoleMarkup.Styled($"[[{i + 1}]]", theme.ListMarker);
            var link = ConsoleMarkup.Link(sources[i], Markup.Escape(sources[i]), theme.Link);
            AnsiConsole.MarkupLine($"  {number} {link}");
        }
    }

    protected abstract void WriteAnswer(string answer);
}
