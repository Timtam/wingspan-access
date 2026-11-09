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

    /// <summary>The last bubble read per player and when, so a bubble passing two hooks is read once.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, (string Text, float Time)> LastRead = new();

    public static void Report(string playerId, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            Pending.Enqueue((playerId, text));
    }

    /// <summary>A bubble shown by the bubble itself: its player is the header it belongs to.</summary>
    public static void Report(HeaderSpeechBubble bubble, string text)
    {
        PlayerHeaderButton header = bubble == null ? null : bubble.GetComponentInParent<PlayerHeaderButton>();
        Report(header?.ID, text);
    }

    public static void Tick()
    {
        while (Pending.TryDequeue(out var bubble))
        {
            Rules.Player player = GameStateCache.State?.GetPlayer(bubble.PlayerId);
            // The local player's own bubbles and the AI's (whose turns the opponent watcher reads in
            // more detail) stay silent.
            if (player == null ? !IsOnline() : player.IsLocalPlayer() || player.IsAI())
                continue;
            string text = UiText.Clean(bubble.Text);
            if (text.Length == 0)
                continue;
            string key = bubble.PlayerId ?? string.Empty;
            if (LastRead.TryGetValue(key, out var last) && last.Text == text && UnityEngine.Time.realtimeSinceStartup - last.Time < 5f)
                continue;
            LastRead[key] = (text, UnityEngine.Time.realtimeSinceStartup);

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

/// <summary>Speech bubbles at a player's portrait: through the header's ShowText ...</summary>
[HarmonyPatch(typeof(PlayerHeaderButton), nameof(PlayerHeaderButton.ShowText))]
internal static class PlayerSpeechBubblePatch
{
    private static void Prefix(PlayerHeaderButton __instance, string text) => SpeechBubbleAnnouncer.Report(__instance?.ID, text);
}

/// <summary>... or directly on the bubble (the summary of another player's turn).</summary>
[HarmonyPatch(typeof(HeaderSpeechBubble), nameof(HeaderSpeechBubble.Show))]
internal static class HeaderSpeechBubbleShowPatch
{
    private static void Prefix(HeaderSpeechBubble __instance, string text) => SpeechBubbleAnnouncer.Report(__instance, text);
}

[HarmonyPatch(typeof(HeaderSpeechBubble), nameof(HeaderSpeechBubble.AnimatedShow))]
internal static class HeaderSpeechBubbleAnimatedShowPatch
{
    private static void Prefix(HeaderSpeechBubble __instance, string text) => SpeechBubbleAnnouncer.Report(__instance, text);
}
