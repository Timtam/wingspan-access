using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using WingspanAccess.Localization;

namespace WingspanAccess.UI;

/// <summary>
/// Saved games in the play menu ("Your games") only show portraits and a "turns left/round"
/// counter. This reads their save data: game mode, round, turns left, players and the date.
/// </summary>
internal static class SaveDescriber
{
    private static readonly HashSet<string> Logged = new();

    /// <summary>Description for a saved game slot, or null if the element is none.</summary>
    public static string Describe(GameObject selected)
    {
        MatchUIBase[] slots = selected.GetComponents<MatchUIBase>();
        if (slots.Length == 0)
            return null;
        GameSave save = slots[0].GameSave;
        if (save == null)
            return null;

        MatchUILocal local = slots[0].TryCast<MatchUILocal>();
        // The slot's date text keeps its prefab placeholder ("2020/12/22") while hidden.
        string lastPlayed = local?.lastPlayedTimeText == null || !UiText.IsShown(local.lastPlayedTimeText)
            ? null
            : UiText.Clean(local.lastPlayedTimeText.text);
        if (Logged.Add(save.MatchId ?? string.Empty))
            Plugin.Logger.LogInfo($"[Save] id={save.MatchId} mode={save.GameType} round={save.RoundNumber} turn={save.TurnNumber} tokens={save.TokensLeft} date={save.GamePlayedDate} finished={save.IsFinished} players={save.PlayerCount} names={save.PlayerNames?.Count} portraits={save.Portraits?.Count} uiPortraits={slots[0].portraits?.Count} lastPlayed='{lastPlayed}'");

        // Online slots: invitations, running, finished and timed out games; local saves otherwise.
        MatchUIOnlineInvitation invitation = slots[0].TryCast<MatchUIOnlineInvitation>();
        MatchUIOnline online = slots[0].TryCast<MatchUIOnline>();
        bool finished = slots[0].TryCast<MatchUIOnlineFinished>() != null;
        bool timedOut = slots[0].TryCast<MatchUIOnlineTimeout>() != null;

        var parts = new List<string>();
        if (invitation != null)
            parts.Add(Loc.Get("save.invite", HostName(invitation)));
        else
            parts.Add(Loc.Get(online != null ? "save.online" : "save.title"));

        string mode = save.GameType.ToString().Replace("_Online", string.Empty);
        if (Loc.Has("mode." + mode))
            parts.Add(Loc.Get("mode." + mode));

        if (invitation != null)
        {
            // An invitation's save is a placeholder: no rounds played yet.
            if (invitation.NotReadyIndicator != null && invitation.NotReadyIndicator.activeInHierarchy)
                parts.Add(Loc.Get("save.not_ready"));
        }
        else if (timedOut)
        {
            parts.Add(Loc.Get("save.timed_out"));
        }
        else if (finished || save.IsFinished || (save.RoundNumber >= 4 && save.TokensLeft == 0))
        {
            // The last save of a finished local game is round 5 with no turns left.
            parts.Add(Loc.Get("save.finished"));
        }
        else
        {
            if (online != null)
                parts.AddRange(OnlineState(online));
            parts.Add(Loc.Get("save.round", save.RoundNumber + 1));
            parts.Add(Loc.Count("save.turns_left", save.TokensLeft));
        }

        string players = Players(save);
        if (players.Length == 0)
            players = Players(slots[0].portraits);
        if (players.Length > 0)
            parts.Add(Loc.Get("save.players", players));

        if (online != null)
        {
            // Online games: the turn state above says more than a date.
        }
        else if (!string.IsNullOrEmpty(lastPlayed))
        {
            parts.Add(lastPlayed);
        }
        else
        {
            string played = PlayedDate(save.GamePlayedDate);
            string created = played == null ? CreatedDate(save.MatchId) : null;
            if (played != null)
                parts.Add(Loc.Get("save.date", played));
            else if (created != null)
                parts.Add(Loc.Get("save.created", created));
        }

        return string.Join(", ", parts);
    }

    private static string HostName(MatchUIOnlineInvitation invitation)
    {
        string host = invitation.Invite?.HostUsername;
        if (string.IsNullOrEmpty(host) && invitation.hostNameText != null)
            host = UiText.Clean(invitation.hostNameText.text);
        return string.IsNullOrEmpty(host) ? Loc.Get("online.someone") : host;
    }

    /// <summary>Whose turn it is in a running online game and the time left for it.</summary>
    private static IEnumerable<string> OnlineState(MatchUIOnline online)
    {
        if (online.yourTurn != null && online.yourTurn.isActiveAndEnabled)
            yield return Loc.Get("save.your_turn");
        else if (online.wait != null && online.wait.isActiveAndEnabled)
            yield return Loc.Get("save.waiting");

        if (online.timeLeftText != null && UiText.IsShown(online.timeLeftText))
        {
            string time = UiText.Clean(online.timeLeftText.text);
            if (time.Length > 0)
                yield return Loc.Get("save.time_left", time);
        }
    }

    private static string Players(GameSave save)
    {
        var names = new List<string>();
        var list = save.PlayerNames;
        var portraits = save.Portraits;
        for (int i = 0; list != null && i < list.Count; i++)
        {
            string name = list[i];
            if (portraits != null && i < portraits.Count)
            {
                PictureColor portrait = portraits[i];
                string color = Loc.Has("color." + portrait.Color) ? Loc.Get("color." + portrait.Color) : null;
                if (string.IsNullOrEmpty(name))
                    name = color ?? string.Empty;
                else if (color != null && color != name)
                    name = $"{name} {color}";
                if (portrait.ShowAiLabel)
                    name = Loc.Get("save.ai", name);
            }

            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        return string.Join(", ", names);
    }

    /// <summary>The portraits shown on the slot: player colors, with "AI" where the game marks it.</summary>
    private static string Players(Il2CppSystem.Collections.Generic.List<PlayerPortraitUI> portraits)
    {
        var names = new List<string>();
        for (int i = 0; portraits != null && i < portraits.Count; i++)
        {
            PlayerPortraitUI portrait = portraits[i];
            if (portrait == null || !portrait.gameObject.activeInHierarchy)
                continue;
            string colorKey = "color." + portrait.CurrentPlayerColorName;
            string name = Loc.Has(colorKey) ? Loc.Get(colorKey) : portrait.CurrentPlayerColorName.ToString();
            names.Add(portrait._showAiLabel ? Loc.Get("save.ai", name) : name);
        }

        return string.Join(", ", names);
    }

    /// <summary>Local match ids end in the creation time as a Windows file time ("local_134386716154754881").</summary>
    private static string CreatedDate(string matchId)
    {
        int separator = matchId == null ? -1 : matchId.LastIndexOf('_');
        if (separator < 0 || !long.TryParse(matchId.Substring(separator + 1), out long fileTime))
            return null;
        try
        {
            return DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("g", Culture());
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>The save stores the date as a number; Unix seconds, Unix milliseconds or an OLE date.</summary>
    private static string PlayedDate(double value)
    {
        if (value <= 0)
            return null;

        try
        {
            DateTime date;
            if (value > 1e11)
                date = DateTimeOffset.FromUnixTimeMilliseconds((long)value).LocalDateTime;
            else if (value > 1e8)
                date = DateTimeOffset.FromUnixTimeSeconds((long)value).LocalDateTime;
            else
                date = DateTime.FromOADate(value).ToLocalTime();
            return date.ToString("g", Culture());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static CultureInfo Culture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(I2.Loc.LocalizationManager.CurrentLanguageCode);
        }
        catch (Exception)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
