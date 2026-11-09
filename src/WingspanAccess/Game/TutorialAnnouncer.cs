using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// The tutorial explains everything in speech bubbles (hint announcers). Their text appears letter
/// by letter, so reading the bubble would only catch a part. This reads the hint's full text as soon
/// as a bubble shows a new hint, with the bird or bonus card it shows, and reveals the text at once
/// so that the next Enter continues instead of only finishing the animation.
/// </summary>
internal static class TutorialAnnouncer
{
    private static string _lastText;
    private static int _nextCheck;
    private static Tutorial.TutorialDriver _driver;
    private static System.IntPtr _task;
    private static bool _taskKeysSpoken;
    private static int _nextDriverLookup;
    private static int _lastCardId;

    public static void Tick(GamepadScreen top)
    {
        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 6;

        // Only while a tutorial runs; the driver is looked up again now and then, since chapters reload it.
        if (Time.frameCount >= _nextDriverLookup)
        {
            _nextDriverLookup = Time.frameCount + 30;
            _driver = Object.FindObjectOfType<Tutorial.TutorialDriver>();
        }

        if (_driver == null)
        {
            _lastText = null;
            _task = System.IntPtr.Zero;
            return;
        }

        Tutorial.HintAnnouncer announcer = ActiveAnnouncer(top);
        if (announcer != null)
            AnnounceHint(top, announcer);
        else
            _lastText = null;
        AnnounceTaskKeys();
    }

    private static void AnnounceHint(GamepadScreen top, Tutorial.HintAnnouncer announcer)
    {
        // A new bubble first plays its show animation and only then starts revealing the text, so this
        // keeps checking; otherwise the first Enter would only finish the animation instead of continuing.
        if (announcer.IsRevealing)
            announcer.RevealNow();

        string raw = HintText(announcer);
        string text = UiText.Clean(raw);
        if (string.IsNullOrEmpty(text) || text == _lastText)
            return;
        _lastText = text;
        if (raw.IndexOf('<') >= 0)
            Plugin.Logger.LogInfo($"[Tutorial] raw: {raw}");

        // A card shown next to the hints is read once, not with every hint about it.
        var parts = new List<string> { text };
        CardView shown = announcer._card != null && announcer._card.gameObject.activeInHierarchy ? announcer._card
            : announcer._bonusCard != null && announcer._bonusCard.gameObject.activeInHierarchy ? announcer._bonusCard : null;
        int shownId = shown == null ? 0 : shown.GetInstanceID();
        if (shown != null && shownId != _lastCardId)
            parts.Add(CardDescriber.DescribeCard(shown));
        _lastCardId = shownId;
        // Enter continues while the bubble is the top screen, except after the last hint of a task
        // that waits for an action; then the task's key hint follows instead.
        Tutorial.Task task = _driver._currentTask;
        int hints = task?.Hints?.Count ?? 0;
        bool waitsForAction = task != null && task.TryCast<Tutorial.ReadTask>() == null && task._hintIndex >= hints;
        if (top != null && top.TryCast<Tutorial.HintAnnouncerBase>() != null && !waitsForAction)
            parts.Add(Loc.Get("tutorial.continue"));

        Plugin.Logger.LogInfo($"[Tutorial] hint in {announcer.name}, task {task?.GetIl2CppType().Name} hint {task?._hintIndex}/{hints}, continue {announcer.IsContinueEnabled}");
        ScreenReader.Say(UiText.JoinSentences(parts), interrupt: false);
    }

    /// <summary>
    /// The tutorial's hints talk about clicking. Once a task has shown all its hints and waits for the
    /// player, this says which keys do it here (e.g. "Press 2 for the grassland").
    /// </summary>
    private static void AnnounceTaskKeys()
    {
        Tutorial.Task task = _driver._currentTask;
        if (task == null)
        {
            _task = System.IntPtr.Zero;
            return;
        }

        if (task.Pointer != _task)
        {
            _task = task.Pointer;
            _taskKeysSpoken = false;
            Plugin.Logger.LogInfo($"[Tutorial] task {task.GetIl2CppType().Name}");
        }

        int hints = task.Hints?.Count ?? 0;
        if (_taskKeysSpoken || task._hintIndex < hints || task.TryCast<Tutorial.ReadTask>() != null)
            return;
        _taskKeysSpoken = true;
        string keys = TaskKeys(task);
        if (keys != null)
            ScreenReader.Say(keys, interrupt: false);
    }

    private static string TaskKeys(Tutorial.Task task)
    {
        var habitatTask = task.TryCast<Tutorial.GoToHabitatTask>();
        if (habitatTask != null)
            return Loc.Get("tutorial.keys.habitat", HabitatKey(habitatTask.Habitat), Loc.Get("habitat." + habitatTask.Habitat));

        var actionTask = task.TryCast<Tutorial.ChooseActionTask>();
        if (actionTask != null)
        {
            ActionType action = actionTask.AllowedAction;
            if (action == ActionType.PlayBird)
                return Loc.Get("tutorial.keys.play_bird");
            if (action == ActionType.GainFood || action == ActionType.LayEggs || action == ActionType.DrawBirds)
                return Loc.Get("tutorial.keys.action", HabitatKey(action), Loc.Get("habitat." + action));
            return Loc.Get("tutorial.keys.any_action");
        }

        var food = task.TryCast<Tutorial.ChooseFoodTask>();
        if (food != null)
            return Loc.Get("tutorial.keys.food", GameReader.FoodTypeName(food.Food));
        var foodDie = task.TryCast<Tutorial.ChooseSpecificFoodOnFoodDiceTask>();
        if (foodDie != null)
            return Loc.Get("tutorial.keys.food_side", GameReader.FoodTypeName(foodDie.Food));
        var foods = task.TryCast<Tutorial.ChooseMultipleFoodTask>();
        if (foods != null)
            return Loc.Get("tutorial.keys.foods", $"{GameReader.FoodTypeName(foods.Food1)}, {GameReader.FoodTypeName(foods.Food2)}");

        var card = task.TryCast<Tutorial.ChooseBirdCardTask>();
        if (card != null)
        {
            // Without a named bird, the task lists the birds it lets you click (e.g. where to lay eggs).
            var birds = new List<int>();
            if ((int)card.Bird != 0)
                birds.Add((int)card.Bird);
            for (int i = 0; birds.Count == 0 && card.ClickableBirds != null && i < card.ClickableBirds.Count; i++)
                birds.Add((int)card.ClickableBirds[i]);
            return CardKeys(birds.ToArray());
        }
        var cards = task.TryCast<Tutorial.ChooseBirdCardsTask>();
        if (cards != null)
            return CardKeys((int)cards.Bird1, (int)cards.Bird2);
        var moreCards = task.TryCast<Tutorial.ChooseMultipleBirdCardsTask>();
        if (moreCards != null)
            return CardKeys((int)moreCards.Bird1, (int)moreCards.Bird2, (int)moreCards.Bird3);
        if (task.TryCast<Tutorial.ChooseBirdCardFromDeckTask>() != null)
            return Loc.Get("tutorial.keys.deck");

        var boardBird = task.TryCast<Tutorial.ChooseSpecificBirdInHabitatTask>();
        if (boardBird != null)
            return Loc.Get("tutorial.keys.board_bird", GameReader.BirdName((int)boardBird._birdToChoose));
        if (task.TryCast<Tutorial.ChooseBirdInHabitatTask>() != null || task.TryCast<Tutorial.ChooseBirdsToPayEggsTask>() != null)
            return Loc.Get("tutorial.keys.board_any");

        if (task.TryCast<Tutorial.HideTurnStartPopupTask>() != null || task.TryCast<Tutorial.HideInterjectionPopupTask>() != null)
            return Loc.Get("tutorial.keys.enter");
        if (task.TryCast<Tutorial.ConfirmTask>() != null)
            return Loc.Get("tutorial.keys.confirm");
        if (task.TryCast<Tutorial.ClickDiscardButtonTask>() != null)
            return Loc.Get("tutorial.keys.exchange");
        if (task.TryCast<Tutorial.RerollBirdfeederTask>() != null)
            return Loc.Get("tutorial.keys.reroll");
        if (task.TryCast<Tutorial.CacheOrTakeSeedTask>() != null)
            return Loc.Get("tutorial.keys.option");
        var portrait = task.TryCast<Tutorial.ClickPlayerPortraitTask>();
        if (portrait != null)
            return Loc.Get("tutorial.keys.portrait", PlayerNameById(portrait.PlayerID));
        if (task.TryCast<Tutorial.OpenGoalsTask>() != null)
            return Loc.Get("tutorial.keys.goals");
        if (task.TryCast<Tutorial.ShowOverviewTask>() != null)
            return Loc.Get("tutorial.keys.overview");
        if (task.TryCast<Tutorial.GoBackTask>() != null)
            return Loc.Get("tutorial.keys.back");
        return null;
    }

    /// <summary>
    /// Birds to pick: from the tray (zone 5), from the hand while choosing an action (zone 7, which
    /// plays the bird), or on the decision's own screen.
    /// </summary>
    private static string CardKeys(params int[] birds)
    {
        var names = new List<string>();
        bool inTray = false, inHand = false;
        ActionType boardHabitat = ActionType.None;
        var tray = GameStateCache.State?.BirdsDeckState?.Visible;
        Rules.Player player = GameStateCache.LocalPlayer;
        // The hand as zone 7 lists it: the bird card views in the local player's hand.
        var hand = new HashSet<int>();
        var cards = HandAnnouncer.LocalHand()?.cards;
        for (int i = 0; cards != null && i < cards.Count; i++)
        {
            BirdCardView view = cards[i]?.TryCast<BirdCardView>();
            if (view != null)
                hand.Add((int)view.ID);
        }

        foreach (int bird in birds)
        {
            names.Add(GameReader.BirdName(bird));
            for (int i = 0; tray != null && i < tray.Count; i++)
                inTray |= tray[i] == bird;
            inHand |= hand.Contains(bird);
            if (player != null && HabitatKey(player.GetBirdHabitatType(bird)) > 0)
                boardHabitat = player.GetBirdHabitatType(bird);
        }

        if (names.Count == 0)
            return Loc.Get("tutorial.keys.board_any");

        // Zone 7 picks hand cards while choosing an action or a card to discard or tuck; other
        // decisions (selection screens) have their own cards.
        Decision decision = Screens.Find<Decision>();
        bool fromHand = decision == null || UI.Zones.HandZone.IsHandTask(decision);
        string list = string.Join(", ", names);
        if (inTray)
            return Loc.Get("tutorial.keys.card_tray", list);
        if (inHand && fromHand)
            return Loc.Get("tutorial.keys.card_hand", list);
        if (!inHand && boardHabitat != ActionType.None)
            return Loc.Get("tutorial.keys.board_bird_in", HabitatKey(boardHabitat), Loc.Get("habitat." + boardHabitat), list);
        return Loc.Get("tutorial.keys.card", list);
    }

    /// <summary>The player whose portrait the tutorial waits to be clicked, or null.</summary>
    public static string WantedPortrait()
    {
        var task = _driver == null ? null : _driver._currentTask?.TryCast<Tutorial.ClickPlayerPortraitTask>();
        return task?.PlayerID;
    }

    private static string PlayerNameById(string playerId)
    {
        var players = GameStateCache.State?.Players;
        for (int i = 0; players != null && i < players.Count; i++)
        {
            if (players[i] != null && (players[i].ID == playerId || players[i].P_ID == playerId))
                return players[i].IsLocalPlayer() ? Loc.Get("zone.you") : GameReader.PlayerName(players[i]);
        }

        return Loc.Get("tutorial.opponent");
    }

    private static int HabitatKey(ActionType habitat) => habitat switch
    {
        ActionType.GainFood => 1,
        ActionType.LayEggs => 2,
        ActionType.DrawBirds => 3,
        _ => 0,
    };

    /// <summary>The visible hint bubble: the top screen, the bubble the chooser used last, or any visible one.</summary>
    private static Tutorial.HintAnnouncer ActiveAnnouncer(GamepadScreen top)
    {
        Tutorial.HintAnnouncer announcer = top?.TryCast<Tutorial.HintAnnouncer>();
        if (announcer != null && announcer.IsVisible)
            return announcer;

        Tutorial.HintAnnouncerChooser chooser = top?.TryCast<Tutorial.HintAnnouncerChooser>();
        if (chooser?._lastUsed != null && chooser._lastUsed.IsVisible)
            return chooser._lastUsed;

        // Small bubbles also accompany decisions without being the top screen.
        foreach (Tutorial.HintAnnouncer candidate in Object.FindObjectsOfType<Tutorial.HintAnnouncer>())
        {
            if (candidate != null && candidate.IsVisible)
                return candidate;
        }

        return null;
    }

    /// <summary>The hint's full text (with rich text tags); the bubble's own text only while the hint gives none.</summary>
    private static string HintText(Tutorial.HintAnnouncer announcer)
    {
        string text = null;
        try
        {
            Tutorial.BaseHint hint = announcer._hint?.TryCast<Tutorial.BaseHint>();
            text = hint?.GetText();
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Tutorial hint text failed: {e.Message}");
        }

        if (string.IsNullOrEmpty(text))
            text = announcer.SpeechBubble?.HintText?.text;
        return text ?? string.Empty;
    }
}
