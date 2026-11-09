using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Game;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI.Zones;

/// <summary>
/// Zones: a key enters a part of the game (1 to 3 habitats, 4 birdfeeder, 5 tray, 6 supply, 7 hand,
/// 8 status, 9 players, 0 round goals). Inside a zone Left/Right move between its items, Home/End
/// jump to the first/last one, Up/Down read the item line by line and Escape goes back to the game.
/// Enter and Space stay with the game: where the current task offers the item, the cursor selects it
/// in the game as well, so Enter acts on it. A screen over the zone's task (a yes/no question, the
/// goal board) pauses the zone until the task is back on top; a new task ends it.
/// </summary>
internal static class ZoneNavigator
{
    /// <summary>The key of each zone. Change it here to put zones on other keys.</summary>
    private static readonly (KeyCode Key, Func<Zone> Create)[] ZoneKeys =
    {
        (KeyCode.Alpha1, () => new HabitatZone(ActionType.GainFood)),
        (KeyCode.Alpha2, () => new HabitatZone(ActionType.LayEggs)),
        (KeyCode.Alpha3, () => new HabitatZone(ActionType.DrawBirds)),
        (KeyCode.Alpha4, () => new BirdfeederZone()),
        (KeyCode.Alpha5, () => new TrayZone()),
        (KeyCode.Alpha6, () => new SupplyZone()),
        // While the starting cards are chosen, they take the place of the still empty hand.
        (KeyCode.Alpha7, () => StartCardsZone.Applies() ? new StartCardsZone() : new HandZone()),
        (KeyCode.Alpha8, () => new StatusZone()),
        (KeyCode.Alpha9, () => new PlayersZone()),
        (KeyCode.Alpha0, () => new GoalsZone()),
    };

    /// <summary>In-game screens that keep their own arrow navigation.</summary>
    private static readonly HashSet<string> MenuScreens = new() { "PauseMenu_Screen", "OptionsMenuPause" };

    private static Zone _zone;
    private static KeyCode _zoneKey;
    private static int _index = -1;
    private static int _line = -1;
    private static List<string> _lines;
    private static int _screenId;

    /// <summary>Another screen covers the zone's task; the keys belong to that screen meanwhile.</summary>
    private static bool _paused;

    /// <summary>Frame at which "back in zone" is said after a pause (0: nothing to say).</summary>
    private static int _resumeFrame;

    /// <summary>Whose board 1 to 3 read; null for your own. Chosen in the players zone.</summary>
    public static Rules.Player BoardOwner;

    public static bool IsActive => _zone != null && !_paused;

    /// <summary>The bird under the cursor of the open zone, 0 if there is none.</summary>
    public static int CurrentBirdId
    {
        get
        {
            if (!IsActive || _index < 0)
                return 0;
            List<ZoneItem> items = _zone.Items();
            return _index < items.Count ? items[_index].BirdId : 0;
        }
    }

    /// <summary>
    /// The item under the cursor could not be selected in the game although the task lets zones
    /// select (a tray card while choosing the starting cards): Enter would act on whatever the game
    /// has selected instead.
    /// </summary>
    private static bool _enterBlocked;

    /// <summary>Whether Enter belongs to the zone (choosing a player, or nothing to choose) instead of the game.</summary>
    public static bool ConsumesEnter => IsActive && (_zone.HandlesEnter || _enterBlocked);

    public static void Tick()
    {
        GamepadScreen top = Screens.Top();
        int screenId = top == null ? 0 : top.GetInstanceID();
        if (_zone != null)
            FollowScreen(screenId);

        // Another player's board stays chosen until "You" is chosen in zone 9 or your next turn starts.
        if (BoardOwner != null && (GameStateCache.State == null || (top != null && top.TryCast<NewTurnAnnouncement_Popup>() != null)))
            BoardOwner = null;

        bool modifier = Held(KeyCode.LeftControl) || Held(KeyCode.RightControl) || Held(KeyCode.LeftAlt) || Held(KeyCode.RightAlt);
        if (!modifier)
        {
            foreach (var (key, create) in ZoneKeys)
            {
                if (!UnityEngine.Input.GetKeyDown(key))
                    continue;
                if (InGame(top))
                    Enter(key, create, screenId);
                else if (top != null && top.TryCast<HabitatsOverview_Screen>() != null)
                    ScreenReader.SayTransient(Loc.Get("overview.still_open"));
                else if (top != null && IsGameScreen(top))
                    ScreenReader.SayTransient(Loc.Get("game.not_running"));
                return;
            }
        }

        if (_zone == null)
        {
            HandZone.CloseLeftoverHand(top);
            return;
        }

        if (_paused)
            return;
        if (_resumeFrame > 0 && Time.frameCount >= _resumeFrame)
        {
            _resumeFrame = 0;
            ScreenReader.Say(Loc.Get("zone.resumed", (int)(_zoneKey - KeyCode.Alpha0)), interrupt: false, history: false);
        }

        _zone.Tick();
        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            Leave(announce: true);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            Move(1);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            Move(-1);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.Home))
            MoveTo(0);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.End))
            MoveTo(int.MaxValue);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            Detail(1);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            Detail(-1);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_zone.HandlesEnter)
                Activate();
            else if (_enterBlocked)
                ScreenReader.SayTransient(Loc.Get("zone.not_selectable"));
        }
    }

    /// <summary>
    /// A screen over the zone's task pauses the zone, and it goes on when the task is back on top
    /// (after "Skip this power?" while drawing, say). So does a choice the task opens over its own
    /// controls, like nectar or seed for a die taken in zone 4. When the task is gone, the zone ends.
    /// </summary>
    private static void FollowScreen(int screenId)
    {
        bool onTop = screenId == _screenId;
        if (!onTop && !Screens.Contains(_screenId))
        {
            Leave(announce: false);
            return;
        }

        GameObject choice = onTop ? ChoiceInTask() : null;
        bool pause = !onTop || choice != null;
        if (pause && !_paused)
        {
            _paused = true;
            _resumeFrame = 0;
            Plugin.Logger.LogInfo($"[Zone] {_zone.GetType().Name} paused under {(choice != null ? UiText.PathOf(choice) : Screens.DescribeStack())}");
            // The game moved its selection into the choice while the zone still held the keys, so
            // the focus announcement skipped it.
            if (choice != null)
                ScreenReader.Say(UiText.Describe(choice), interrupt: false);
        }
        else if (!pause && _paused)
        {
            _paused = false;
            // After the task's own announcement, which follows its return to the top.
            _resumeFrame = Time.frameCount + 10;
            Plugin.Logger.LogInfo($"[Zone] {_zone.GetType().Name} resumed");
        }
    }

    /// <summary>
    /// The game's selection in a panel the task opened over its own controls (the decisions' panels:
    /// one of two foods for a die, a food from the supply ...). No zone selects controls there.
    /// </summary>
    private static GameObject ChoiceInTask()
    {
        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        return selected != null && selected.activeInHierarchy && UiText.PathOf(selected).Contains("/DecisionsPanels/") ? selected : null;
    }

    private static void Enter(KeyCode key, Func<Zone> create, int screenId)
    {
        _screenId = screenId;
        _paused = false;
        _resumeFrame = 0;
        if (_zone != null && key == _zoneKey)
        {
            ScreenReader.SayTransient(_zone.Title()); // the same key again repeats the overview
            return;
        }

        _zone?.OnLeave();
        _zone = create();
        _zoneKey = key;
        _index = -1;
        _enterBlocked = false;
        _line = -1;
        _lines = null;
        _zone.OnEnter();
        ScreenReader.SayTransient(_zone.Title());
    }

    private static void Leave(bool announce)
    {
        bool hand = _zone is HandZone;
        _zone?.OnLeave();
        _zone = null;
        _paused = false;
        _resumeFrame = 0;
        _enterBlocked = false;
        if (!announce)
            return;
        GamepadScreen top = Screens.Top();
        string text = Loc.Get("zone.left", top == null ? string.Empty : UiText.ScreenName(top));

        // In a task, also where the game's own selection is, which the arrow keys move from now on.
        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if (!hand && top != null && top.TryCast<Decision>() != null && selected != null && selected.activeInHierarchy)
            text = UiText.JoinSentences(new[] { text, UiText.Describe(selected) });
        ScreenReader.SayTransient(text);
    }

    private static void Move(int step)
    {
        List<ZoneItem> items = _zone.Items();
        if (items.Count == 0)
        {
            ScreenReader.SayTransient(Loc.Get("zone.empty"));
            return;
        }

        // The cursor follows the game's own selection where there is one (the hand). The first
        // arrow press in a zone reads the selected (or first) item instead of skipping it.
        int selected = _zone.SelectedIndex(items);
        if (_index < 0)
        {
            Show(items, selected >= 0 ? selected : 0);
            return;
        }

        Show(items, (selected >= 0 ? selected : _index) + step);
    }

    private static void MoveTo(int index)
    {
        List<ZoneItem> items = _zone.Items();
        if (items.Count == 0)
        {
            ScreenReader.SayTransient(Loc.Get("zone.empty"));
            return;
        }

        Show(items, index);
    }

    private static void Show(List<ZoneItem> items, int index)
    {
        Plugin.Logger.LogInfo($"[Zone] {_zone.GetType().Name} item {index}");
        _index = Mathf.Clamp(index, 0, items.Count - 1);
        _line = -1;
        _lines = null;
        ZoneItem item = items[_index];
        _enterBlocked = (_zone is HandZone || SelectionAllowed(Screens.Top())) && !_zone.Select(item);
        ScreenReader.SayTransient($"{item.Summary}, {Loc.Get("position", _index + 1, items.Count)}");
    }

    private static void Detail(int step)
    {
        List<ZoneItem> items = _zone.Items();
        if (items.Count == 0)
        {
            ScreenReader.SayTransient(Loc.Get("zone.empty"));
            return;
        }

        if (_index < 0 || _index >= items.Count)
        {
            Show(items, 0);
            return;
        }

        _lines ??= items[_index].Details?.Invoke() ?? new List<string> { items[_index].Summary };
        if (_lines.Count == 0)
            _lines.Add(items[_index].Summary);
        _line = Mathf.Clamp(_line < 0 ? 0 : _line + step, 0, _lines.Count - 1);
        ScreenReader.SayTransient(_lines[_line]);
    }

    private static void Activate()
    {
        List<ZoneItem> items = _zone.Items();
        if (_index >= 0 && _index < items.Count)
            _zone.Activate(items[_index]);
        else
            ScreenReader.SayTransient(Loc.Get("zone.choose_first"));
    }

    /// <summary>
    /// Board items are only selected in the game during tasks about them (laying eggs, choosing a
    /// bird ...). While choosing an action, Enter has to stay with the habitat's action.
    /// </summary>
    private static bool SelectionAllowed(GamepadScreen top) =>
        top != null && top.TryCast<Decision>() != null &&
        top.TryCast<ChooseAction_Decision>() == null && top.TryCast<WaitForOtherPlayer_Decision>() == null;

    private static bool InGame(GamepadScreen top) =>
        top != null && GameStateCache.State != null && GameStateCache.LocalPlayer != null && IsGameScreen(top) &&
        !MenuScreens.Contains(top.GetIl2CppType().Name);

    /// <summary>The board's screens, the tutorial's hint bubbles over it and the view of another player's board.</summary>
    private static bool IsGameScreen(GamepadScreen top) =>
        UiText.PathOf(top.gameObject).StartsWith("GAME/Game_Canvas") || top.TryCast<Tutorial.HintAnnouncerBase>() != null ||
        top.TryCast<PlayerPreview_Screen>() != null;

    private static bool Held(KeyCode key) => UnityEngine.Input.GetKey(key);
}
