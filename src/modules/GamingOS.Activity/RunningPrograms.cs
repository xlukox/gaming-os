using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GamingOS.Activity;

/// <summary>Full paths of the programs running right now.</summary>
public static class RunningPrograms
{
    // Only asks Windows for the program's path. Never opens a game's memory, so it is safe next to anti-cheat.
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public static IEnumerable<string> Paths()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (PathOf(process.Id) is { } path) yield return path;
            }
        }
    }

    /// <summary>The program's path, or null for processes Windows does not let us look at (system, protected).</summary>
    public static string? PathOf(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new char[1024];
            var length = buffer.Length;
            return QueryFullProcessImageName(handle, 0, buffer, ref length) ? new string(buffer, 0, length) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, [Out] char[] path, ref int size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
