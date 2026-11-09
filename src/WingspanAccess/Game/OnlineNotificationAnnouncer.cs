using System;
using System.Collections.Concurrent;
using HarmonyLib;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// Asynchronous online games give each player up to 72 hours per turn. When it is your turn in
/// another online game, or your time there runs low, the game shows a notification at the top of the
/// screen for a few seconds ("Du bist in einem Online-Spiel am Zug!"), in the menus and during
/// other games, as set under "Online notifications" in the game settings. This reads it with the
/// game's mode and players, and the online menu tells in how many games it is your turn.
/// </summary>
internal static class OnlineNotificationAnnouncer
{
    private static readonly ConcurrentQueue<(string MatchId, SessionNotificationCodes Type)> Pending = new();

    private static int _onlineMenuId;
    private static int _countFrame;
    private static int _lastYourTurnCount = -1;

    public static void Report(InGameNotification notification)
    {
        if (notification != null)
            Pending.Enqueue((notification.MatchId, notification.NotificationType));
    }

    public static void Tick()
    {
        while (Pending.TryDequeue(out var notification))
            Announce(notification.MatchId, notification.Type);
        WatchOnlineMenu();
    }

    private static void Announce(string matchId, SessionNotificationCodes type)
    {
        string term = type switch
        {
            SessionNotificationCodes.MatchmakingTurnStarted => "UI/Online/Notification/YourTurn",
            SessionNotificationCodes.MatchmakingLowTimeRemaining => "UI/Online/Notification/TimeRunningOut",
            _ => null,
        };
        if (term == null)
            return;

        string text = I2.Loc.LocalizationManager.GetTranslation(term, true, 0, true, false, null, null, true);
        string game = null;
        try
        {
            game = SaveDescriber.ModeAndPlayers(NakamaGod.I?.GetOnlineMatchSave(matchId));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Online notification game {matchId}: {e.Message}");
        }

        Plugin.Logger.LogInfo($"[Notification] {type} for {matchId}: {game}");
        ScreenReader.Say(UiText.JoinSentences(new[] { UiText.Clean(text ?? string.Empty), game, Loc.Get("online.notification_where") }), interrupt: false);
    }

    /// <summary>In the online menu: how many of the active games wait for your move, once the list is loaded.</summary>
    private static void WatchOnlineMenu()
    {
        GamepadScreen top = Screens.Top();
        if (top == null || top.TryCast<OnlineMenu_Screen>() == null)
        {
            _onlineMenuId = 0;
            return;
        }

        if (top.GetInstanceID() != _onlineMenuId)
        {
            _onlineMenuId = top.GetInstanceID();
            _countFrame = Time.frameCount + 60;
            _lastYourTurnCount = -1;
            return;
        }

        if (Time.frameCount < _countFrame)
            return;
        _countFrame = Time.frameCount + 60;

        int count = 0;
        foreach (MatchUIOnline match in UnityEngine.Object.FindObjectsOfType<MatchUIOnline>())
        {
            if (match.TryCast<MatchUIOnlineInvitation>() != null || match.TryCast<MatchUIOnlineFinished>() != null ||
                match.TryCast<MatchUIOnlineTimeout>() != null)
                continue;
            if (match.yourTurn != null && match.yourTurn.isActiveAndEnabled)
                count++;
        }

        // Said when the menu has loaded the list and whenever a game more waits for you.
        if (count > _lastYourTurnCount && count > 0)
            ScreenReader.Say(Loc.Count("online.your_turn_count", count), interrupt: false);
        _lastYourTurnCount = count;
    }
}

/// <summary>The notification panel at the top of the screen (shown according to the game's settings).</summary>
[HarmonyPatch(typeof(InGameNotification_HUD), nameof(InGameNotification_HUD.ShowNotification))]
internal static class OnlineNotificationPatch
{
    private static void Prefix(InGameNotification notification) => OnlineNotificationAnnouncer.Report(notification);
}
