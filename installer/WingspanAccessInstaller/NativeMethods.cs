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

    /// <summary>The user's AppData\LocalLow folder, where Unity games keep their logs.</summary>
    public static bool TryGetLocalLowFolder(out string path)
    {
        path = null;
        Guid localAppDataLow = new("A520A1A4-1780-4FF6-BD18-167343C5AF16");
        int status = SHGetKnownFolderPath(ref localAppDataLow, 0, IntPtr.Zero, out IntPtr result);
        try
        {
            if (status == 0 && result != IntPtr.Zero)
                path = Marshal.PtrToStringUni(result);
            return !string.IsNullOrEmpty(path);
        }
        finally
        {
            // Windows asks for this whether the call worked or not.
            if (result != IntPtr.Zero)
                Marshal.FreeCoTaskMem(result);
        }
    }

    /// <summary>Restores the window if it is minimized and makes it the foreground window.</summary>
    public static bool BringToFront(IntPtr window)
    {
        const int Restore = 9;
        if (IsIconic(window))
            ShowWindow(window, Restore);
        return SetForegroundWindow(window);
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

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
