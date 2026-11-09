using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WingspanAccessInstaller;

/// <summary>Finds Wingspan (Steam app 1054490) in the Steam libraries.</summary>
internal static class SteamLocator
{
    public const string AppId = "1054490";
    private const string DefaultInstallDir = "Wingspan";

    private static readonly Regex PathEntry = new("\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex InstallDirEntry = new("\"installdir\"\\s+\"(?<dir>[^\"]+)\"", RegexOptions.IgnoreCase);

    /// <summary>The game folder, or null if Wingspan is not in any Steam library.</summary>
    public static string FindGame()
    {
        foreach (string library in Libraries())
        {
            string steamapps = Path.Combine(library, "steamapps");
            string installDir = DefaultInstallDir;
            string manifest = Path.Combine(steamapps, $"appmanifest_{AppId}.acf");
            if (File.Exists(manifest))
            {
                Match match = InstallDirEntry.Match(SafeRead(manifest));
                if (match.Success)
                    installDir = match.Groups["dir"].Value;
            }

            string game = Path.Combine(steamapps, "common", installDir);
            Log.Detail($"Looking for Wingspan in {game}");
            if (File.Exists(Path.Combine(game, "Wingspan.exe")))
                return game;
        }

        return null;
    }

    /// <summary>The Steam folder and every library listed in libraryfolders.vdf.</summary>
    public static IEnumerable<string> Libraries()
    {
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (string root in SteamRoots())
        {
            if (!Directory.Exists(root))
                continue;
            if (seen.Add(Normalize(root)))
                yield return root;

            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;
            foreach (Match match in PathEntry.Matches(SafeRead(vdf)))
            {
                string library = match.Groups["path"].Value.Replace("\\\\", "\\");
                if (Directory.Exists(library) && seen.Add(Normalize(library)))
                    yield return library;
            }
        }
    }

    private static IEnumerable<string> SteamRoots()
    {
        // InstallPath keeps the real letter case; SteamPath is usually lower case.
        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) is string machinePath)
            yield return machinePath;
        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) is string machinePath64)
            yield return machinePath64;
        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string userPath)
            yield return userPath.Replace('/', '\\');
        yield return @"C:\Program Files (x86)\Steam";
        yield return @"C:\Program Files\Steam";
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd('\\');

    private static string SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (System.UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
