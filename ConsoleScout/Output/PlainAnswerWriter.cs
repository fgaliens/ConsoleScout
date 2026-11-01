using Spectre.Console;

namespace ConsoleScout.Output;

internal sealed class PlainAnswerWriter : AnswerWriter
{
    protected override void WriteAnswer(string answer)
    {
        var text = MathTranscriber.Transcribe(answer);
        AnsiConsole.WriteLine(text);
    }
}
