using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace WingspanAccessInstaller;

/// <summary>An expected problem, with a message for the user.</summary>
internal sealed class InstallerException : Exception
{
    public InstallerException(string message, int exitCode = 1)
        : base(message)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

/// <summary>What to install where.</summary>
internal sealed class InstallRequest
{
    public string GameDir { get; set; }
    public string PackageZip { get; set; }
    public bool WriteRegistry { get; set; } = true;

    /// <summary>Asked when another BepInEx version is installed: (installed, packaged) → replace it?</summary>
    public Func<string, string, bool> ReplaceOtherBepInEx { get; set; }
}

/// <summary>Installs and removes BepInEx and Wingspan Access in a game folder.</summary>
internal static class GameInstall
{
    public const string PluginFolderName = "WingspanAccess";
    public const string PluginFileName = "WingspanAccess.dll";
    public const string GameProcessName = "Wingspan";

    /// <summary>A line only BepInEx's own changelog.txt has; other files of that name stay.</summary>
    private const string BepInExChangelogMarker = "Changelog (excluding merge commits)";

    private static readonly string[] BepInExRootFiles = { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" };

    private static readonly (string Section, string Key, string Value)[] RequiredSettings =
    {
        // The console window would take the focus away from the game.
        ("Logging.Console", "Enabled", "false"),
        // Unity's messages and a log that is complete after a crash make bug reports useful.
        ("Logging.Disk", "WriteUnityLog", "true"),
        ("Logging.Disk", "InstantFlushing", "true"),
    };

    public static string PluginDir(string gameDir) => Path.Combine(gameDir, "BepInEx", "plugins", PluginFolderName);

    public static bool IsGameFolder(string dir) =>
        !string.IsNullOrEmpty(dir) && Path.IsPathRooted(dir) && File.Exists(Path.Combine(dir, "Wingspan.exe"));

    /// <summary>True for a BepInEx\plugins\WingspanAccess folder.</summary>
    public static bool IsPluginFolder(string dir)
    {
        var plugin = new DirectoryInfo(dir);
        return string.Equals(plugin.Name, PluginFolderName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(plugin.Parent?.Name, "plugins", StringComparison.OrdinalIgnoreCase)
            && string.Equals(plugin.Parent?.Parent?.Name, "BepInEx", StringComparison.OrdinalIgnoreCase)
            && plugin.Parent.Parent.Parent != null;
    }

    public static string GameDirOfPluginFolder(string pluginDir) => new DirectoryInfo(pluginDir).Parent.Parent.Parent.FullName;

    /// <summary>A full path without a trailing backslash (except for a drive root).</summary>
    public static string NormalizeDir(string dir)
    {
        string trimmed = Path.GetFullPath(dir.Trim().Trim('"')).TrimEnd('\\', '/');
        return trimmed.EndsWith(":") ? trimmed + "\\" : trimmed;
    }

    /// <summary>The game folder from the command line, the last installation or the Steam libraries; null if unknown.</summary>
    public static string Locate(string requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return NormalizeDir(requested);
        string registered = UninstallEntry.RegisteredGameDir();
        if (IsGameFolder(registered))
            return registered;
        string found = SteamLocator.FindGame();
        return found == null ? null : NormalizeDir(found);
    }

    /// <summary>True if Wingspan runs from this folder (or from a folder Windows does not tell).</summary>
    public static bool IsGameRunning(string gameDir)
    {
        List<Process> processes = GameProcesses(gameDir);
        processes.ForEach(process => process.Dispose());
        return processes.Count > 0;
    }

    /// <summary>Asks the game to close, like its close button.</summary>
    public static void CloseGame(string gameDir)
    {
        foreach (Process process in GameProcesses(gameDir))
        {
            using (process)
            {
                Log.Detail($"Asking Wingspan (process {process.Id}) to close");
                process.CloseMainWindow();
            }
        }
    }

    /// <summary>The product version of a file without build details ("6.0.0-be.793"), or null.</summary>
    public static string VersionText(string file)
    {
        if (!File.Exists(file))
            return null;
        string version = FileVersionInfo.GetVersionInfo(file).ProductVersion;
        if (string.IsNullOrWhiteSpace(version))
            return null;
        int plus = version.IndexOf('+');
        return (plus >= 0 ? version.Substring(0, plus) : version).Trim();
    }

    public static string InstalledModVersion(string gameDir) => VersionText(Path.Combine(PluginDir(gameDir), PluginFileName));

    public static string BepInExVersion(string root) => VersionText(Path.Combine(root, "BepInEx", "core", "BepInEx.Core.dll"));

    /// <summary>Installs the package and returns the installed mod version.</summary>
    public static string Install(InstallRequest request)
    {
        string game = request.GameDir;
        if (!IsGameFolder(game))
            throw new InstallerException(Strings.Get("not_game_folder", game), 2);
        if (IsGameRunning(game))
            throw new InstallerException(Strings.Get("game_running"), 3);

        string work = NewTempDir("WingspanAccessPackage");
        try
        {
            Log.Info(Strings.Get("extracting"));
            ZipFile.ExtractToDirectory(request.PackageZip, work);
            string package = PackageRoot(work);
            string packagedPlugin = Path.Combine(package, "BepInEx", "plugins", PluginFolderName);
            string version = VersionText(Path.Combine(packagedPlugin, PluginFileName));
            if (version == null)
                throw new InstallerException(Strings.Get("not_a_package", Path.GetFileName(request.PackageZip)));

            InstallBepInEx(package, game, request.ReplaceOtherBepInEx);

            Log.Info(Strings.Get("installing_mod", version));
            string pluginDir = PluginDir(game);
            DeleteDirectory(pluginDir);
            CopyDirectory(packagedPlugin, pluginDir);

            Configure(package, game);
            CopySelf(pluginDir);
            if (request.WriteRegistry)
                UninstallEntry.Write(game, version);
            return version;
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    /// <summary>Removes the mod and, if asked, BepInEx with everything in it.</summary>
    public static void Uninstall(string game, bool removeBepInEx, bool writeRegistry)
    {
        if (string.IsNullOrEmpty(game) || !Path.IsPathRooted(game))
            throw new InstallerException(Strings.Get("not_game_folder", game), 2);

        string pluginDir = PluginDir(game);
        if (!IsGameFolder(game) && !Directory.Exists(pluginDir))
        {
            // The game was moved or removed; only the entry under Installed apps is left.
            Log.Info(Strings.Get("nothing_installed", game));
            if (writeRegistry)
                UninstallEntry.Remove();
            return;
        }

        if (IsGameRunning(game))
            throw new InstallerException(Strings.Get("game_running"), 3);

        if (Directory.Exists(pluginDir))
        {
            Log.Info(Strings.Get("removing", "Wingspan Access"));
            DeleteDirectory(pluginDir);
        }

        if (removeBepInEx)
        {
            Log.Info(Strings.Get("removing", "BepInEx"));
            DeleteDirectory(Path.Combine(game, "BepInEx"));
            DeleteDirectory(Path.Combine(game, "dotnet"));
            foreach (string file in BepInExRootFiles)
                DeleteFile(Path.Combine(game, file));
            string changelog = Path.Combine(game, "changelog.txt");
            if (File.Exists(changelog) && File.ReadAllText(changelog).Contains(BepInExChangelogMarker))
                DeleteFile(changelog);
        }

        if (writeRegistry)
            UninstallEntry.Remove();
    }

    /// <summary>Sets the given keys in a BepInEx config file and keeps everything else as it is.</summary>
    public static string PatchConfig(string text, IEnumerable<(string Section, string Key, string Value)> settings)
    {
        string newline = text.Contains("\r\n") ? "\r\n" : text.Contains("\n") ? "\n" : Environment.NewLine;
        List<string> lines = text.Length == 0 ? new List<string>() : text.Replace("\r\n", "\n").Split('\n').ToList();

        foreach ((string section, string key, string value) in settings)
        {
            string entry = $"{key} = {value}";
            int start = -1;
            int end = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]"))
                    continue;
                if (start >= 0)
                {
                    end = i;
                    break;
                }
                if (string.Equals(trimmed.Substring(1, trimmed.Length - 2).Trim(), section, StringComparison.OrdinalIgnoreCase))
                    start = i;
            }

            if (start < 0)
            {
                while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
                    lines.RemoveAt(lines.Count - 1);
                if (lines.Count > 0)
                    lines.Add(string.Empty);
                lines.AddRange(new[] { $"[{section}]", string.Empty, entry, string.Empty });
                continue;
            }

            int found = -1;
            for (int i = start + 1; i < end && found < 0; i++)
            {
                string trimmed = lines[i].Trim();
                int equals = trimmed.IndexOf('=');
                if (!trimmed.StartsWith("#") && equals > 0 && string.Equals(trimmed.Substring(0, equals).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    found = i;
            }

            if (found >= 0)
            {
                lines[found] = entry;
                continue;
            }

            int insertAt = end;
            while (insertAt - 1 > start && lines[insertAt - 1].Trim().Length == 0)
                insertAt--;
            lines.InsertRange(insertAt, new[] { string.Empty, entry });
        }

        string result = string.Join(newline, lines);
        return result.EndsWith(newline) ? result : result + newline;
    }

    public static string NewTempDir(string prefix)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void TryDeleteDirectory(string dir)
    {
        try
        {
            DeleteDirectory(dir);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Log.Detail($"Could not delete {dir}: {e.Message}");
        }
    }

    private static void InstallBepInEx(string package, string game, Func<string, string, bool> replaceOther)
    {
        string packaged = BepInExVersion(package);
        if (packaged == null)
        {
            Log.Detail("The package has no BepInEx");
            return;
        }

        string installed = BepInExVersion(game);
        if (installed != null && !string.Equals(installed, packaged, StringComparison.OrdinalIgnoreCase))
        {
            if (replaceOther == null || !replaceOther(installed, packaged))
            {
                Log.Info(Strings.Get("keeping_bepinex", installed));
                return;
            }

            // Files of the other version; BepInEx generates interop and cache again on the next start.
            foreach (string dir in new[] { @"BepInEx\core", @"BepInEx\interop", @"BepInEx\cache", "dotnet" })
                DeleteDirectory(Path.Combine(game, dir));
        }
        else if (installed != null && File.Exists(Path.Combine(game, "winhttp.dll"))
            && File.Exists(Path.Combine(game, "doorstop_config.ini")) && Directory.Exists(Path.Combine(game, "dotnet")))
        {
            Log.Info(Strings.Get("keeping_bepinex", installed));
            return;
        }

        Log.Info(Strings.Get("installing_bepinex", packaged));
        foreach (string file in BepInExRootFiles)
        {
            string source = Path.Combine(package, file);
            if (File.Exists(source))
                CopyFile(source, Path.Combine(game, file));
        }

        CopyDirectory(Path.Combine(package, "dotnet"), Path.Combine(game, "dotnet"));
        CopyDirectory(Path.Combine(package, "BepInEx", "core"), Path.Combine(game, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins"));
    }

    private static void Configure(string package, string game)
    {
        Log.Info(Strings.Get("config"));
        string target = Path.Combine(game, "BepInEx", "config", "BepInEx.cfg");
        string packaged = Path.Combine(package, "BepInEx", "config", "BepInEx.cfg");
        string text = File.Exists(target) ? File.ReadAllText(target)
            : File.Exists(packaged) ? File.ReadAllText(packaged)
            : string.Empty;
        string patched = PatchConfig(text, RequiredSettings);
        if (File.Exists(target) && patched == text)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        // UTF-8 without byte order mark, as BepInEx writes it.
        File.WriteAllText(target, patched);
        Log.Detail($"Wrote {target}");
    }

    /// <summary>Puts this installer next to the mod, so the mod can start it for updates.</summary>
    private static void CopySelf(string pluginDir)
    {
        string target = Path.Combine(pluginDir, Program.ExeName);
        if (string.Equals(Path.GetFullPath(Program.ExePath), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            return;
        Version packaged = Releases.ParseVersion(VersionText(target));
        if (packaged != null && packaged > Program.InstallerVersion)
        {
            Log.Detail($"Keeping the newer installer {packaged} from the package");
            return;
        }

        CopyFile(Program.ExePath, target);
    }

    /// <summary>The folder with BepInEx in it, also when the ZIP has one folder around everything.</summary>
    private static string PackageRoot(string extracted)
    {
        if (Directory.Exists(Path.Combine(extracted, "BepInEx")))
            return extracted;
        string[] dirs = Directory.GetDirectories(extracted);
        return dirs.Length == 1 && Directory.Exists(Path.Combine(dirs[0], "BepInEx")) ? dirs[0] : extracted;
    }

    private static List<Process> GameProcesses(string gameDir)
    {
        string exe = string.IsNullOrEmpty(gameDir) ? null : Path.GetFullPath(Path.Combine(gameDir, "Wingspan.exe"));
        var result = new List<Process>();
        foreach (Process process in Process.GetProcessesByName(GameProcessName))
        {
            string path = NativeMethods.ProcessImagePath(process.Id);
            if (exe == null || path == null || string.Equals(Path.GetFullPath(path), exe, StringComparison.OrdinalIgnoreCase))
                result.Add(process);
            else
                process.Dispose();
        }

        return result;
    }

    private static void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
            CopyFile(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (string dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }

    private static void CopyFile(string source, string target)
    {
        Retry(() =>
        {
            if (File.Exists(target))
                File.SetAttributes(target, FileAttributes.Normal);
            File.Copy(source, target, true);
        });
        Log.Detail($"Copied {target}");
    }

    private static void DeleteDirectory(string dir)
    {
        if (!Directory.Exists(dir))
            return;
        Retry(() =>
        {
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(dir, true);
        });
        Log.Detail($"Deleted {dir}");
    }

    private static void DeleteFile(string file)
    {
        if (!File.Exists(file))
            return;
        Retry(() =>
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        });
        Log.Detail($"Deleted {file}");
    }

    /// <summary>Virus scanners and a game that is just closing hold files for a moment.</summary>
    private static void Retry(Action action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (attempt < 6)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException) when (attempt < 3)
            {
                Thread.Sleep(500);
            }
        }
    }
}
