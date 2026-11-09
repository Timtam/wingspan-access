using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// Bird powers whose progress the game only shows as pictures: food cached on a bird so far, and
/// the bonus card drawn from the deck.
/// </summary>
internal static class PowerProgressAnnouncer
{
    private static int _decisionId;
    private static int _last = -1;
    private static int _nextCheck;

    public static void Tick(GamepadScreen top)
    {
        CacheUpToAmountOfFoodFromSupplyOnBird_Decision cache = top?.TryCast<CacheUpToAmountOfFoodFromSupplyOnBird_Decision>();
        BaseDrawBonusCardFromDeck_Decision bonus = top?.TryCast<BaseDrawBonusCardFromDeck_Decision>();
        if (cache == null && bonus == null)
        {
            _decisionId = 0;
            return;
        }

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 10;

        int count = cache != null ? cache.foodToCache?.Count ?? 0 : bonus.bonusCardDraw?.Count ?? 0;
        if (top.GetInstanceID() != _decisionId)
        {
            _decisionId = top.GetInstanceID();
            _last = count;
            return;
        }

        if (count == _last)
            return;
        bool added = count > _last;
        _last = count;
        ScreenReader.Say(cache != null ? CacheText(cache, count) : added ? BonusText(bonus) : null);
    }

    /// <summary>"2 von 2 gelagert: Fisch, Fisch. Leertaste bestätigt".</summary>
    private static string CacheText(CacheUpToAmountOfFoodFromSupplyOnBird_Decision decision, int count)
    {
        var food = new List<string>();
        var list = decision.foodToCache;
        for (int i = 0; list != null && i < list.Count; i++)
            food.Add(GameReader.FoodTypeName(list[i].FoodType));

        var parts = new List<string> { Loc.Get("cache.progress", count, decision.amountToCache, string.Join(", ", food)) };
        if (count > 0)
            parts.Add(Loc.Get("cache.undo_hint"));
        parts.Add(Loc.Get("dice.confirm"));
        return count == 0 ? Loc.Get("cache.none") : UiText.JoinSentences(parts);
    }

    /// <summary>The bonus card just drawn, read in full, then the confirm hint.</summary>
    private static string BonusText(BaseDrawBonusCardFromDeck_Decision decision)
    {
        BonusCardView view = decision.bonusCard;
        string card = view != null ? CardDescriber.DescribeBonus(view) : null;
        if (string.IsNullOrEmpty(card))
        {
            var drawn = decision.bonusCardDraw;
            card = drawn == null || drawn.Count == 0 ? "?" : GameReader.BonusName(drawn[drawn.Count - 1]);
        }

        return UiText.JoinSentences(new[] { Loc.Get("draw.drawn", card), Loc.Get("dice.confirm") });
    }
}
