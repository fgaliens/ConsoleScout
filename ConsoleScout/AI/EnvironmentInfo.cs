using System.Globalization;
using System.Runtime.InteropServices;

namespace ConsoleScout.AI;

/// <summary>Facts about the user's machine the model cannot know on its own: the current time, which
/// training data cannot supply, and the operating system, so commands and paths fit it.</summary>
internal sealed class EnvironmentInfo(TimeProvider time)
{
    public string Describe()
    {
        var now = time.GetLocalNow();
        var offset = now.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var zone = time.LocalTimeZone.Id;

        return
            $"""
             Current local date and time: {now.ToString("yyyy-MM-dd HH:mm, dddd", CultureInfo.InvariantCulture)} (UTC{sign}{offset:hh\:mm}, {zone}).
             The user's operating system: {OperatingSystemName()}, {RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}.
             Use these instead of guessing: for "today", "now", relative dates and ages, and for commands,
             paths and shortcuts, which should fit this operating system unless the user asks for another.
             """;
    }

    private static string OperatingSystemName()
    {
        var version = Environment.OSVersion.Version;

        if (OperatingSystem.IsMacOS())
        {
            return $"macOS {version.Major}.{version.Minor}";
        }

        if (OperatingSystem.IsWindows())
        {
            return $"Windows {(version.Build >= 22000 ? 11 : 10)} (build {version.Build})";
        }

        if (OperatingSystem.IsLinux())
        {
            return $"Linux ({RuntimeInformation.OSDescription})";
        }

        return RuntimeInformation.OSDescription;
    }
}
