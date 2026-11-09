using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace WingspanAccessInstaller;

/// <summary>The entry under "Installed apps", for the current user (no administrator rights needed).</summary>
internal static class UninstallEntry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WingspanAccess";
    private const string GameFolderValue = "WingspanFolder";

    public static void Write(string gameDir, string version)
    {
        string pluginDir = GameInstall.PluginDir(gameDir);
        string exe = Path.Combine(pluginDir, Program.ExeName);
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue("DisplayName", "Wingspan Access");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "Wingspan Access");
        key.SetValue("DisplayIcon", Path.Combine(gameDir, "Wingspan.exe"));
        key.SetValue("InstallLocation", pluginDir);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        key.SetValue("EstimatedSize", (int)(DirectorySize(pluginDir) / 1024), RegistryValueKind.DWord);
        key.SetValue("UninstallString", $"{Options.Quote(exe)} --uninstall --game {Options.Quote(gameDir)}");
        key.SetValue("QuietUninstallString", $"{Options.Quote(exe)} --silent --uninstall --game {Options.Quote(gameDir)}");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue(GameFolderValue, gameDir);
        if (!string.IsNullOrEmpty(Program.Repository))
        {
            string url = $"https://github.com/{Program.Repository}";
            key.SetValue("URLInfoAbout", url);
            key.SetValue("HelpLink", url);
            key.SetValue("URLUpdateInfo", url + "/releases");
        }

        Log.Detail($@"Wrote HKCU\{KeyPath}");
    }

    public static void Remove()
    {
        Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
        Log.Detail($@"Removed HKCU\{KeyPath}");
    }

    /// <summary>The game folder of the last installation, or null.</summary>
    public static string RegisteredGameDir()
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(GameFolderValue) as string;
    }

    private static long DirectorySize(string dir) =>
        Directory.Exists(dir) ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length) : 0;
}
