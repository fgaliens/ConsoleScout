using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConsoleScout.Session;

internal static class WindowsParentProcess
{
    private const int ProcessBasicInformationClass = 0;

    /// <returns>The parent's process id, or 0 when Windows does not say.</returns>
    public static int GetId()
    {
        using var current = Process.GetCurrentProcess();
        var information = new ProcessBasicInformation();
        var size = Marshal.SizeOf<ProcessBasicInformation>();

        var status = NtQueryInformationProcess(current.Handle, ProcessBasicInformationClass, ref information, size, out _);
        return status == 0 ? information.InheritedFromUniqueProcessId.ToInt32() : 0;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);
}
