using System.Runtime.InteropServices;

namespace ConsoleScout.Session;

/// <summary>A process's start time on macOS from sysctl(KERN_PROC_PID), which, unlike Process.StartTime, also
/// works for processes of other users. That matters here: the session leader of a Terminal tab is
/// /usr/bin/login, owned by root.</summary>
internal static class MacProcessStartTime
{
    private const int CtlKern = 1;
    private const int KernProc = 14;
    private const int KernProcPid = 1;

    /// <summary>sizeof(struct kinfo_proc) on 64-bit macOS. Its first field, kp_proc.p_un.__p_starttime, is a
    /// struct timeval: 8 bytes of seconds, then 4 bytes of microseconds.</summary>
    private const int KinfoProcSize = 648;

    /// <returns>UTC ticks, or null when the process does not exist or sysctl fails.</returns>
    public static long? GetUtcTicks(int pid)
    {
        int[] name = [CtlKern, KernProc, KernProcPid, pid];
        var buffer = new byte[KinfoProcSize];
        var length = (nuint)buffer.Length;

        var result = sysctl(name, (uint)name.Length, buffer, ref length, IntPtr.Zero, 0);
        if (result != 0 || length < 12)
        {
            return null;
        }

        var seconds = BitConverter.ToInt64(buffer, 0);
        var microseconds = BitConverter.ToInt32(buffer, 8);
        if (seconds <= 0)
        {
            return null;
        }

        var started = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return started.UtcTicks + microseconds * TimeSpan.TicksPerMicrosecond;
    }

    [DllImport("libc", EntryPoint = "sysctl")]
    private static extern int sysctl(int[] name, uint nameLength, byte[] oldValue, ref nuint oldLength, IntPtr newValue, nuint newLength);
}
