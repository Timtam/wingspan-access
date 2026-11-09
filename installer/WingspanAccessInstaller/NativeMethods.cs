using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WingspanAccessInstaller;

internal static class NativeMethods
{
    private const int ProcessQueryLimitedInformation = 0x1000;
    private const int AllowAnyProcess = -1;

    /// <summary>Lets the installer copy started next take the foreground, so screen readers follow it.</summary>
    public static void AllowForegroundForNextProcess() => AllowSetForegroundWindow(AllowAnyProcess);

    /// <summary>The full path of a process's EXE, also for 32-bit processes; null if Windows does not tell.</summary>
    public static string ProcessImagePath(int processId)
    {
        IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
            return null;
        try
        {
            var buffer = new StringBuilder(1024);
            int size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
