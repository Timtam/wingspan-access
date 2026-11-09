using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace WingspanAccessInstaller;

internal static class Program
{
    public const string ExeName = "WingspanAccessInstaller.exe";

    public static string ExePath { get; } = Assembly.GetExecutingAssembly().Location;

    public static Version InstallerVersion { get; } = Releases.ParseVersion(BuildInfo.Version) ?? new Version(0, 0, 0, 0);

    /// <summary>The GitHub repository ("owner/name") with the releases; empty in local builds.</summary>
    public static string Repository { get; private set; } = BuildInfo.RepositorySlug;

    [STAThread]
    private static int Main(string[] args)
    {
        // .NET Framework 4.8 uses the system's TLS settings; only very old setups need TLS 1.2 added.
        if (ServicePointManager.SecurityProtocol != 0)
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(Options.Usage);
            return 2;
        }

        Strings.SetLanguage(options.Language);
        Repository = options.Repository ?? string.Empty;

        if (!options.Relaunched && TryRelaunchFromTemp(options))
            return 0;

        Log.Start(args);
        foreach (string unknown in options.Unknown)
            Log.Detail($"Ignoring unknown argument {unknown}");

        if (options.Silent)
            return SilentRunner.Run(options);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (options.Uninstall)
            return UninstallDialogs.Run(options);

        Application.Run(new MainForm(options));
        return 0;
    }

    /// <summary>
    /// The copy next to the mod cannot replace or delete itself. It starts a copy of itself from
    /// %TEMP% with the same arguments and ends. Returns false if it should just run from here.
    /// </summary>
    private static bool TryRelaunchFromTemp(Options options)
    {
        string dir = Path.GetDirectoryName(ExePath);
        if (!GameInstall.IsPluginFolder(dir))
            return false;

        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "WingspanAccessInstaller");
            Directory.CreateDirectory(tempDir);
            string copy = Path.Combine(tempDir, ExeName);
            try
            {
                File.Copy(ExePath, copy, true);
            }
            catch (IOException)
            {
                // An earlier copy is still running.
                copy = Path.Combine(tempDir, $"WingspanAccessInstaller-{Guid.NewGuid():N}.exe");
                File.Copy(ExePath, copy);
            }

            Start(copy, options.ToArguments(options.GameDir ?? GameInstall.GameDirOfPluginFolder(dir), "--relaunched"));
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Starts another installer process and lets it take the foreground.</summary>
    public static void Start(string exe, IEnumerable<string> arguments)
    {
        string commandLine = Options.Join(arguments);
        Log.Detail($"Starting {exe} {commandLine}");
        NativeMethods.AllowForegroundForNextProcess();
        Process.Start(new ProcessStartInfo(exe, commandLine) { UseShellExecute = false })?.Dispose();
    }

    /// <summary>Starts this installer again with administrator rights. False if the user said no.</summary>
    public static bool RestartElevated(Options options, string gameDir)
    {
        try
        {
            var start = new ProcessStartInfo(ExePath, Options.Join(options.ToArguments(gameDir, "--relaunched")))
            {
                UseShellExecute = true,
                Verb = "runas",
            };
            Process.Start(start)?.Dispose();
            return true;
        }
        catch (Win32Exception e)
        {
            Log.Detail($"No administrator rights: {e.Message}");
            return false;
        }
    }

    /// <summary>True if the release brings a newer installer than this one.</summary>
    public static bool HasNewerInstaller(ReleaseInfo release, Options options) =>
        !options.NoSelfUpdate && release?.Installer != null && release.Version != null && release.Version > InstallerVersion;

    /// <summary>Downloads the release's installer and hands over to it with the same arguments.</summary>
    public static void StartNewerInstaller(ReleaseInfo release, Options options, string gameDir, Action<int> progress, CancellationToken token)
    {
        string exe = Path.Combine(GameInstall.NewTempDir("WingspanAccessInstaller"), ExeName);
        Log.Info(Strings.Get("downloading", release.Installer.Name));
        Releases.Download(release.Installer.DownloadUrl, exe, progress, token);
        Log.Info(Strings.Get("self_updating"));
        Start(exe, options.ToArguments(gameDir, "--relaunched", "--no-self-update"));
    }

    /// <summary>Starts Wingspan through Steam, or directly if Steam does not answer.</summary>
    public static void StartGame(string gameDir)
    {
        Log.Info(Strings.Get("starting_game"));
        try
        {
            Process.Start(new ProcessStartInfo($"steam://rungameid/{SteamLocator.AppId}") { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception e)
        {
            Log.Detail($"Steam did not start the game: {e.Message}");
            Process.Start(new ProcessStartInfo(Path.Combine(gameDir, "Wingspan.exe")) { UseShellExecute = true, WorkingDirectory = gameDir })?.Dispose();
        }
    }
}
