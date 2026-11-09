using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx.Configuration;
using UnityEngine;
using WingspanAccess.Input;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Updates;

/// <summary>
/// Asks GitHub for the latest release once the game has started and says when a newer version is
/// out. F5 checks again; F5 twice installs it: the mod starts WingspanAccessInstaller.exe from its
/// folder and closes the game, and the installer updates the mod and starts the game again.
/// Release builds know their GitHub repository (BuildInfo.RepositorySlug); local builds do not check.
/// </summary>
internal static class UpdateChecker
{
    private const string InstallerName = "WingspanAccessInstaller.exe";
    private const float ConfirmSeconds = 10f;

    private static ConfigEntry<bool> _checkOnStart;
    private static bool _startupCheckStarted;
    private static Task<Release> _pending;
    private static bool _pendingIsManual;
    private static Release _latest;
    private static string _announcedTag;
    private static float _confirmUntil = -1f;

    private static bool HasSource => !string.IsNullOrEmpty(BuildInfo.RepositorySlug);

    public static void Initialize(ConfigFile config)
    {
        _checkOnStart = config.Bind("Updates", "CheckOnStart", true,
            "Ask GitHub for a new version of Wingspan Access when the game starts. F5 checks at any time.");
    }

    /// <summary>Call once per frame.</summary>
    public static void Tick()
    {
        // Same moment as the load announcement; the result is queued after it.
        if (!_startupCheckStarted && GamepadController.I != null)
        {
            _startupCheckStarted = true;
            if (HasSource && (_checkOnStart == null || _checkOnStart.Value))
                StartCheck(manual: false);
        }

        if (_pending != null && _pending.IsCompleted)
            FinishCheck();

        if (Keys.Pressed(KeyCode.F5))
            OnF5();
    }

    private static void OnF5()
    {
        if (!HasSource)
        {
            ScreenReader.SayTransient(Loc.Get("update.unavailable"));
            return;
        }

        if (_pending != null)
        {
            ScreenReader.SayTransient(Loc.Get("update.checking"));
            return;
        }

        if (IsNewer(_latest))
        {
            if (Time.realtimeSinceStartup <= _confirmUntil)
            {
                StartInstaller();
                return;
            }

            _confirmUntil = Time.realtimeSinceStartup + ConfirmSeconds;
            ScreenReader.SayTransient(Loc.Get("update.confirm", _latest.VersionText));
            return;
        }

        ScreenReader.SayTransient(Loc.Get("update.checking"));
        StartCheck(manual: true);
    }

    private static void StartCheck(bool manual)
    {
        _pendingIsManual = manual;
        _pending = Task.Run(FetchLatestAsync);
    }

    private static void FinishCheck()
    {
        Task<Release> task = _pending;
        _pending = null;
        if (task.IsFaulted || task.IsCanceled)
        {
            Plugin.Logger.LogWarning($"[Update] check failed: {task.Exception?.GetBaseException().Message ?? "timeout"}");
            if (_pendingIsManual)
                ScreenReader.SayTransient(Loc.Get("update.failed"));
            return;
        }

        _latest = task.Result;
        Plugin.Logger.LogInfo($"[Update] latest release: {_latest?.Tag ?? "none"}, this version: {BuildInfo.Version}");
        if (IsNewer(_latest))
        {
            if (_pendingIsManual || _announcedTag != _latest.Tag)
            {
                _announcedTag = _latest.Tag;
                ScreenReader.Say(Loc.Get("update.available", _latest.VersionText), interrupt: _pendingIsManual);
            }
        }
        else if (_pendingIsManual)
        {
            ScreenReader.SayTransient(Loc.Get("update.current", BuildInfo.Version));
        }
    }

    private static void StartInstaller()
    {
        string folder = Path.GetDirectoryName(typeof(UpdateChecker).Assembly.Location) ?? string.Empty;
        string installer = Path.Combine(folder, InstallerName);
        if (!File.Exists(installer))
        {
            ScreenReader.Say(Loc.Get("update.browser"));
            OpenInBrowser(_latest.PageUrl ?? $"https://github.com/{BuildInfo.RepositorySlug}/releases/latest");
            return;
        }

        try
        {
            var start = new ProcessStartInfo(installer) { UseShellExecute = false };
            foreach (string argument in new[] { "--update", "--restart", "--game", BepInEx.Paths.GameRootPath.TrimEnd('\\', '/'), "--lang", Loc.Language })
                start.ArgumentList.Add(argument);
            Process.Start(start)?.Dispose();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"[Update] could not start the installer: {e}");
            ScreenReader.Say(Loc.Get("update.start_failed"));
            return;
        }

        Plugin.Logger.LogInfo($"[Update] installer started for {_latest.Tag}, closing the game");
        ScreenReader.Say(Loc.Get("update.starting"));
        // Right away: once the installer window has the focus, the game may stop running frames.
        Application.Quit();
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"[Update] could not open {url}: {e.Message}");
        }
    }

    private static async Task<Release> FetchLatestAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"WingspanAccess/{BuildInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        string url = $"https://api.github.com/repos/{BuildInfo.RepositorySlug}/releases/latest";
        using HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null; // no release published yet
        response.EnsureSuccessStatusCode();
        using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using JsonDocument json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        JsonElement root = json.RootElement;
        string page = root.TryGetProperty("html_url", out JsonElement pageUrl) ? pageUrl.GetString() : null;
        return new Release(root.GetProperty("tag_name").GetString(), page);
    }

    private static bool IsNewer(Release release)
    {
        Version current = ParseVersion(BuildInfo.Version);
        return release?.Version != null && current != null && release.Version > current;
    }

    /// <summary>"v1.2.3", "1.2.3-beta" or "1.2.3+abc" as a four-part Version; null if it is none.</summary>
    private static Version ParseVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        string core = text.Trim().TrimStart('v', 'V');
        int cut = core.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0)
            core = core.Substring(0, cut);
        if (!core.Contains('.'))
            core += ".0";
        if (!Version.TryParse(core, out Version version))
            return null;
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
    }

    private sealed class Release
    {
        public Release(string tag, string pageUrl)
        {
            Tag = tag;
            PageUrl = pageUrl;
            Version = ParseVersion(tag);
        }

        public string Tag { get; }
        public string PageUrl { get; }
        public Version Version { get; }
        public string VersionText => Tag?.TrimStart('v', 'V');
    }
}
