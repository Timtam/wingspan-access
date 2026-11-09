using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// Online games have a time limit per turn; running out of time costs karma. The game shows the time
/// left next to the player's name (OnlinePlayerNameAndTimer_HUD) and a warning icon when it gets low.
/// This reads the time for the status zone and announces the warning when it appears.
/// </summary>
internal static class OnlineTimerWatcher
{
    private static bool _warned;
    private static int _nextCheck;

    /// <summary>"Player: 1:30 left" for every turn timer the game shows.</summary>
    public static List<string> TimeLines()
    {
        var lines = new List<string>();
        foreach (OnlinePlayerNameAndTimer_HUD hud in Object.FindObjectsOfType<OnlinePlayerNameAndTimer_HUD>())
        {
            string time = TimeText(hud);
            if (time == null)
                continue;
            string name = hud._nameText == null ? null : UiText.Clean(hud._nameText.text);
            lines.Add(string.IsNullOrEmpty(name) ? Loc.Get("status.time", time) : Loc.Get("status.time_of", name, time));
        }

        return lines;
    }

    public static void Tick()
    {
        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 30;

        try
        {
            bool low = false;
            string time = null;
            foreach (OnlinePlayerNameAndTimer_HUD hud in Object.FindObjectsOfType<OnlinePlayerNameAndTimer_HUD>())
            {
                if (hud._lowTimeRemainingWarningIcon == null || !hud._lowTimeRemainingWarningIcon.gameObject.activeInHierarchy)
                    continue;
                low = true;
                time = TimeText(hud);
            }

            if (low && !_warned)
                ScreenReader.Say(Loc.Get("status.time_low", time ?? "?"));
            _warned = low;
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Online timer check failed: {e.Message}");
            _nextCheck = Time.frameCount + 600;
        }
    }

    private static string TimeText(OnlinePlayerNameAndTimer_HUD hud)
    {
        if (hud == null || hud._timeText == null || !UiText.IsShown(hud._timeText))
            return null;
        string time = UiText.Clean(hud._timeText.text);
        return time.Length == 0 ? null : time;
    }
}
