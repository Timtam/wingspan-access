using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using WingspanAccess.Input;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Diagnostics;

/// <summary>
/// F4 twice opens the installer's "Report a problem" window beside the game, which keeps running:
/// WingspanAccessInstaller.exe --report --game &lt;game folder&gt; from the plugin folder. The first press
/// says what the second does. Builds without the Static Forms key have no reports, and F4 says so.
/// </summary>
internal static class ProblemReportKey
{
    private const string InstallerName = "WingspanAccessInstaller.exe";
    private const float ConfirmSeconds = 10f;

    private static float _confirmUntil = -1f;

    /// <summary>Call once per frame.</summary>
    public static void Tick()
    {
        if (!Keys.Pressed(KeyCode.F4))
            return;

        if (!BuildInfo.ProblemReports)
        {
            ScreenReader.SayTransient(Loc.Get("report.unavailable"));
            return;
        }

        if (Time.realtimeSinceStartup > _confirmUntil)
        {
            _confirmUntil = Time.realtimeSinceStartup + ConfirmSeconds;
            ScreenReader.SayTransient(Loc.Get("report.confirm"));
            return;
        }

        _confirmUntil = -1f;
        StartReportWindow();
    }

    private static void StartReportWindow()
    {
        string folder = Path.GetDirectoryName(typeof(ProblemReportKey).Assembly.Location) ?? string.Empty;
        string installer = Path.Combine(folder, InstallerName);
        if (!File.Exists(installer))
        {
            ScreenReader.SayTransient(Loc.Get("report.no_installer"));
            return;
        }

        try
        {
            var start = new ProcessStartInfo(installer) { UseShellExecute = false };
            foreach (string argument in new[] { "--report", "--game", BepInEx.Paths.GameRootPath.TrimEnd('\\', '/'), "--lang", Loc.Language })
                start.ArgumentList.Add(argument);
            Process.Start(start)?.Dispose();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"[Report] could not start the installer: {e}");
            ScreenReader.SayTransient(Loc.Get("report.start_failed"));
            return;
        }

        Plugin.Logger.LogInfo("[Report] report window started");
        ScreenReader.SayTransient(Loc.Get("report.starting"));
    }
}
