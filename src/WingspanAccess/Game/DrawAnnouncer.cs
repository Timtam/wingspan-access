using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Game;

/// <summary>
/// Drawing bird cards: announces how many cards are still to be drawn, how many are marked and
/// which card came from the deck. The game shows the remaining count only as icons, and a card
/// drawn from the deck only as an animation.
/// </summary>
internal static class DrawAnnouncer
{
    private static int _decisionId;
    private static int _lastLeft = -1;
    private static int _lastMarked = -1;
    private static int _lastDrawn;
    private static bool _lastShowingDrawn;
    private static int _nextCheck;

    public static void Tick(GamepadScreen top)
    {
        DrawBirds_Decision decision = top == null ? null : top.TryCast<DrawBirds_Decision>();
        if (decision == null)
        {
            _decisionId = 0;
            return;
        }

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 10;

        int left = decision._amountToChoose - decision.CardsTakenTotalCount;
        int marked = decision._selectedCards?.Count ?? 0;
        // A card from the deck is shown on the deck until the next confirm moves it into the hand.
        bool showingDrawn = decision._isDrawingCardFromDeck;
        Il2CppSystem.Collections.Generic.List<int> drawn = decision._cardsDrawnFromDeck;
        int drawnCount = drawn?.Count ?? 0;
        bool first = decision.GetInstanceID() != _decisionId;
        if (first)
        {
            _decisionId = decision.GetInstanceID();
            _lastDrawn = 0;
        }

        if (!first && left == _lastLeft && marked == _lastMarked && drawnCount == _lastDrawn && showingDrawn == _lastShowingDrawn)
            return;

        Plugin.Logger.LogInfo($"[Draw] amount={decision._amountToChoose} taken={decision.CardsTakenTotalCount} marked={marked} deck={drawnCount} tray={decision._cardsTakenFromTray?.Count ?? 0} drawing={showingDrawn} hasDrawn={decision._hasDrawnACardFromDeck}");
        var parts = new List<string>();
        if (drawnCount > _lastDrawn)
        {
            var names = new List<string>();
            for (int i = _lastDrawn; i < drawnCount; i++)
                names.Add(GameReader.BirdName(drawn[i]));
            parts.Add(Loc.Get("draw.drawn", string.Join(", ", names)));
        }

        if (showingDrawn)
        {
            parts.Add(Loc.Get("draw.take_hint"));
        }
        else
        {
            if (left > 0)
                parts.Add(Loc.Count("draw.left", left));
            if (marked > 0)
                parts.Add(Loc.Count("draw.marked", marked));
            else if (left <= 0)
                parts.Add(Loc.Get("draw.confirm"));
        }

        _lastLeft = left;
        _lastMarked = marked;
        _lastDrawn = drawnCount;
        _lastShowingDrawn = showingDrawn;
        ScreenReader.Say(UI.UiText.JoinSentences(parts), interrupt: !first);
    }

    /// <summary>Whether a card is marked in the current draw or starting selection decision.</summary>
    public static bool? IsMarked(CardView view)
    {
        GamepadScreen top = UI.Screens.Top();
        if (top == null)
            return null;

        DrawBirds_Decision draw = top.TryCast<DrawBirds_Decision>();
        if (draw?._selectedCards != null)
            return Contains(draw._selectedCards, view);

        PlayerResourcesSetup_Decision setup = top.TryCast<PlayerResourcesSetup_Decision>();
        if (setup?._cardsTaken != null)
        {
            BirdCardView bird = view.TryCast<BirdCardView>();
            if (bird != null)
                return ContainsBird(setup._cardsTaken, bird);
            BonusCardView bonus = view.TryCast<BonusCardView>();
            if (bonus != null)
                return setup._bonusCardTaken != null && setup._bonusCardTaken.GetInstanceID() == bonus.GetInstanceID();
        }

        return null;
    }

    private static bool Contains(Il2CppSystem.Collections.Generic.List<CardView> list, CardView view)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].GetInstanceID() == view.GetInstanceID())
                return true;
        }

        return false;
    }

    private static bool ContainsBird(Il2CppSystem.Collections.Generic.List<BirdCardView> list, BirdCardView view)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].GetInstanceID() == view.GetInstanceID())
                return true;
        }

        return false;
    }
}
