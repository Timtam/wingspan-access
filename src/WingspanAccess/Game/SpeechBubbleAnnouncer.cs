using System;
using System.Collections.Concurrent;
using HarmonyLib;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// In online games the other players' moves arrive all at once; the game then sums each of them up
/// in a speech bubble at the player's portrait ("Ich habe einen Vogel gespielt: Baumschwalm im
/// Feuchtgebiet."). This reads those bubbles right away; the opponent watcher adds the details (food,
/// eggs, cards) when the local player's turn begins. Against the AI the watcher reads each turn as
/// it happens, so the bubbles are only read online.
/// </summary>
internal static class SpeechBubbleAnnouncer
{
    private static readonly ConcurrentQueue<(string PlayerId, string Text)> Pending = new();

    public static void Report(string playerId, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            Pending.Enqueue((playerId, text));
    }

    public static void Tick()
    {
        while (Pending.TryDequeue(out var bubble))
        {
            if (!IsOnline())
                continue;
            Rules.Player player = GameStateCache.State?.GetPlayer(bubble.PlayerId);
            if (player != null && player.IsLocalPlayer())
                continue;
            string text = UiText.Clean(bubble.Text);
            if (text.Length == 0)
                continue;

            Plugin.Logger.LogInfo($"[Bubble] {bubble.PlayerId}: {text}");
            ScreenReader.Say(player == null ? text : Loc.Get("opponent.says", GameReader.PlayerName(player), text), interrupt: false);
        }
    }

    private static bool IsOnline()
    {
        try
        {
            return GameLoader.Instance != null && GameLoader.Instance.IsOnlineGameLoaded;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

[HarmonyPatch(typeof(LocalGameStateView), nameof(LocalGameStateView.ShowPlayerSpeechBubble))]
internal static class PlayerSpeechBubblePatch
{
    private static void Prefix(string playerId, string text) => SpeechBubbleAnnouncer.Report(playerId, text);
}
