namespace ConsoleScout.App;

/// <summary>Lets -c take an optional reference without swallowing the question. System.CommandLine would hand
/// an optional option value the next word whatever it is, so "csc -c and why" would lose "and". Before parsing,
/// a reference right after -c (x42, d7, n3) is moved to a hidden option, and -c itself becomes a plain flag.</summary>
internal static class ContextArguments
{
    public const string ReferenceOption = "--with-context-reference";

    public static string[] Normalize(IReadOnlyList<string> args)
    {
        var normalized = new List<string>(args.Count + 1);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            // Everything after "--" is the question, verbatim.
            if (arg == "--")
            {
                normalized.AddRange(args.Skip(i));
                break;
            }

            normalized.Add(arg);

            var isContextFlag = arg is "-c" or "--with-context" or "--continue";
            if (isContextFlag && i + 1 < args.Count && ContextReference.TryParse(args[i + 1]) is not null)
            {
                normalized.Add(ReferenceOption);
                normalized.Add(args[i + 1]);
                i++;
            }
        }

        return normalized.ToArray();
    }
}
