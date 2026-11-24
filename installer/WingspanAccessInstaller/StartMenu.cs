using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace WingspanAccessInstaller;

/// <summary>
/// The Start menu entry "Wingspan Access - Report a problem" (for the current user), so a report can be
/// sent when the game does not start at all. Only builds with a form key make it.
/// </summary>
internal static class StartMenu
{
    private static readonly string[] AllNames = { "Wingspan Access - Report a problem", "Wingspan Access – Problem melden" };

    private static string Folder => Environment.GetFolderPath(Environment.SpecialFolder.Programs);

    /// <summary>Writes the entry after an installation, or removes it if this build cannot send reports.</summary>
    public static void UpdateReportShortcut(string installerExe, string gameDir)
    {
        try
        {
            RemoveReportShortcuts();
            if (!ReportKey.Available)
                return;
            string path = Path.Combine(Folder, Strings.Get("report_shortcut") + ".lnk");
            var link = (IShellLinkW)new ShellLink();
            try
            {
                link.SetPath(installerExe);
                link.SetArguments($"--report --game {Options.Quote(gameDir)}");
                link.SetWorkingDirectory(Path.GetDirectoryName(installerExe));
                link.SetDescription(Strings.Get("report_title"));
                ((IPersistFile)link).Save(path, true);
            }
            finally
            {
                Marshal.FinalReleaseComObject(link);
            }

            Log.Detail($"Wrote the Start menu entry {path}");
        }
        catch (Exception e) when (e is COMException || e is IOException || e is UnauthorizedAccessException || e is InvalidCastException)
        {
            // Nice to have; the installation itself is fine without it.
            Log.Detail($"No Start menu entry: {e.Message}");
        }
    }

    public static void RemoveReportShortcuts()
    {
        foreach (string name in AllNames)
        {
            string path = Path.Combine(Folder, name + ".lnk");
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Log.Detail($"Removed {path}");
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Detail($"Could not remove {path}: {e.Message}");
            }
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, int flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxIconPath, out int icon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int icon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, int reserved);
        void Resolve(IntPtr hwnd, int flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
