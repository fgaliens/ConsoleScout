namespace ConsoleScout.Output;

/// <summary>Writes a finished answer to the console. The implementation is chosen from
/// OutputSettings.Format, so the model's text and the way it is displayed stay independent.</summary>
internal interface IAnswerWriter
{
    void Write(string answer);
}
