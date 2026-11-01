using Spectre.Console;

namespace ConsoleScout.Output;

internal sealed class MarkdownAnswerWriter(MarkdownRenderer renderer) : AnswerWriter
{
    protected override void WriteAnswer(string answer)
    {
        var rendered = renderer.Render(answer);
        AnsiConsole.Write(rendered);
    }
}
