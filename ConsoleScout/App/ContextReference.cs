using System.Text.RegularExpressions;
using ConsoleScout.Contracts;

namespace ConsoleScout.App;

/// <summary>Which earlier answer -c follows up on: message <see cref="Id"/> from any terminal (m42), the latest
/// message of dialog <see cref="DialogId"/> from any terminal (d7), or the question <see cref="Back"/> steps
/// before the latest one in this terminal (b3, "back"; 0, the default, is the latest).</summary>
internal sealed partial record ContextReference(long? Id, long? DialogId, int Back)
{
    public static ContextReference Latest { get; } = new(null, null, 0);

    /// <summary>m42 refers to message 42, d7 to dialog 7, b3 to the question three before the latest;
    /// case does not matter.</summary>
    [GeneratedRegex(@"^(?<kind>[mMbBdD])(?<number>\d+)$")]
    public static partial Regex Pattern();

    /// <summary>How the reference reads in messages, e.g. "dialog d7".</summary>
    public string Description => this switch
    {
        { Id: { } id } => $"message m{id}",
        { DialogId: { } dialog } => $"dialog d{dialog}",
        { Back: 0 } => "the last answer",
        _ => $"the answer {Back} back",
    };

    public HistoryReference ToHistory() => new(Id, DialogId, Back);

    /// <returns>Null when <paramref name="value"/> is not a reference; it then belongs to the question.</returns>
    public static ContextReference? TryParse(string value)
    {
        var match = Pattern().Match(value);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var number))
        {
            return null;
        }

        var kind = char.ToLowerInvariant(match.Groups["kind"].Value[0]);
        return kind switch
        {
            'm' => new ContextReference(number, null, 0),
            'd' => new ContextReference(null, number, 0),
            _ => new ContextReference(null, null, number),
        };
    }
}
