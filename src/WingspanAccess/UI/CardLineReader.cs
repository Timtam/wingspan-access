using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// Up and Down on a card the game has selected in a single row (the tray while drawing, the hand)
/// read it line by line, the way the zones do. Where Up or Down lead somewhere (from the starting
/// cards to the food, between the habitats of the board), both stay with the game.
/// </summary>
internal static class CardLineReader
{
    private static int _selectionId;
    private static int _line = -1;

    /// <summary>Every frame: another selection starts again at the card's first line.</summary>
    public static void Follow()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        int id = selected == null ? 0 : selected.GetInstanceID();
        if (id != _selectionId)
        {
            _selectionId = id;
            _line = -1;
        }
    }

    /// <summary>Reads the next (step 1) or previous (step -1) line; false if the key belongs to the game.</summary>
    public static bool TryRead(int step)
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        GamepadCardSlot slot = selected == null ? null : selected.GetComponent<GamepadCardSlot>();
        // Birds on the board sit in rows the arrows move between.
        if (slot == null || slot._birdSlot != null)
            return false;

        UnityEngine.UI.Selectable selectable = selected.GetComponent<UnityEngine.UI.Selectable>();
        if (selectable == null || Leads(selectable.FindSelectableOnUp()) || Leads(selectable.FindSelectableOnDown()))
            return false;

        CardView view = CardDescriber.FindCardView(selected);
        if (view == null)
            return false;
        BaseBirdCardView bird = view.TryCast<BaseBirdCardView>();
        int birdId = bird == null ? 0 : (int)bird.ID;
        List<string> lines = birdId > 0 ? CardDescriber.WithFacts(CardDescriber.CardLines(view), birdId, bird) : CardDescriber.CardLines(view);
        if (lines.Count == 0)
            return false;

        if (selected.GetInstanceID() != _selectionId)
        {
            _selectionId = selected.GetInstanceID();
            _line = -1;
        }

        _line = Mathf.Clamp(_line < 0 ? 0 : _line + step, 0, lines.Count - 1);
        ScreenReader.SayTransient(lines[_line]);
        return true;
    }

    private static bool Leads(UnityEngine.UI.Selectable target) =>
        target != null && target.IsInteractable() && target.gameObject.activeInHierarchy;
}
