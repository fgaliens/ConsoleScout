using Spectre.Console;

namespace ConsoleScout.Presentation;

internal sealed class MarkdownAnswerWriter(MarkdownRenderer renderer, ConsoleTheme theme) : AnswerWriter(theme)
{
    protected override void WriteAnswer(string answer) => AnsiConsole.Write(renderer.Render(answer));
}
