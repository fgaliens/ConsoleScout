using Spectre.Console;

namespace ConsoleScout.Presentation;

internal sealed class PlainAnswerWriter(ConsoleTheme theme) : AnswerWriter(theme)
{
    protected override void WriteAnswer(string answer) => AnsiConsole.WriteLine(MathTranscriber.Transcribe(answer));
}
