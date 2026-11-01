using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Text.RegularExpressions;

namespace ConsoleScout.App;

/// <summary>-h with its usage lines in the order arguments are written: options first, then the question,
/// for both names of the program. System.CommandLine accepts either order but lists the question first,
/// and its layout API is internal, so the default help is captured and its usage line rewritten.</summary>
internal static partial class HelpLayout
{
    private const string Usage = "  ConsoleScout [options] [<question>...]\n  csc [options] [<question>...]";

    /// <summary>The default usage line. Its program name is the process name, so it reads ConsoleScout or
    /// csc depending on how the program was started.</summary>
    [GeneratedRegex(@"^  \S+ \[<question>\.\.\.\] \[options\]$", RegexOptions.Multiline)]
    private static partial Regex DefaultUsage();

    public static void Apply(RootCommand root)
    {
        var help = root.Options.OfType<HelpOption>().First();
        if (help.Action is HelpAction inner)
        {
            help.Action = new RewritingHelpAction(inner);
        }
    }

    private sealed class RewritingHelpAction(HelpAction inner) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            var configuration = parseResult.InvocationConfiguration;
            var output = configuration.Output;

            using var captured = new StringWriter();
            configuration.Output = captured;
            int exitCode;
            try
            {
                exitCode = inner.Invoke(parseResult);
            }
            finally
            {
                configuration.Output = output;
            }

            var help = captured.ToString();
            var text = DefaultUsage().Replace(help, Usage);
            output.Write(text);
            return exitCode;
        }
    }
}
