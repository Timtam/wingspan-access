using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WingspanAccess.Input;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI.Zones;

/// <summary>
/// The cards dealt at the start of a game (the birds, then the bonus cards). While they are chosen,
/// 7 shows them instead of the still empty hand: Left/Right choose a card, Up/Down read it line by
/// line, Enter keeps it or puts it back. The game's own rows (cards above, food below) move between
/// the rows with Up/Down instead.
/// </summary>
internal sealed class StartCardsZone : Zone
{
    /// <summary>Frame at which the state of the card just toggled with Enter is read (0: none).</summary>
    private int _checkFrame;
    private CardView _toggled;

    private static PlayerResourcesSetup_Decision Decision() => Screens.Top()?.TryCast<PlayerResourcesSetup_Decision>();

    /// <summary>Whether the starting cards are being chosen right now.</summary>
    public static bool Applies() => Decision() != null;

    /// <summary>The card slots the game lets the player choose now, from left to right.</summary>
    private static List<(GamepadCardSlot Slot, CardView Card)> Cards()
    {
        var cards = new List<(GamepadCardSlot Slot, CardView Card)>();
        foreach (GamepadCardSlot slot in Object.FindObjectsOfType<GamepadCardSlot>())
        {
            UnityEngine.UI.Selectable selectable = slot.GetSelectable();
            if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                continue;
            CardView card = CardDescriber.FindCardView(slot.gameObject);
            if (card != null)
                cards.Add((slot, card));
        }

        return cards.OrderBy(entry => entry.Slot.transform.position.x).ToList();
    }

    /// <summary>Whether the card is among the birds (or is the bonus card) chosen so far.</summary>
    private static bool IsTaken(CardView card)
    {
        PlayerResourcesSetup_Decision decision = Decision();
        if (decision == null || card == null)
            return false;
        int id = card.GetInstanceID();
        var taken = decision._cardsTaken;
        for (int i = 0; taken != null && i < taken.Count; i++)
        {
            if (taken[i] != null && taken[i].GetInstanceID() == id)
                return true;
        }

        BonusCardView bonus = decision._bonusCardTaken;
        return bonus != null && bonus.GetInstanceID() == id;
    }

    public override string Title()
    {
        List<(GamepadCardSlot Slot, CardView Card)> cards = Cards();
        return Loc.Get("zone.start", cards.Count, cards.Count(entry => IsTaken(entry.Card)));
    }

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        foreach ((GamepadCardSlot slot, CardView card) in Cards())
        {
            BaseBirdCardView bird = card.TryCast<BaseBirdCardView>();
            int birdId = bird == null ? 0 : (int)bird.ID;
            List<string> lines = CardDescriber.CardLines(card);
            // A bird: name, points, habitats and food cost, enough to compare; Up/Down read the rest.
            string summary = string.Join(", ", lines.Take(bird != null ? 4 : 1));
            if (IsTaken(card))
                summary = $"{summary}, {Loc.Get("card.marked")}";
            GamepadCardSlot currentSlot = slot;
            CardView current = card;
            items.Add(new ZoneItem
            {
                Summary = summary,
                Details = () => birdId > 0 ? CardDescriber.WithFacts(CardDescriber.CardLines(current), birdId, bird) : CardDescriber.CardLines(current),
                Control = () => currentSlot.GetSelectable(),
                Tag = card,
                BirdId = birdId,
            });
        }

        return items;
    }

    public override int SelectedIndex(List<ZoneItem> items)
    {
        GameObject selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        if (selected == null)
            return -1;
        for (int i = 0; i < items.Count; i++)
        {
            UnityEngine.UI.Selectable control = items[i].Control?.Invoke();
            if (control != null && control.gameObject.GetInstanceID() == selected.GetInstanceID())
                return i;
        }

        return -1;
    }

    public override bool HandlesEnter => true;

    /// <summary>Enter keeps the card or puts it back, the way the game's own Enter on it does.</summary>
    public override void Activate(ZoneItem item)
    {
        if (!Select(item))
        {
            ScreenReader.SayTransient(Loc.Get("zone.not_selectable"));
            return;
        }

        KeyboardBridge.Press(VirtualGamepad.Buttons.Submit);
        _toggled = item.Tag as CardView;
        _checkFrame = Time.frameCount + 12;
    }

    public override void Tick()
    {
        if (_checkFrame == 0 || Time.frameCount < _checkFrame)
            return;
        _checkFrame = 0;
        if (_toggled == null)
            return;
        string name = CardDescriber.CardLines(_toggled).FirstOrDefault() ?? string.Empty;
        ScreenReader.SayTransient(Loc.Get(IsTaken(_toggled) ? "zone.start_taken" : "zone.start_returned", name), interrupt: false);
    }
}
