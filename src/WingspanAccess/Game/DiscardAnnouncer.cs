using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Game;

/// <summary>
/// Discarding food for a power (e.g. "discard up to 5 invertebrates to tuck cards"): the game only
/// shows the discarded tokens as pictures, so this announces how many are discarded.
/// </summary>
internal static class DiscardAnnouncer
{
    private static int _decisionId;
    private static int _last = -1;
    private static int _nextCheck;

    public static void Tick(GamepadScreen top)
    {
        DiscardFoodToTuckCardsFromDeck_Decision decision = top == null ? null : top.TryCast<DiscardFoodToTuckCardsFromDeck_Decision>();
        if (decision == null)
        {
            _decisionId = 0;
            return;
        }

        if (decision.GetInstanceID() != _decisionId)
        {
            _decisionId = decision.GetInstanceID();
            _last = decision.discardedFood?.Count ?? 0;
            return;
        }

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 10;

        int discarded = decision.discardedFood?.Count ?? 0;
        if (discarded == _last)
            return;
        _last = discarded;
        int max = System.Math.Min(decision.targetAmount, decision.maxPossibleAmount);
        ScreenReader.Say($"{Loc.Get("discard.count", discarded, max)}. {Loc.Get("dice.confirm")}");
    }
}
