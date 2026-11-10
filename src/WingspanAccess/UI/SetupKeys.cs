using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// The starting choice shows the dealt cards in a row and the food below them; the game moves between
/// the two rows with Up and Down. With the keyboard they are one row, as in the zones: Left and Right
/// go through the cards and then the food, Home and End jump to the first and the last, and Up and
/// Down read the selected card line by line.
/// </summary>
internal static class SetupKeys
{
    private static string _loggedRow;

    /// <summary>Step 1 or -1 moves to the next or previous item, int.MinValue and int.MaxValue to the first and last; false off the starting choice.</summary>
    public static bool TryMove(GamepadScreen top, int step)
    {
        if (!Applies(top))
            return false;
        List<UnityEngine.UI.Selectable> row = Row(top);
        if (row.Count == 0)
            return false;

        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        int index = selected == null ? -1 : row.FindIndex(item => item.gameObject.GetInstanceID() == selected.GetInstanceID());
        int target = step == int.MinValue ? 0 : step == int.MaxValue ? row.Count - 1 : index < 0 ? 0 : index + step;
        if (target < 0 || target >= row.Count || target == index)
        {
            // At the first or last item the key repeats it, as in the zones.
            if (selected != null && selected.activeInHierarchy)
                ScreenReader.SayTransient(UiText.Describe(selected));
            return true;
        }

        row[target].Select();
        return true;
    }

    /// <summary>Up and Down: the selected card line by line; on food its name and state again.</summary>
    public static bool TryRead(GamepadScreen top, int step)
    {
        if (!Applies(top))
            return false;
        if (CardLineReader.TryRead(step, anyCard: true))
            return true;

        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy)
            ScreenReader.SayTransient(UiText.Describe(selected));
        return true;
    }

    /// <summary>
    /// An item of the row as one list: a card in short (see StartCardsZone.Summary), the food by name,
    /// both with "selected" when taken; the switch to the other view as a button; then the place in
    /// the whole row ("7 of 11"). Null for anything else.
    /// </summary>
    public static string Describe(GameObject target)
    {
        GamepadScreen top = Screens.Top();
        if (!Applies(top) || target == null)
            return null;
        List<UnityEngine.UI.Selectable> row = Row(top);
        int index = row.FindIndex(item => item.gameObject.GetInstanceID() == target.GetInstanceID());
        if (index < 0)
            return null;

        string text;
        UnityEngine.UI.Toggle toggle = target.GetComponent<UnityEngine.UI.Toggle>();
        if (toggle == null)
        {
            CardView card = CardDescriber.FindCardView(target);
            if (card == null)
                return null;
            text = Zones.StartCardsZone.Summary(card);
        }
        else if (IsSwitch(toggle))
        {
            text = $"{UiText.NameOf(target)}, {Localization.Loc.Get("role.button")}";
        }
        else
        {
            string name = UiText.NameOf(target) ?? UiText.HumanizeTypeName(target.name);
            text = toggle.isOn ? $"{name}, {Localization.Loc.Get("card.marked")}" : name;
        }

        return $"{text}, {Localization.Loc.Get("position", index + 1, row.Count)}";
    }

    /// <summary>The toggles that switch between birds with food and the bonus cards (they work like buttons).</summary>
    public static bool IsSwitch(UnityEngine.UI.Toggle toggle) =>
        toggle.name is "ToggleBonus" or "ToggleFood" && toggle.GetComponentInParent<PlayerResourcesSetup_Decision>() != null;

    /// <summary>Whether the toggle is food of the starting choice, whose state reads "selected" like the cards.</summary>
    public static bool IsFood(UnityEngine.UI.Toggle toggle) =>
        !IsSwitch(toggle) && toggle.GetComponentInParent<PlayerResourcesSetup_Decision>() != null &&
        UiText.PathOf(toggle.gameObject).Contains("/CardsFood/");

    /// <summary>The card Enter was pressed on, and the frame its new state is read (0: none).</summary>
    private static CardView _toggledCard;
    private static int _checkFrame;
    private static bool _wasTaken;

    /// <summary>Enter on a card takes it or puts it back; its new state is read a moment later, as for the food.</summary>
    public static void Tick(GamepadScreen top)
    {
        if (!Applies(top))
        {
            _checkFrame = 0;
            return;
        }

        if (_checkFrame > 0 && Time.frameCount >= _checkFrame)
        {
            _checkFrame = 0;
            bool taken = _toggledCard != null && Zones.StartCardsZone.IsTaken(_toggledCard);
            if (_toggledCard != null && taken != _wasTaken)
                ScreenReader.SayTransient(Localization.Loc.Get(taken ? "role.option.on" : "role.option.off"));
        }

        if (Zones.ZoneNavigator.IsActive || !(UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)))
            return;
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        if (selected == null || selected.GetComponent<GamepadCardSlot>() == null)
            return;
        _toggledCard = CardDescriber.FindCardView(selected);
        if (_toggledCard == null)
            return;
        _wasTaken = Zones.StartCardsZone.IsTaken(_toggledCard);
        _checkFrame = Time.frameCount + 12;
    }

    private static bool Applies(GamepadScreen top) => top != null && top.TryCast<PlayerResourcesSetup_Decision>() != null;

    /// <summary>The dealt cards from left to right, then the food the same way, then the rest ("choose a bonus card").</summary>
    private static List<UnityEngine.UI.Selectable> Row(GamepadScreen top)
    {
        var row = new List<UnityEngine.UI.Selectable>();
        foreach (var (slot, _) in Zones.StartCardsZone.Cards())
        {
            UnityEngine.UI.Selectable card = slot.GetSelectable();
            if (card != null)
                row.Add(card);
        }

        var food = new List<UnityEngine.UI.Toggle>();
        foreach (UnityEngine.UI.Toggle toggle in top.GetComponentsInChildren<UnityEngine.UI.Toggle>(false))
        {
            if (toggle.IsInteractable() && toggle.gameObject.activeInHierarchy && toggle.GetComponentInParent<GamepadCardSlot>() == null)
                food.Add(toggle);
        }

        // The food sits in the decision's food panel; other toggles (the bonus card choice) come last.
        row.AddRange(food.OrderBy(toggle => UiText.PathOf(toggle.gameObject).Contains("/CardsFood/") ? 0 : 1)
            .ThenBy(toggle => toggle.transform.position.x));

        string names = string.Join(", ", row.Select(item => item.name));
        if (names != _loggedRow)
        {
            _loggedRow = names;
            Plugin.Logger.LogInfo($"[Setup] one row: {names}");
        }

        return row;
    }
}
