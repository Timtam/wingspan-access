using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Game;
using WingspanAccess.Input;
using WingspanAccess.Localization;

namespace WingspanAccess.UI.Zones;

/// <summary>The birds in one habitat of the board being read (your own unless another player was chosen).</summary>
internal sealed class HabitatZone : Zone
{
    private readonly ActionType _habitat;

    public HabitatZone(ActionType habitat) => _habitat = habitat;

    /// <summary>Another player's board is only read: Enter does not reach the game (it would take your action).</summary>
    public override bool HandlesEnter => ZoneNavigator.BoardOwner != null;

    public override void Activate(ZoneItem item)
    {
        if (ZoneNavigator.BoardOwner != null)
            Speech.ScreenReader.SayTransient(Loc.Get("zone.board_of", GameReader.PlayerName(ZoneNavigator.BoardOwner)));
    }

    private static Rules.Player Owner => ZoneNavigator.BoardOwner ?? GameStateCache.LocalPlayer;

    public override string Title()
    {
        Rules.Player owner = Owner;
        int count = owner?.GetAllBirdsInHabitat(_habitat)?.Count ?? 0;
        string birds = Loc.Count("game.birds_in_habitat", count);
        if (owner != null && ZoneNavigator.BoardOwner != null)
            return Loc.Get("zone.habitat_of", GameReader.PlayerName(owner), Loc.Get("habitat." + _habitat), birds);
        // While choosing an action the habitat's action matters (Enter takes it); otherwise only the place.
        bool choosing = Screens.Top()?.TryCast<ChooseAction_Decision>() != null;
        return $"{Loc.Get((choosing ? "action." : "habitat.") + _habitat)}, {birds}";
    }

    public override void OnEnter()
    {
        // Moving the view also decides which action Enter takes when choosing an action.
        if (ZoneNavigator.BoardOwner == null && UIGod.I != null && UIGod.I.GetCurrentHabitat() != _habitat)
            UIGod.I.GoTo(_habitat);
    }

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        var birds = Owner?.GetAllBirdsInHabitat(_habitat);
        bool own = ZoneNavigator.BoardOwner == null;
        for (int i = 0; birds != null && i < birds.Count; i++)
        {
            Rules.Bird bird = birds[i];
            if (bird == null)
                continue;
            items.Add(new ZoneItem
            {
                Summary = GameReader.DescribeBoardBird(bird),
                Details = () =>
                {
                    BaseBirdCardView view = own ? CardDescriber.FindBoardView(bird) : null;
                    return view != null ? CardDescriber.BirdLines(view) : CardDescriber.BirdLines(bird.ID, bird);
                },
                Control = own ? () => BoardControl(bird) : null,
            });
        }

        return items;
    }

    /// <summary>The game's selectable slot for a bird on the board, if the current task offers it.</summary>
    private static UnityEngine.UI.Selectable BoardControl(Rules.Bird bird)
    {
        foreach (GamepadCardSlot slot in Object.FindObjectsOfType<GamepadCardSlot>())
        {
            BaseBirdCardView card = slot._birdSlot?.Card;
            if (card != null && card.GameStateBird != null && card.GameStateBird.Pointer == bird.Pointer)
                return slot.GetSelectable();
        }

        return null;
    }
}

/// <summary>The dice in the birdfeeder; while taking food they are the dice to choose.</summary>
internal sealed class BirdfeederZone : Zone
{
    public override string Title()
    {
        string food = GameReader.FoodList(GameStateCache.State?.BirdFeederState);
        return Loc.Get("game.birdfeeder", food.Length == 0 ? Loc.Get("game.nothing") : food);
    }

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        var decision = Screens.Top()?.TryCast<BaseGainFoodFromBirdFeeder_Decision>();
        if (decision?.Dice != null)
        {
            foreach (DiceUI_Image die in decision.Dice)
            {
                if (die == null || !die.gameObject.activeInHierarchy)
                    continue;
                DiceUI_Image current = die;
                items.Add(new ZoneItem
                {
                    Summary = GameReader.FoodTypeName(die.FoodType),
                    Details = () => new List<string> { GameReader.FoodTypeName(current.FoodType) },
                    Control = () => (UnityEngine.UI.Selectable)current.Button ?? current.Toggle,
                });
            }

            // The reroll buttons next to the dice belong to the zone while they can be used (End reaches them).
            foreach (UnityEngine.UI.Selectable button in decision.GetComponentsInChildren<UnityEngine.UI.Selectable>(false))
            {
                Transform parent = button == null ? null : button.transform.parent;
                if (button == null || !button.interactable ||
                    !(RerollButtons.Contains(button.gameObject.name) || (parent != null && RerollButtons.Contains(parent.name))))
                    continue;
                UnityEngine.UI.Selectable current = button;
                string label = UiText.Describe(button.gameObject);
                items.Add(new ZoneItem { Summary = label, Details = () => new List<string> { label }, Control = () => current });
            }

            return items;
        }

        Rules.Food feeder = GameStateCache.State?.BirdFeederState;
        if (feeder == null)
            return items;
        AddDice(items, feeder.Invertebrate, FoodType.Invertebrate);
        AddDice(items, feeder.Seed, FoodType.Seed);
        AddDice(items, feeder.Fruit, FoodType.Fruit);
        AddDice(items, feeder.Fish, FoodType.Fish);
        AddDice(items, feeder.Rodent, FoodType.Rodent);
        AddDice(items, feeder.InvertebrateSeed, FoodType.InvertebrateSeed);
        AddDice(items, feeder.Nectar, FoodType.Nectar);
        AddDice(items, feeder.NectarFruit, FoodType.NectarFruit);
        AddDice(items, feeder.NectarSeed, FoodType.NectarSeed);
        return items;
    }

    private static readonly HashSet<string> RerollButtons = new() { "ReloadBirdfeederButton", "RerollAllDicesOutsideBirdfeederButton" };

    private static void AddDice(List<ZoneItem> items, int count, FoodType type)
    {
        string name = GameReader.FoodTypeName(type);
        for (int i = 0; i < count; i++)
            items.Add(new ZoneItem { Summary = name, Details = () => new List<string> { name } });
    }
}

/// <summary>The face-up bird cards and the deck.</summary>
internal sealed class TrayZone : Zone
{
    public override string Title()
    {
        List<string> names = TrayCards().ConvertAll(GameReader.BirdName);
        return Loc.Get("game.tray", names.Count == 0 ? Loc.Get("game.nothing") : string.Join(", ", names));
    }

    /// <summary>
    /// The face-up cards. Cards taken this turn stay in the rules model until the turn ends, but the
    /// tray on screen no longer shows them, so only cards with a view in the tray count.
    /// </summary>
    private static List<int> TrayCards()
    {
        var ids = new List<int>();
        var visible = GameStateCache.State?.BirdsDeckState?.Visible;
        for (int i = 0; visible != null && i < visible.Count; i++)
            ids.Add(visible[i]);
        List<int> shown = ids.FindAll(id => TrayView(id) != null);
        return shown.Count > 0 ? shown : ids;
    }

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        foreach (int card in TrayCards())
        {
            int id = card;
            items.Add(new ZoneItem
            {
                Summary = GameReader.BirdName(id),
                Details = () =>
                {
                    BaseBirdCardView view = TrayView(id);
                    return view != null ? CardDescriber.BirdLines(view) : CardDescriber.BirdLines(id, null);
                },
                Control = () => TrayControl(id),
            });
        }

        items.Add(new ZoneItem
        {
            Summary = Loc.Get("card.deck"),
            Details = () => new List<string> { Loc.Get("card.deck") },
            Control = DeckControl,
        });
        return items;
    }

    private static BaseBirdCardView TrayView(int id)
    {
        foreach (CardSlot slot in Object.FindObjectsOfType<CardSlot>())
        {
            BaseBirdCardView card = slot.Card?.TryCast<BaseBirdCardView>();
            if (card != null && (int)card.ID == id)
                return card;
        }

        return null;
    }

    private static UnityEngine.UI.Selectable TrayControl(int id)
    {
        foreach (GamepadCardSlot slot in Object.FindObjectsOfType<GamepadCardSlot>())
        {
            BaseBirdCardView card = slot._slot?.Card?.TryCast<BaseBirdCardView>();
            if (card != null && (int)card.ID == id)
                return slot.GetSelectable();
        }

        return null;
    }

    private static UnityEngine.UI.Selectable DeckControl()
    {
        foreach (GamepadCardSlot slot in Object.FindObjectsOfType<GamepadCardSlot>())
        {
            if (slot._slot != null && slot._slot.TryCast<DeckSlot>() != null)
                return slot.GetSelectable();
        }

        return null;
    }
}

/// <summary>Your food and the number of cards in your hand.</summary>
internal sealed class SupplyZone : Zone
{
    public override string Title()
    {
        Rules.Player player = GameStateCache.LocalPlayer;
        string food = GameReader.FoodList(player?._pf);
        return Loc.Get("zone.supply", food.Length == 0 ? Loc.Get("game.no_food") : food);
    }

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        Rules.Player player = GameStateCache.LocalPlayer;
        if (player == null)
            return items;

        Rules.Food food = player._pf;
        if (food != null)
        {
            AddFood(items, food.Invertebrate, FoodType.Invertebrate);
            AddFood(items, food.Seed, FoodType.Seed);
            AddFood(items, food.Fruit, FoodType.Fruit);
            AddFood(items, food.Fish, FoodType.Fish);
            AddFood(items, food.Rodent, FoodType.Rodent);
            AddFood(items, food.Nectar, FoodType.Nectar);
        }

        Add(items, Loc.Count("game.bird_cards", player.BirdCards?.Count ?? 0));
        Add(items, Loc.Count("game.bonus_cards", player.BonusCards?.Count ?? 0));
        return items;
    }

    private static void AddFood(List<ZoneItem> items, int count, FoodType type)
    {
        if (count > 0)
            Add(items, $"{count} {GameReader.FoodTypeName(type)}");
    }

    private static void Add(List<ZoneItem> items, string text) =>
        items.Add(new ZoneItem { Summary = text, Details = () => new List<string> { text } });
}

/// <summary>
/// Your hand. Entering opens it when the game allows (to play a bird); the cursor then moves the
/// game's own card selection, so Enter plays or chooses the card.
/// </summary>
internal sealed class HandZone : Zone
{
    private bool _opened;

    private static PlayerHandUI Hand => HandAnnouncer.LocalHand();

    private static List<CardView> Cards()
    {
        var result = new List<CardView>();
        var cards = Hand?.cards;
        for (int i = 0; cards != null && i < cards.Count; i++)
        {
            if (cards[i] != null)
                result.Add(cards[i]);
        }

        return result;
    }

    public override string Title() => Loc.Count("zone.hand", Cards().Count);

    /// <summary>Decisions that take a card from the hand: playing a bird, discarding or tucking cards.</summary>
    public static bool IsHandTask(GamepadScreen top) =>
        top != null && (top.TryCast<ChooseAction_Decision>() != null || top.TryCast<DiscardBirds_Decision>() != null ||
            top.TryCast<TuckCardsFromHand_Decision>() != null || top.GetIl2CppType().Name.Contains("Hand"));

    public override void OnEnter()
    {
        // Open the hand only where cards are played or chosen from it; elsewhere it is read-only.
        PlayerHandUI hand = Hand;
        GamepadScreen top = Screens.Top();
        bool handTask = top != null && (top.TryCast<ChooseAction_Decision>() != null || top.GetIl2CppType().Name.Contains("Hand"));
        if (hand == null || hand.IsInCardPreview || !handTask)
            return;
        KeyboardBridge.Press(VirtualGamepad.Buttons.ToggleCardPreview);
        _opened = true;
    }

    public override void OnLeave()
    {
        if (_opened && Hand != null && Hand.IsInCardPreview)
            KeyboardBridge.Press(VirtualGamepad.Buttons.Back);
    }

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        foreach (CardView card in Cards())
        {
            CardView current = card;
            items.Add(new ZoneItem
            {
                Summary = CardDescriber.HandSummary(card),
                Details = () => CardDescriber.CardLines(current),
                Tag = card,
            });
        }

        return items;
    }

    public override int SelectedIndex(List<ZoneItem> items)
    {
        GameObject selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null || Hand == null)
            return -1;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Tag is CardView card && card.gameObject.GetInstanceID() == selected.GetInstanceID())
                return i;
        }

        return -1;
    }

    /// <summary>Steps the game's own hand selection to the card (the way the gamepad does).</summary>
    public override bool Select(ZoneItem item)
    {
        PlayerHandUI hand = Hand;
        if (hand == null || !(hand.IsInCardPreview || IsHandTask(Screens.Top())))
            return false;

        List<ZoneItem> items = Items();
        int target = items.FindIndex(other => ReferenceEquals(other.Tag, item.Tag) ||
            (other.Tag is CardView a && item.Tag is CardView b && a.GetInstanceID() == b.GetInstanceID()));
        for (int guard = 0; guard < 20; guard++)
        {
            int current = SelectedIndex(items);
            if (current < 0 && guard == 0)
            {
                // Discarding from the hand starts without a selected card; the first step selects one.
                hand.NextCard();
                continue;
            }

            if (current < 0 || current == target)
                return current == target;
            if (current < target)
                hand.NextCard();
            else
                hand.PreviousCard();
        }

        return false;
    }
}

/// <summary>Round, actions left and whose turn it is.</summary>
internal sealed class StatusZone : Zone
{
    public override string Title() => string.Join(", ", Lines());

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        foreach (string line in Lines())
        {
            string text = line;
            items.Add(new ZoneItem { Summary = text, Details = () => new List<string> { text } });
        }

        return items;
    }

    private static List<string> Lines()
    {
        var lines = new List<string>();
        Rules.GameState state = GameStateCache.State;
        Rules.Player player = GameStateCache.LocalPlayer;
        if (state == null || player == null)
            return lines;

        lines.Add(Loc.Get("status.round", state.RoundsCounter + 1));
        lines.Add(Loc.Count("status.actions", player.TokensLeft));
        Rules.Player current = state.GetPlayer(state.CurrentPlayerID);
        if (current != null)
            lines.Add(Loc.Get("status.current", current.IsLocalPlayer() ? Loc.Get("zone.you") : GameReader.PlayerName(current)));
        return lines;
    }
}

/// <summary>All players. Enter on a player makes 1 to 3 read that player's habitats.</summary>
internal sealed class PlayersZone : Zone
{
    public override string Title() => Loc.Count("zone.players", Players().Count);

    public override bool HandlesEnter => true;

    private static List<Rules.Player> Players()
    {
        var result = new List<Rules.Player>();
        var players = GameStateCache.State?.Players;
        for (int i = 0; players != null && i < players.Count; i++)
        {
            Rules.Player player = players[i];
            if (player == null)
                continue;
            if (player.IsLocalPlayer())
                result.Insert(0, player);
            else
                result.Add(player);
        }

        return result;
    }

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        foreach (Rules.Player player in Players())
        {
            Rules.Player current = player;
            items.Add(new ZoneItem { Summary = GameReader.PlayerSummary(player), Details = () => GameReader.PlayerLines(current), Tag = player });
        }

        return items;
    }

    public override void Activate(ZoneItem item)
    {
        if (item?.Tag is not Rules.Player player)
            return;
        ZoneNavigator.BoardOwner = player.IsLocalPlayer() ? null : player;
        Speech.ScreenReader.SayTransient(ZoneNavigator.BoardOwner == null
            ? Loc.Get("zone.board_own")
            : Loc.Get("zone.board_of", GameReader.PlayerName(player)));

        // The tutorial asks to click a player's portrait (to look at their board, or back at yours):
        // the gamepad does that with the player preview button, which shows the next player's board.
        if (TutorialAnnouncer.WantedPortrait() != null)
        {
            Plugin.Logger.LogInfo($"[Zone] previewing the board of {player.P_ID} for the tutorial");
            KeyboardBridge.Press(VirtualGamepad.Buttons.PreviewPlayerRight);
        }
    }
}

/// <summary>The four round goals with the standings.</summary>
internal sealed class GoalsZone : Zone
{
    public override string Title() => Loc.Get("zone.goals");

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        for (int round = 1; round <= 4; round++)
        {
            string goal = GameReader.GoalText(round);
            if (goal == null)
                continue;
            int current = round;
            string standings = GoalMatDescriber.ModelStandings(round);
            string summary = Loc.Get("game.goal", round, goal);
            items.Add(new ZoneItem
            {
                Summary = standings == null ? summary : $"{summary}: {standings}",
                Details = () =>
                {
                    var lines = new List<string> { Loc.Get("game.goal", current, goal) };
                    string explanation = GameReader.GoalExplanation(current);
                    if (!string.IsNullOrEmpty(explanation) && explanation != goal)
                        lines.Add(explanation);
                    if (standings != null)
                        lines.Add(standings);
                    return lines;
                },
            });
        }

        return items;
    }
}
