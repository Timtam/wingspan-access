using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Game;

/// <summary>
/// Page Up/Down move the view between the habitats (UIGod.GetCurrentHabitat). While the local
/// player decides something, every change is announced with the habitat's action and bird count.
/// </summary>
internal static class ActionAnnouncer
{
    private static ActionType _last = ActionType.None;

    public static void Tick(GamepadScreen top)
    {
        if (UIGod.I == null)
        {
            _last = ActionType.None;
            return;
        }

        // Only the local player's decisions; the remembered habitat survives screen changes,
        // so it is announced when the view really moves, not after every decision.
        Decision decision = top == null ? null : top.TryCast<Decision>();
        if (decision == null || top.TryCast<WaitForOtherPlayer_Decision>() != null)
            return;

        ActionType habitat = UIGod.I.GetCurrentHabitat();
        if (habitat == _last)
            return;

        _last = habitat;
        if (habitat != ActionType.GainFood && habitat != ActionType.LayEggs && habitat != ActionType.DrawBirds)
            return;

        string text = Loc.Get("action." + habitat);
        Rules.Player player = GameStateCache.LocalPlayer;
        if (player != null)
            text = $"{text}, {Loc.Count("game.birds_in_habitat", player.GetAllBirdsInHabitat(habitat)?.Count ?? 0)}";
        ScreenReader.Say(text, interrupt: false);
    }
}
