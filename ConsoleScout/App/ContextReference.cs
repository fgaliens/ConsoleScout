using System.Text.RegularExpressions;

namespace ConsoleScout.App;

/// <summary>Which earlier answer -c follows up on: answer <see cref="Id"/> from any terminal, or the question
/// <see cref="Back"/> steps before the latest one in this terminal (0, the default, is the latest).</summary>
internal sealed partial record ContextReference(long? Id, int Back)
{
    public static ContextReference Latest { get; } = new(null, 0);

    /// <summary>x42 refers to answer 42, n3 to the question three before the latest; case does not matter.</summary>
    [GeneratedRegex(@"^(?<kind>[xXnN])(?<number>\d+)$")]
    public static partial Regex Pattern();

    /// <returns>Null when <paramref name="value"/> is not a reference; it then belongs to the question.</returns>
    public static ContextReference? TryParse(string value)
    {
        var match = Pattern().Match(value);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var number))
        {
            return null;
        }

        var kind = char.ToLowerInvariant(match.Groups["kind"].Value[0]);
        return kind == 'x' ? new ContextReference(number, 0) : new ContextReference(null, number);
    }
}
