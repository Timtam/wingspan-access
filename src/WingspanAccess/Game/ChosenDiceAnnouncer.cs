using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// When taking food from the birdfeeder, chosen dice move into a separate row
/// ("DicesChosenButtons"). This announces that row whenever it changes.
/// </summary>
internal static class ChosenDiceAnnouncer
{
    private static string _last;
    private static int _nextCheck;

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

        string text = string.Join(", ", chosen);
        if (text == _last)
            return;

        bool first = _last == null;
        _last = text;
        if (!first)
            ScreenReader.Say(chosen.Count == 0 ? Loc.Get("dice.none") : Loc.Get("dice.chosen", text));
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
