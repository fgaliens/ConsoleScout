using Spectre.Console;

namespace ConsoleScout.Output;

/// <summary>Shared frame around an answer: the blank line before it and the empty-answer notice.
/// Only the answer body itself differs between formats.</summary>
internal abstract class AnswerWriter : IAnswerWriter
{
    public void Write(string answer)
    {
        AnsiConsole.WriteLine();

        if (answer.Length > 0)
        {
            WriteAnswer(answer);
        }
        else
        {
            AnsiConsole.WriteLine("(the model returned an empty answer)");
        }
    }

    protected abstract void WriteAnswer(string answer);
}
