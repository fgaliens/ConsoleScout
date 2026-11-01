using System.ComponentModel;
using System.Diagnostics;

namespace ConsoleScout.Session;

/// <summary>Which terminal session a question belongs to, so csc -c follows up on questions from the same
/// terminal only. On Unix that is the session leader of the terminal tab (getsid), so subshells and pipelines
/// stay in it; on Windows the process that started csc. The leader's start time is part of the key, since
/// process ids get reused. CONSOLESCOUT_SESSION overrides it, to share or separate sessions on purpose.</summary>
internal static class TerminalSession
{
    /// <summary>Used when the parent shell cannot be identified: all such runs share one session.</summary>
    public const string Shared = "default";

    /// <summary>Overrides the detected session. Not a setting: the configuration loader skips it.</summary>
    public const string Variable = "CONSOLESCOUT_SESSION";

    private static readonly Lazy<string> Resolved = new(Resolve);

    public static string Id => Resolved.Value;

    public static bool IsShared => Id == Shared;

    private static string Resolve()
    {
        var configured = Environment.GetEnvironmentVariable(Variable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        try
        {
            var leader = OperatingSystem.IsWindows() ? WindowsParentProcess.GetId() : UnixSession.GetLeaderId();
            if (leader <= 1)
            {
                return Shared;
            }

            using var process = Process.GetProcessById(leader);
            var started = process.StartTime.ToUniversalTime().Ticks;
            return $"{leader}-{started}";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception
            or NotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            return Shared;
        }
    }
}
