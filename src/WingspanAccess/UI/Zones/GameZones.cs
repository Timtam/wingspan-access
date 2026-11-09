using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Game;
using WingspanAccess.Input;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI.Zones;

/// <summary>
/// The birds in one habitat of the board being read (your own unless another player was chosen), then
/// the row's next free place: what a bird costs there in eggs and what the action gives once it is
/// there.
/// </summary>
internal sealed class HabitatZone : Zone
{
    private readonly ActionType _habitat;

    /// <summary>Marks the item for the row's next free place.</summary>
    private static readonly object NextPlaceTag = new();

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

        // Choosing the habitat for a bird: what it costs here and what it unlocks.
        PlayBird_Decision placing = PlacingBird();
        HabitatRow row = owner == null ? null : HabitatRow.Read(owner, _habitat);
        if (placing != null && row != null)
            return PlaceText(row, placing._currentData);

        // While choosing an action the habitat's action matters (Enter takes it), with what it gives now;
        // otherwise only the place.
        bool choosing = Screens.Top()?.TryCast<ChooseAction_Decision>() != null;
        string yield = choosing ? HabitatRow.Yield(_habitat, row?.Now) : null;
        if (yield != null)
            return Loc.Get("zone.habitat_action", Loc.Get("action." + _habitat), yield, birds);
        return $"{Loc.Get((choosing ? "action." : "habitat.") + _habitat)}, {birds}";
    }

    /// <summary>The bird being played while its habitat is chosen, or null.</summary>
    private static PlayBird_Decision PlacingBird()
    {
        PlayBird_Decision decision = Screens.Find<PlayBird_Decision>();
        var state = decision?.CurrentState;
        return state != null && state.TryCast<View.Decisions.PlayBird.States.HabitatSelectionStateController>() != null ? decision : null;
    }

    /// <summary>"Wald, Platz 3: kostet 1 Ei, schaltet frei: 2 Futter".</summary>
    private string PlaceText(HabitatRow row, PlayBirdData bird)
    {
        string habitat = Loc.Get("habitat." + _habitat);
        if (row.Full)
            return Loc.Get("zone.habitat_full", habitat);
        string cost = EggCostText(HabitatRow.EggCost(row.Used, bird?.BirdData, bird?.EggsDiscountChosenByPlayer ?? 0));
        string unlocks = HabitatRow.Yield(_habitat, row.Next);
        return unlocks == null
            ? Loc.Get("zone.habitat_place_cost", habitat, row.NextPlace, cost)
            : Loc.Get("zone.habitat_place", habitat, row.NextPlace, cost, unlocks);
    }

    private static string EggCostText(int eggs) => eggs <= 0 ? Loc.Get("zone.egg_cost_none") : Loc.Count("zone.egg_cost", eggs);

    /// <summary>
    /// The row's next free place: its egg cost (for the bird being played, if any) and what the action
    /// gives once a bird is there, then (Up and Down) what it gives now, after the next bird, and the
    /// egg costs of all places.
    /// </summary>
    private ZoneItem NextPlaceItem(HabitatRow row, PlayBirdData bird)
    {
        string now = HabitatRow.Yield(_habitat, row.Now);
        string next = HabitatRow.Yield(_habitat, row.Next);
        var details = new List<string>();
        string summary;
        if (row.Full)
        {
            summary = now == null ? Loc.Get("zone.no_place_plain") : Loc.Get("zone.no_place", now);
            details.Add(summary);
        }
        else
        {
            string cost = EggCostText(HabitatRow.EggCost(row.Used, bird?.BirdData, bird?.EggsDiscountChosenByPlayer ?? 0));
            summary = next == null
                ? Loc.Get("zone.next_place_cost", row.NextPlace, row.Places, cost)
                : Loc.Get("zone.next_place", row.NextPlace, row.Places, cost, next);
            if (now != null)
                details.Add(Loc.Get("zone.yield_now", now));
            if (next != null)
                details.Add(Loc.Get("zone.yield_next", next));
        }

        details.Add(Loc.Get("zone.egg_costs", HabitatRow.EggCostList()));
        return new ZoneItem { Summary = summary, Details = () => details, Tag = NextPlaceTag };
    }

    /// <summary>The next place stands for the habitat itself: while a bird's habitat is chosen, Enter takes this one.</summary>
    public override bool Select(ZoneItem item) =>
        ReferenceEquals(item.Tag, NextPlaceTag) ? PlacingBird() != null : base.Select(item);

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
                    List<string> lines = view != null ? CardDescriber.BirdLines(view) : CardDescriber.BirdLines(bird.ID, bird);
                    return CardDescriber.WithFacts(lines, bird.ID, view);
                },
                Control = own ? () => BoardControl(bird) : null,
                BirdId = bird.ID,
            });
        }

        HabitatRow row = Owner == null ? null : HabitatRow.Read(Owner, _habitat);
        if (row != null)
            items.Add(NextPlaceItem(row, own ? PlacingBird()?._currentData : null));
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
    /// The face-up cards. Only cards with a view in the tray count, so the list matches the screen
    /// while cards are taken and the tray is refilled.
    /// </summary>
    private static List<int> TrayCards()
    {
        List<int> ids = GameReader.TrayBirds();
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
                    List<string> lines = view != null ? CardDescriber.BirdLines(view) : CardDescriber.BirdLines(id, null);
                    return CardDescriber.WithFacts(lines, id, view);
                },
                Control = () => TrayControl(id),
                BirdId = id,
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

        Add(items, Loc.Count("game.bird_cards", GameReader.HandCount(player)));
        Add(items, Loc.Count("game.bonus_cards", GameReader.BonusCount(player)));
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

    public override string Title()
    {
        List<CardView> cards = Cards();
        int bonus = cards.Count(card => card.TryCast<BonusCardView>() != null);
        if (bonus == 0)
            return Loc.Count("zone.hand", cards.Count);
        // The bonus cards sit in the hand after the birds: "Hand: 2 Vogelkarten, 1 Bonuskarte".
        return Loc.Get("zone.hand_parts", Loc.Count("game.bird_cards", cards.Count - bonus), Loc.Count("game.bonus_cards", bonus));
    }

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
        // The card preview button closes the hand again; Back does not while an action is chosen.
        if (_opened && Hand != null && Hand.IsInCardPreview)
            KeyboardBridge.Press(VirtualGamepad.Buttons.ToggleCardPreview);
    }

    /// <summary>Frame since which the hand has been open outside zone 7 (0: it is not).</summary>
    private static int _leftOpenSince;

    /// <summary>Frame at which the closing is checked (0: no closing under way).</summary>
    private static int _checkClosedFrame;

    /// <summary>The closing did not work; no new try until the hand is closed or the task changes.</summary>
    private static bool _gaveUp;

    /// <summary>
    /// A card tried in a habitat it does not fit in brings up the game's error popup; the hand then
    /// stays open with the card chosen, and every habitat action tries to play that card again. While
    /// an action is chosen outside zone 7 the hand is closed again with the card preview button (the
    /// way it was opened), once, and "hand closed" is said only when it worked. Not in the tutorial,
    /// whose tasks open the hand themselves.
    /// </summary>
    public static void CloseLeftoverHand(GamepadScreen top)
    {
        PlayerHandUI hand = Hand;
        bool choosing = top != null && top.TryCast<ChooseAction_Decision>() != null;
        bool open = hand != null && hand.IsInCardPreview;

        if (_checkClosedFrame > 0)
        {
            if (Time.frameCount < _checkClosedFrame)
                return;
            _checkClosedFrame = 0;
            if (open)
            {
                Plugin.Logger.LogInfo("[Zone] the hand did not close");
                _gaveUp = true;
            }
            else
            {
                ScreenReader.SayTransient(Loc.Get("zone.hand_closed"), interrupt: false);
            }

            return;
        }

        if (!choosing || !open)
        {
            _leftOpenSince = 0;
            _gaveUp = false;
            return;
        }

        if (_gaveUp)
            return;

        // Leaving zone 7 closes the hand itself a few frames later; only a hand that stays open counts.
        if (_leftOpenSince == 0)
        {
            _leftOpenSince = Time.frameCount;
            return;
        }

        if (Time.frameCount - _leftOpenSince < 45 || Object.FindObjectOfType<Tutorial.TutorialDriver>() != null)
            return;
        _leftOpenSince = 0;
        Plugin.Logger.LogInfo("[Zone] closing the hand left open outside zone 7");
        KeyboardBridge.Press(VirtualGamepad.Buttons.ToggleCardPreview);
        _checkClosedFrame = Time.frameCount + 20;
    }

    public override List<ZoneItem> Items()
    {
        var items = new List<ZoneItem>();
        foreach (CardView card in Cards())
        {
            CardView current = card;
            BaseBirdCardView bird = card.TryCast<BaseBirdCardView>();
            int birdId = bird == null ? 0 : (int)bird.ID;
            items.Add(new ZoneItem
            {
                Summary = CardDescriber.HandSummary(card),
                Details = () => birdId > 0 ? CardDescriber.WithFacts(CardDescriber.CardLines(current), birdId, bird) : CardDescriber.CardLines(current),
                Tag = card,
                BirdId = birdId,
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

    /// <summary>The card the zone is moving the game's selection to while the game catches up (null: none).</summary>
    private CardView _target;

    /// <summary>Frame of the next try toward the target, and the frame after which the zone gives up.</summary>
    private int _nextTry, _giveUpFrame;

    /// <summary>Tries so far to get a first card selected for the target.</summary>
    private int _tries;

    /// <summary>Whether the card preview button was pressed for the target (once).</summary>
    private bool _previewPressed;

    public override bool SelectionPending => _target != null;

    public override void Tick()
    {
        if (_target == null || Time.frameCount < _nextTry)
            return;
        if (Time.frameCount > _giveUpFrame)
        {
            Plugin.Logger.LogInfo($"[Zone] hand: gave up selecting {_target.name}; {State()}");
            _target = null;
            return;
        }

        StepToTarget();
    }

    /// <summary>Moves the game's own hand selection to the card, the way the gamepad does.</summary>
    public override bool Select(ZoneItem item)
    {
        _target = null;
        PlayerHandUI hand = Hand;
        if (hand == null || item.Tag is not CardView card || !(hand.IsInCardPreview || IsHandTask(Task())))
            return false;

        _target = card;
        _tries = 0;
        _previewPressed = false;
        _nextTry = 0;
        _giveUpFrame = Time.frameCount + 180;
        return StepToTarget();
    }

    /// <summary>The decision the hand is used for, also under a tutorial bubble.</summary>
    private static Decision Task() => Screens.Find<Decision>();

    /// <summary>
    /// Steps the game's hand selection toward the target; true once the target is selected. While
    /// playing a bird the hand's own steps move it (the card preview is open). Other tasks (discarding,
    /// tucking) use their own "next card" and "previous card", which the gamepad's shoulder buttons and
    /// Tab call. Where the game needs a few frames for the first card, Tick tries again.
    /// </summary>
    private bool StepToTarget()
    {
        PlayerHandUI hand = Hand;
        List<ZoneItem> items = Items();
        int target = _target == null ? -1 : items.FindIndex(other => other.Tag is CardView card && card.GetInstanceID() == _target.GetInstanceID());
        if (hand == null || target < 0)
        {
            _target = null;
            return false;
        }

        Decision task = Task();
        bool playing = task == null || task.TryCast<ChooseAction_Decision>() != null;
        for (int guard = 0; guard < 20; guard++)
        {
            int current = SelectedIndex(items);
            if (current == target)
            {
                _target = null;
                return true;
            }

            if (current >= 0)
            {
                Step(playing ? null : task, hand, current < target);
                continue;
            }

            if (playing)
            {
                // The open hand without a selected card: one step selects one.
                if (guard > 0)
                    break;
                hand.NextCard();
                continue;
            }

            // Discarding or tucking starts without a selected card. Like the gamepad, the task's "next
            // card" selects the first one (the hand's own steps do nothing there: its input mode stays
            // Mouse); a closed hand is opened with the card preview button first.
            _tries++;
            string how;
            if (!hand.IsInCardPreview && !_previewPressed)
            {
                how = "the card preview button";
                _previewPressed = true;
                KeyboardBridge.Press(VirtualGamepad.Buttons.ToggleCardPreview);
            }
            else
            {
                how = "the task's next card";
                task.NextCard();
            }

            if (SelectedIndex(items) >= 0)
                continue;
            Plugin.Logger.LogInfo($"[Zone] hand: no card selected after {how} (try {_tries}); {State()}");
            _nextTry = Time.frameCount + 20;
            return false;
        }

        _target = null;
        return false;
    }

    private static void Step(Decision task, PlayerHandUI hand, bool next)
    {
        if (task != null && next)
            task.NextCard();
        else if (task != null)
            task.PreviousCard();
        else if (next)
            hand.NextCard();
        else
            hand.PreviousCard();
    }

    /// <summary>The hand's state for the log: card preview, mode, the selected card, the task's choice.</summary>
    public static string State()
    {
        PlayerHandUI hand = Hand;
        if (hand == null)
            return "no hand";
        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        Decision task = Task();
        string choice = string.Empty;
        var select = task?.TryCast<View.UI.Game.Decisions.Base.BaseSelectBird_Decision>();
        if (select != null)
            choice = $", chosen {select._cardsSelected?.Count ?? 0} of {select._amountToChoose}, choice shown {select._birdCardChoice?.IsPresenting}";
        CardView card = hand.SelectedCard;
        return $"task {(task == null ? "-" : task.GetIl2CppType().Name)}, preview {hand.IsInCardPreview}/{hand.inCardsPreview}, mode {hand.handMode}, " +
            $"input {hand.InputMode}, locked {hand.HandHandlingLocked}, about to play {hand.aboutToPlayMode}, " +
            $"hand selection {(card == null ? "-" : card.name)} #{hand.carouselSelectedIndex}, " +
            $"selected {(selected == null ? "-" : UiText.PathOf(selected))}{choice}";
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

        lines.Add(Loc.Get("status.round", state.P_RoundsCounter + 1));
        lines.Add(Loc.Count("status.actions", player.P_TokensLeft));
        Rules.Player current = GameReader.CurrentPlayer(state);
        if (current != null)
            lines.Add(Loc.Get("status.current", current.IsLocalPlayer() ? Loc.Get("zone.you") : GameReader.PlayerName(current)));
        lines.AddRange(OnlineTimerWatcher.TimeLines());
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
