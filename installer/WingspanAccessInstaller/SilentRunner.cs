using System;
using System.IO;
using System.Text;
using System.Threading;

namespace WingspanAccessInstaller;

/// <summary>
/// "--silent": no window, messages to the standard output and the log file. Exit codes: 0 done,
/// 1 failed, 2 wrong arguments or no game folder, 3 the game is running.
/// </summary>
internal static class SilentRunner
{
    public static int Run(Options options)
    {
        // UTF-8 for scripts that read the output; a window program has no console code page to use.
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
        Log.Progress += Console.WriteLine;
        try
        {
            if (options.Check)
                return Check();

            string game = GameInstall.Locate(options.GameDir);
            if (options.Detect)
                return Detect(game);
            if (game == null)
                throw new InstallerException(Strings.Get("game_missing"), 2);

            if (options.Install)
                return Install(options, game);

            if (options.Uninstall)
            {
                GameInstall.Uninstall(game, options.RemoveBepInEx, !options.NoRegistry);
                Log.Info(Strings.Get(options.RemoveBepInEx ? "done_uninstall_bepinex" : "done_uninstall"));
                return 0;
            }

            Console.Error.WriteLine(Options.Usage);
            return 2;
        }
        catch (InstallerException e)
        {
            Log.Info(e.Message);
            return e.ExitCode;
        }
        catch (Exception e)
        {
            Log.Error(e);
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    private static int Check()
    {
        if (string.IsNullOrEmpty(Program.Repository))
            throw new InstallerException(Strings.Get("latest_unknown", Strings.Get("no_repo")), 2);
        ReleaseInfo release = Releases.Latest(Program.Repository);
        if (release == null)
        {
            Log.Info(Strings.Get("latest_none"));
            return 1;
        }

        Log.Info(Strings.Get("latest", release.VersionText));
        Console.WriteLine($"Tag: {release.Tag}");
        Console.WriteLine($"Package: {release.Package?.Name ?? "-"} {release.Package?.DownloadUrl}");
        Console.WriteLine($"Installer: {release.Installer?.Name ?? "-"} {release.Installer?.DownloadUrl}");
        Console.WriteLine($"Page: {release.PageUrl}");
        Console.WriteLine($"Newer than this installer ({BuildInfo.Version}): {release.Version > Program.InstallerVersion}");
        return 0;
    }

    private static int Detect(string game)
    {
        if (game == null)
        {
            Log.Info(Strings.Get("game_missing"));
            return 1;
        }

        Console.WriteLine($"Game: {game}");
        Console.WriteLine($"Wingspan.exe: {(GameInstall.IsGameFolder(game) ? "found" : "missing")}");
        Console.WriteLine($"Wingspan Access: {GameInstall.InstalledModVersion(game) ?? "not installed"}");
        Console.WriteLine($"BepInEx: {GameInstall.BepInExVersion(game) ?? "not installed"}");
        Console.WriteLine($"Running: {GameInstall.IsGameRunning(game)}");
        return GameInstall.IsGameFolder(game) ? 0 : 1;
    }

    private static int Install(Options options, string game)
    {
        ReleaseInfo release = null;
        if (string.IsNullOrEmpty(options.ZipPath) && !string.IsNullOrEmpty(Program.Repository))
        {
            try
            {
                release = Releases.Latest(Program.Repository);
            }
            catch (Exception e) when (Releases.IsCheckFailure(e))
            {
                Log.Info(Strings.Get("latest_unknown", Strings.Get("check_failed", e.Message)));
            }
        }

        using Package package = PackageSource.Get(options.ZipPath, release, null, CancellationToken.None);
        string version = GameInstall.Install(new InstallRequest
        {
            GameDir = game,
            PackageZip = package.Zip,
            WriteRegistry = !options.NoRegistry,
            ReplaceOtherBepInEx = (_, _) => options.ReplaceBepInEx,
        });
        Log.Info(Strings.Get("installed_short", version));
        return 0;
    }
}
