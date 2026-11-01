using System.Runtime.InteropServices;

namespace ConsoleScout.Session;

/// <summary>PROCESS_BASIC_INFORMATION from ntdll; only <see cref="InheritedFromUniqueProcessId"/> is read.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProcessBasicInformation
{
    public IntPtr ExitStatus;
    public IntPtr PebBaseAddress;
    public IntPtr AffinityMask;
    public IntPtr BasePriority;
    public IntPtr UniqueProcessId;
    public IntPtr InheritedFromUniqueProcessId;
}
