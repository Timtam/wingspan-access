using HarmonyLib;

namespace WingspanAccess.Patches;

/// <summary>
/// Starting a game or a tutorial chapter passes through the play menu and the player setup before
/// the board appears. This tells the focus announcer that a start is under way, so those menus are
/// not read out.
/// </summary>
[HarmonyPatch(typeof(GameLoader), nameof(GameLoader.InvokeStartGame))]
internal static class GameStartPatch
{
    private static void Prefix() => UI.FocusAnnouncer.GameStarting();
}
