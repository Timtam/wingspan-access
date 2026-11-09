using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Game;

/// <summary>
/// At round end (and game end) the player activates birds with "round end" powers one by one.
/// The game highlights them on the board; this names the birds still to activate and their habitat.
/// </summary>
internal static class SpecialPowersAnnouncer
{
    private static readonly ActionType[] Habitats = { ActionType.GainFood, ActionType.LayEggs, ActionType.DrawBirds };

    private static int _decisionId;
    private static string _last;
    private static bool _logged;
    private static int _nextCheck;

    public static void Tick(GamepadScreen top)
    {
        BaseResolveSpecialPowers_Decision decision = top == null ? null : top.TryCast<BaseResolveSpecialPowers_Decision>();
        if (decision == null)
        {
            _decisionId = 0;
            return;
        }

        if (decision.GetInstanceID() != _decisionId)
        {
            _decisionId = decision.GetInstanceID();
            _last = null;
            _logged = false;
            _nextCheck = Time.frameCount + 20; // after the decision's own instruction
            return;
        }

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 15;

        // Birds still to activate; before the first activation the game may only fill the list of candidates.
        var birds = decision._notActivatedBirds;
        if (birds == null || birds.Count == 0)
            birds = decision._powersActivated == 0 ? decision._birdsToResolve : birds;
        if (!_logged)
        {
            _logged = true;
            Plugin.Logger.LogInfo($"[Powers] toResolve={decision._birdsToResolve?.Count} notActivated={decision._notActivatedBirds?.Count} activated={decision._powersActivated}/{decision._powersToActivate}");
        }

        var names = new List<string>();
        for (int i = 0; birds != null && i < birds.Count; i++)
        {
            if (birds[i] != null)
                names.Add(Describe(birds[i]));
        }

        string text = names.Count == 0 ? null : Loc.Get("powers.to_activate", string.Join(", ", names));
        if (text == _last)
            return;
        _last = text;
        if (text != null)
            ScreenReader.Say(text, interrupt: false);
    }

    /// <summary>"Mauersegler (Grasland)": the bird and the habitat it sits in.</summary>
    private static string Describe(Rules.Bird bird)
    {
        string name = GameReader.BirdName(bird.ID);
        Rules.Player player = GameStateCache.LocalPlayer;
        if (player == null)
            return name;

        ActionType habitat = player.GetBirdHabitatType(bird.ID);
        return System.Array.IndexOf(Habitats, habitat) < 0 ? name : $"{name} ({Loc.Get("habitat." + habitat)})";
    }
}
