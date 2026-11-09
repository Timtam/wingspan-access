using UnityEngine;
using View.UI.Game.Decisions.Base.LayEggs;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Game;

/// <summary>Announces progress in egg laying decisions: how many of the allowed eggs are laid.</summary>
internal static class EggDecisionAnnouncer
{
    private static int _decisionId;
    private static int _lastLaid = -1;
    private static int _nextCheck;

    public static void Tick(GamepadScreen top)
    {
        BaseLayEggs_Decision decision = top == null ? null : top.TryCast<BaseLayEggs_Decision>();
        if (decision == null)
        {
            _decisionId = 0;
            return;
        }

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 10;

        int laid = decision.laidEggs?.Count ?? 0;
        int total = decision.numberOfEggs;
        if (decision.GetInstanceID() != _decisionId)
        {
            _decisionId = decision.GetInstanceID();
            _lastLaid = laid;
            ScreenReader.Say(Loc.Count("eggs.to_lay", total), interrupt: false);
            return;
        }

        if (laid == _lastLaid)
            return;

        _lastLaid = laid;
        string text = Loc.Get("eggs.progress", laid, total);
        if (laid >= total || !decision.CanStillLayEggs())
            text = $"{text}. {Loc.Get("eggs.done_hint")}";
        ScreenReader.Say(text);
    }
}
