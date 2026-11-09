using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Game;

/// <summary>
/// When a turn's action choice opens, says which habitat is in view (its action and bird count):
/// Enter takes that habitat's action. The zone keys 1 to 3 move the view and announce it themselves.
/// </summary>
internal static class ActionAnnouncer
{
    private static int _chooseActionId;

    public static void Tick(GamepadScreen top)
    {
        ChooseAction_Decision decision = top == null ? null : top.TryCast<ChooseAction_Decision>();
        if (decision == null || UIGod.I == null)
        {
            if (top == null || top.TryCast<Decision>() == null)
                _chooseActionId = 0;
            return;
        }

        if (decision.GetInstanceID() == _chooseActionId)
            return;
        _chooseActionId = decision.GetInstanceID();

        ActionType habitat = UIGod.I.GetCurrentHabitat();
        if (habitat != ActionType.GainFood && habitat != ActionType.LayEggs && habitat != ActionType.DrawBirds)
            return;

        string text = Loc.Get("action." + habitat);
        Rules.Player player = GameStateCache.LocalPlayer;
        if (player != null)
            text = $"{text}, {Loc.Count("game.birds_in_habitat", player.GetAllBirdsInHabitat(habitat)?.Count ?? 0)}";
        ScreenReader.SayTransient(text, interrupt: false);
    }
}
