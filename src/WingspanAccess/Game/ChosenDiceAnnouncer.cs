using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// When taking food from the birdfeeder, chosen dice move into a separate row
/// ("DicesChosenButtons"). This announces how much food may be taken when the decision opens,
/// and the chosen dice with the number still possible whenever the row changes.
/// </summary>
internal static class ChosenDiceAnnouncer
{
    private static string _last;
    private static int _mismatchChecks;
    private static int _nextCheck;
    private static int _rerollDecisionId;
    private static bool _wasRerolled;
    private static int _rerollAnnounceFrame;

    /// <summary>After the birdfeeder is rerolled (once the dice have landed), says what it holds now.</summary>
    private static void AnnounceReroll(BaseGainFoodFromBirdFeeder_Decision decision)
    {
        if (decision == null)
        {
            _rerollDecisionId = 0;
            _rerollAnnounceFrame = 0;
            return;
        }

        bool rerolled = decision.birdfeederWasRerolled;
        if (decision.GetInstanceID() != _rerollDecisionId)
        {
            _rerollDecisionId = decision.GetInstanceID();
            _wasRerolled = rerolled;
            _rerollAnnounceFrame = 0;
            return;
        }

        if (rerolled && !_wasRerolled)
            _rerollAnnounceFrame = Time.frameCount + 45;
        _wasRerolled = rerolled;

        if (_rerollAnnounceFrame == 0 || Time.frameCount < _rerollAnnounceFrame)
            return;
        _rerollAnnounceFrame = 0;
        string food = GameReader.FoodList(GameStateCache.State?.BirdFeederState);
        ScreenReader.Say(Loc.Get("dice.rerolled", food.Length == 0 ? Loc.Get("game.nothing") : food));
    }

    public static void Tick(GamepadScreen top)
    {
        if (top == null || top.TryCast<Decision>() == null)
        {
            _last = null;
            return;
        }

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 10;

        AnnounceReroll(top.TryCast<BaseGainFoodFromBirdFeeder_Decision>());
        Transform chosenRow = FindChild(top.transform, "DicesChosenButtons");
        if (chosenRow == null)
        {
            _last = null;
            return;
        }

        var chosen = new List<string>();
        foreach (UnityEngine.UI.Selectable die in chosenRow.GetComponentsInChildren<UnityEngine.UI.Selectable>(false))
        {
            if (!die.interactable)
                continue;
            string name = UiText.NameOf(die.gameObject);
            if (name != null)
                chosen.Add(name);
        }

        // Double dice (nectar or seed) show both foods; the decision knows which one was taken.
        BaseGainFoodFromBirdFeeder_Decision food = top.TryCast<BaseGainFoodFromBirdFeeder_Decision>();
        var taken = food?.foodChosen?.List;
        if (taken != null && taken.Count > 0)
        {
            if (taken.Count == chosen.Count)
            {
                _mismatchChecks = 0;
                chosen.Clear();
                for (int i = 0; i < taken.Count; i++)
                    chosen.Add(GameReader.FoodTypeName(taken[i].Token));
            }
            else if (++_mismatchChecks < 3)
            {
                return; // the row and the decision update in different frames
            }
        }

        string text = string.Join(", ", chosen);
        if (text == _last)
            return;

        bool first = _last == null;
        _last = text;
        int amount = food == null ? 0 : food.amountToChoose;
        if (first)
        {
            // Also after returning from the skip warning, where dice may already be chosen.
            var intro = new List<string>();
            if (amount > 0)
                intro.Add(Loc.Count("food.to_take", amount));
            if (chosen.Count > 0)
                intro.Add(Loc.Get("dice.chosen", text));
            if (intro.Count > 0)
                ScreenReader.Say(UiText.JoinSentences(intro), interrupt: false);
            return;
        }

        if (chosen.Count == 0)
        {
            ScreenReader.Say(Loc.Get("dice.none"));
            return;
        }

        var parts = new List<string> { Loc.Get("dice.chosen", text) };
        if (amount > chosen.Count)
            parts.Add(Loc.Count("dice.more", amount - chosen.Count));
        parts.Add(Loc.Get("dice.confirm"));
        ScreenReader.Say(UiText.JoinSentences(parts));
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }

        return null;
    }
}
