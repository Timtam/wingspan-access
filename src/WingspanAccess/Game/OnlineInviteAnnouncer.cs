using System.Collections.Concurrent;
using System.Collections.Generic;
using HarmonyLib;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Game;

/// <summary>
/// Online invitations arrive silently: the game only adds them to "Your active games" in the online
/// menu. This announces each invitation once per session, with the host's name, wherever the player
/// is. Pending invitations are announced when the game fetches them after logging in.
/// </summary>
internal static class OnlineInviteAnnouncer
{
    private static readonly HashSet<string> Announced = new();

    /// <summary>The online system may report from a network thread; speech waits for the main loop.</summary>
    private static readonly ConcurrentQueue<(string MatchId, string Host)> Pending = new();

    public static void Report(GameInvite invite)
    {
        if (invite == null || invite.HostCanceled)
            return;
        Pending.Enqueue((invite.MatchId ?? string.Empty, invite.HostUsername));
    }

    public static void Tick()
    {
        while (Pending.TryDequeue(out var invite))
        {
            if (!Announced.Add(invite.MatchId))
                continue;
            Plugin.Logger.LogInfo($"[Invite] match {invite.MatchId} from {invite.Host}");
            string host = string.IsNullOrEmpty(invite.Host) ? Loc.Get("online.someone") : invite.Host;
            ScreenReader.Say(Loc.Get("online.invite", host), interrupt: false);
        }
    }
}

[HarmonyPatch(typeof(NakamaGod), nameof(NakamaGod.OnAddInvitation))]
internal static class AddInvitationPatch
{
    private static void Postfix(GameInvite invite) => OnlineInviteAnnouncer.Report(invite);
}

[HarmonyPatch(typeof(NakamaGod), nameof(NakamaGod.OnUpdateInvitationsFromServer))]
internal static class UpdateInvitationsPatch
{
    private static void Postfix(Il2CppSystem.Collections.Generic.List<GameInvite> invites)
    {
        for (int i = 0; invites != null && i < invites.Count; i++)
            OnlineInviteAnnouncer.Report(invites[i]);
    }
}
