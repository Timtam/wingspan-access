using System;
using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Game;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI.Zones;

/// <summary>
/// Zones: a key enters a part of the game (1 to 3 habitats, 4 birdfeeder, 5 tray, 6 supply, 7 hand,
/// 8 status, 9 players, 0 round goals). Inside a zone Left/Right move between its items, Home/End
/// jump to the first/last one, Up/Down read the item line by line and Escape goes back to the game.
/// Enter and Space stay with the game: where the current task offers the item, the cursor selects it
/// in the game as well, so Enter acts on it. A new screen or task ends the zone.
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
        (KeyCode.Alpha7, () => new HandZone()),
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

    /// <summary>Whose board 1 to 3 read; null for your own. Chosen in the players zone.</summary>
    public static Rules.Player BoardOwner;

    public static bool IsActive => _zone != null;

    /// <summary>Whether Enter belongs to the zone (choosing a player) instead of the game.</summary>
    public static bool ConsumesEnter => _zone != null && _zone.HandlesEnter;

    public static void Tick()
    {
        GamepadScreen top = Screens.Top();
        int screenId = top == null ? 0 : top.GetInstanceID();
        if (_zone != null && screenId != _screenId)
            Leave(announce: false);

        bool modifier = Held(KeyCode.LeftControl) || Held(KeyCode.RightControl) || Held(KeyCode.LeftAlt) || Held(KeyCode.RightAlt);
        if (!modifier)
        {
            foreach (var (key, create) in ZoneKeys)
            {
                if (!UnityEngine.Input.GetKeyDown(key))
                    continue;
                if (InGame(top))
                    Enter(key, create, screenId);
                else if (top != null && IsGameScreen(top))
                    ScreenReader.SayTransient(Loc.Get("game.not_running"));
                return;
            }
        }

        if (_zone == null)
            return;

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
        else if (_zone.HandlesEnter && (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)))
            Activate();
    }

    private static void Enter(KeyCode key, Func<Zone> create, int screenId)
    {
        _screenId = screenId;
        if (_zone != null && key == _zoneKey)
        {
            ScreenReader.SayTransient(_zone.Title()); // the same key again repeats the overview
            return;
        }

        _zone?.OnLeave();
        _zone = create();
        _zoneKey = key;
        _index = -1;
        _line = -1;
        _lines = null;
        _zone.OnEnter();
        ScreenReader.SayTransient(_zone.Title());
    }

    private static void Leave(bool announce)
    {
        _zone?.OnLeave();
        _zone = null;
        BoardOwner = null;
        if (!announce)
            return;
        GamepadScreen top = Screens.Top();
        ScreenReader.SayTransient(Loc.Get("zone.left", top == null ? string.Empty : UiText.ScreenName(top)));
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
        _index = Mathf.Clamp(index, 0, items.Count - 1);
        _line = -1;
        _lines = null;
        ZoneItem item = items[_index];
        if (_zone is HandZone || SelectionAllowed(Screens.Top()))
            _zone.Select(item);
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

    private static bool IsGameScreen(GamepadScreen top) => UiText.PathOf(top.gameObject).StartsWith("GAME/Game_Canvas");

    private static bool Held(KeyCode key) => UnityEngine.Input.GetKey(key);
}
