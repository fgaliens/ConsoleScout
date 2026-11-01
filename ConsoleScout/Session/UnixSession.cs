using System.Runtime.InteropServices;

namespace ConsoleScout.Session;

internal static class UnixSession
{
    /// <summary>The Unix session id: the pid of the session leader, shared by every process started from one
    /// terminal tab or tmux pane - subshells, pipelines and scripts included.</summary>
    public static int GetLeaderId() => getsid(0);

    // Blittable signature: no marshalling, so it is NativeAOT-safe without LibraryImport.
    [DllImport("libc", EntryPoint = "getsid")]
    private static extern int getsid(int pid);
}
