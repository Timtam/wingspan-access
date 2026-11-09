using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WingspanAccess.Input;

/// <summary>
/// Translates the keyboard into presses of the virtual gamepad.
/// Every press is held for at least <see cref="MinimumHoldFrames"/> frames so that
/// Rewired sees short taps, too. Nothing is translated while a text field has focus.
/// </summary>
internal static class KeyboardBridge
{
    private const int MinimumHoldFrames = 2;

    private sealed class Binding
    {
        public KeyCode Key;
        public bool Shift;
        public Func<int> Button;
        public int FramesLeft;
    }

    /// <summary>
    /// Keyboard layout (gamepad equivalent in brackets). The glyph.* strings in the localization
    /// files name these keys, so the game's controller hints read as keyboard hints.
    /// </summary>
    private static readonly List<Binding> ButtonBindings = new()
    {
        new Binding { Key = KeyCode.Return, Button = () => VirtualGamepad.Buttons.Submit },          // A
        new Binding { Key = KeyCode.KeypadEnter, Button = () => VirtualGamepad.Buttons.Submit },     // A
        new Binding { Key = KeyCode.Escape, Button = () => VirtualGamepad.Buttons.Back },            // B
        new Binding { Key = KeyCode.Space, Button = () => VirtualGamepad.Buttons.Accept },           // Y
        new Binding { Key = KeyCode.X, Button = () => VirtualGamepad.Buttons.Context },              // X: trade, reroll ...
        new Binding { Key = KeyCode.F10, Button = () => VirtualGamepad.Buttons.Pause },              // Start/Plus
        new Binding { Key = KeyCode.Tab, Button = () => VirtualGamepad.Buttons.NextCard },
        new Binding { Key = KeyCode.Tab, Shift = true, Button = () => VirtualGamepad.Buttons.PreviousCard },
        new Binding { Key = KeyCode.G, Button = () => VirtualGamepad.Buttons.ToggleGoals },              // Minus: goal board
        new Binding { Key = KeyCode.O, Button = () => VirtualGamepad.Buttons.ToggleHabitatsOverview },   // overview
        new Binding { Key = KeyCode.I, Button = () => VirtualGamepad.Buttons.ShowTooltips },             // right stick: tooltips
        new Binding { Key = KeyCode.H, Button = () => VirtualGamepad.Buttons.HideInterface },            // left stick: hide the interface
    };

    private static int _upFrames, _downFrames, _leftFrames, _rightFrames;

    /// <summary>
    /// Carousel screens without horizontal navigation: there, Left/Right page through the carousel
    /// (the gamepad shoulder buttons) instead of moving the selection.
    /// </summary>
    private static readonly Dictionary<string, (Func<int> Left, Func<int> Right)> PagingScreens = new()
    {
        ["CustomGameChooseMode_Popup"] = (() => VirtualGamepad.Buttons.PreviewPlayerLeft, () => VirtualGamepad.Buttons.PreviewPlayerRight),
        // Goal board and nectar board tabs.
        ["GoalAndNectarMat_Screen"] = (() => VirtualGamepad.Buttons.PreviewPlayerLeft, () => VirtualGamepad.Buttons.PreviewPlayerRight),
    };

    private static int _pageLeftFrames, _pageRightFrames;

    /// <summary>Screens whose controls the game's arrow navigation does not reach (see <see cref="MoveSelection"/>).</summary>
    private static readonly HashSet<string> OwnNavigationScreens = new() { "FriendsList_Screen" };

    private static readonly (KeyCode Key, int X, int Y)[] Directions =
    {
        (KeyCode.LeftArrow, -1, 0), (KeyCode.RightArrow, 1, 0), (KeyCode.UpArrow, 0, 1), (KeyCode.DownArrow, 0, -1),
    };

    /// <summary>Whether a text field was being edited in the previous frame.</summary>
    private static bool _wasEditing;

    /// <summary>Keys that just left a text field; they do nothing else until they are released.</summary>
    private static readonly HashSet<KeyCode> Swallowed = new();

    /// <summary>Buttons the mod presses by itself (opening the hand, closing it again), with frames left.</summary>
    private static readonly Dictionary<int, int> OneShots = new();

    /// <summary>Every button the bridge drives, so released buttons are reset as well.</summary>
    private static IEnumerable<int> ManagedButtons()
    {
        foreach (Binding binding in ButtonBindings)
            yield return binding.Button();
        yield return VirtualGamepad.Buttons.PreviewPlayerLeft;
        yield return VirtualGamepad.Buttons.PreviewPlayerRight;
        yield return VirtualGamepad.Buttons.ToggleCardPreview;
        yield return VirtualGamepad.Buttons.HabitatUp;
        yield return VirtualGamepad.Buttons.HabitatDown;
    }

    /// <summary>Presses a gamepad button once (held for a few frames so Rewired sees it).</summary>
    public static void Press(int button) => OneShots[button] = MinimumHoldFrames + 1;

    public static void Tick()
    {
        if (!VirtualGamepad.IsActive)
            return;

        // The text field may end its input on Enter before this runs; the frame after editing still
        // counts, or Enter would turn into "select" and open the field again.
        bool editing = TextFieldHasFocus();
        bool wasEditing = _wasEditing;
        _wasEditing = editing;
        if ((editing || wasEditing) && LeaveTextField())
        {
            ReleaseAll();
            return;
        }

        if (editing)
        {
            ReleaseAll();
            return;
        }

        Swallowed.RemoveWhere(key => !UnityEngine.Input.GetKey(key));

        GamepadScreen top = UI.Screens.Top();

        // In the starting choice the game leads Up from the food row to the rightmost card; it goes to
        // the card right above instead.
        TrackSetupCard(top);
        if (!UI.Zones.ZoneNavigator.IsActive && UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) && SelectSetupCardAbove(top))
            Swallowed.Add(KeyCode.UpArrow);
        string topType = top == null ? null : top.GetIl2CppType().Name;
        bool paging = topType != null && PagingScreens.ContainsKey(topType);

        // A modifier pressed and released within one frame (fast typing, synthetic input) still counts.
        bool shift = Modifier(KeyCode.LeftShift) || Modifier(KeyCode.RightShift);
        bool control = Modifier(KeyCode.LeftControl) || Modifier(KeyCode.RightControl);
        // While a zone is open the arrows, Escape (and for some zones Enter) belong to the zone.
        bool zone = UI.Zones.ZoneNavigator.IsActive;
        var pressedButtons = new HashSet<int>();
        foreach (Binding binding in ButtonBindings)
        {
            if (Swallowed.Contains(binding.Key))
            {
                binding.FramesLeft = 0;
                continue;
            }

            if (zone && (binding.Key == KeyCode.Escape ||
                ((binding.Key == KeyCode.Return || binding.Key == KeyCode.KeypadEnter) && UI.Zones.ZoneNavigator.ConsumesEnter)))
            {
                binding.FramesLeft = 0;
                continue;
            }

            // Escape answers the game's yes/no questions itself (see ConfirmPopupKeys).
            if (binding.Key == KeyCode.Escape && (UI.ConfirmPopupKeys.Swallowing || UI.ConfirmPopupKeys.Owns(top)))
            {
                binding.FramesLeft = 0;
                continue;
            }

            bool modifiersMatch = binding.Shift == shift && !control;
            if (UnityEngine.Input.GetKeyDown(binding.Key) && modifiersMatch)
                binding.FramesLeft = MinimumHoldFrames;

            bool held = binding.FramesLeft > 0 || (UnityEngine.Input.GetKey(binding.Key) && modifiersMatch);
            if (binding.FramesLeft > 0)
                binding.FramesLeft--;
            if (held)
                pressedButtons.Add(binding.Button());
        }

        foreach (int button in new List<int>(OneShots.Keys))
        {
            pressedButtons.Add(button);
            if (--OneShots[button] <= 0)
                OneShots.Remove(button);
        }

        // Lists the game's own arrow navigation does not move through (the friend list): the mod moves
        // the selection to the nearest control in the arrow's direction instead.
        if (!zone && topType != null && OwnNavigationScreens.Contains(topType))
        {
            foreach (var (key, dx, dy) in Directions)
            {
                if (!UnityEngine.Input.GetKeyDown(key) || shift || control)
                    continue;
                if (SwitchFriendList(top, dy) || MoveSelection(top, dx, dy))
                    Swallowed.Add(key);
            }
        }

        float x;
        if (zone)
        {
            x = 0f;
        }
        else if (paging || HandCardSelected())
        {
            // Carousels page with the shoulder buttons; the hand moves between cards like Tab does.
            (Func<int> left, Func<int> right) = paging
                ? PagingScreens[topType]
                : (() => VirtualGamepad.Buttons.PreviousCard, () => VirtualGamepad.Buttons.NextCard);
            if (Held(KeyCode.LeftArrow, ref _pageLeftFrames) > 0f && !shift && !control)
                pressedButtons.Add(left());
            if (Held(KeyCode.RightArrow, ref _pageRightFrames) > 0f && !shift && !control)
                pressedButtons.Add(right());
            x = 0f;
        }
        else
        {
            x = Held(KeyCode.RightArrow, ref _rightFrames) - Held(KeyCode.LeftArrow, ref _leftFrames);
        }

        foreach (int button in ManagedButtons())
            VirtualGamepad.SetButton(button, pressedButtons.Contains(button));

        float y = zone ? 0f : Held(KeyCode.UpArrow, ref _upFrames) - Held(KeyCode.DownArrow, ref _downFrames);
        VirtualGamepad.SetAxis(VirtualGamepad.Axes.NavigationX, x);
        VirtualGamepad.SetAxis(VirtualGamepad.Axes.NavigationY, y);
    }

    /// <summary>Whether the hand is open with one of its cards selected (the card's parent is "PlayerHand").</summary>
    private static bool HandCardSelected()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        Transform parent = selected == null ? null : selected.transform.parent;
        return parent != null && parent.name == "PlayerHand" && UI.HandAnnouncer.IsOpen();
    }

    private static bool Modifier(KeyCode key) => UnityEngine.Input.GetKey(key) || UnityEngine.Input.GetKeyDown(key) || UnityEngine.Input.GetKeyUp(key);

    private static float Held(KeyCode key, ref int framesLeft)
    {
        if (Swallowed.Contains(key))
        {
            framesLeft = 0;
            return 0f;
        }

        if (UnityEngine.Input.GetKeyDown(key))
            framesLeft = MinimumHoldFrames;
        bool held = framesLeft > 0 || UnityEngine.Input.GetKey(key);
        if (framesLeft > 0)
            framesLeft--;
        return held ? 1f : 0f;
    }

    private static void ReleaseAll()
    {
        foreach (Binding binding in ButtonBindings)
            binding.FramesLeft = 0;
        OneShots.Clear();
        foreach (int button in ManagedButtons())
            VirtualGamepad.SetButton(button, false);

        _upFrames = _downFrames = _leftFrames = _rightFrames = 0;
        VirtualGamepad.SetAxis(VirtualGamepad.Axes.NavigationX, 0f);
        VirtualGamepad.SetAxis(VirtualGamepad.Axes.NavigationY, 0f);
    }

    /// <summary>
    /// Enter, Tab or Down in a one-line text field (the online name, a player to invite) end the input
    /// and move to the nearest control below, usually the popup's confirm button; Up and Shift+Tab
    /// move to the control above. The game itself leaves its text fields only through the on-screen
    /// keyboard of a controller. Returns whether a key was used for that.
    /// </summary>
    private static bool LeaveTextField()
    {
        bool shift = Modifier(KeyCode.LeftShift) || Modifier(KeyCode.RightShift);
        KeyCode key;
        bool down;
        if (UnityEngine.Input.GetKeyDown(KeyCode.Return))
            (key, down) = (KeyCode.Return, true);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            (key, down) = (KeyCode.KeypadEnter, true);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            (key, down) = (KeyCode.Tab, !shift);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            (key, down) = (KeyCode.DownArrow, true);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            (key, down) = (KeyCode.UpArrow, false);
        else
            return false;

        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        TMPro.TMP_InputField field = selected == null ? null : selected.GetComponent<TMPro.TMP_InputField>();
        if (field == null || field.lineType == TMPro.TMP_InputField.LineType.MultiLineNewline)
            return false;

        UnityEngine.UI.Selectable target = NearestControl(field, down);
        if (target == null)
            return false;

        field.DeactivateInputField(false);
        eventSystem.SetSelectedGameObject(target.gameObject);
        Swallowed.Add(key);
        return true;
    }

    /// <summary>The card last selected in the starting choice, where Up from the food row returns to.</summary>
    private static GameObject _lastSetupCard;

    /// <summary>
    /// From a food toggle of the starting choice: selects the card the player came from (else the one
    /// nearest above). False elsewhere.
    /// </summary>
    private static bool SelectSetupCardAbove(GamepadScreen top)
    {
        if (top == null || top.TryCast<PlayerResourcesSetup_Decision>() == null)
            return false;
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        if (selected == null || selected.GetComponent<UnityEngine.UI.Toggle>() == null || selected.GetComponentInParent<GamepadCardSlot>() != null)
            return false;

        UnityEngine.UI.Selectable last = _lastSetupCard == null ? null : _lastSetupCard.GetComponent<UnityEngine.UI.Selectable>();
        if (last != null && last.interactable && last.gameObject.activeInHierarchy)
        {
            Plugin.Logger.LogInfo($"[Setup] Up from {selected.name}: back to {last.name}");
            last.Select();
            return true;
        }

        Vector3 position = selected.transform.position;
        UnityEngine.UI.Selectable best = null;
        float bestDistance = float.MaxValue;
        foreach (GamepadCardSlot slot in UnityEngine.Object.FindObjectsOfType<GamepadCardSlot>())
        {
            UnityEngine.UI.Selectable card = slot.GetSelectable();
            if (card == null || !card.interactable || !card.gameObject.activeInHierarchy || card.transform.position.y <= position.y)
                continue;
            float distance = Mathf.Abs(card.transform.position.x - position.x);
            if (distance < bestDistance)
            {
                best = card;
                bestDistance = distance;
            }
        }

        Plugin.Logger.LogInfo($"[Setup] Up from {selected.name} at {position}: {(best == null ? "no card" : best.name + " at " + best.transform.position)}");
        if (best == null)
            return false;
        best.Select();
        return true;
    }

    /// <summary>Remembers the card selected in the starting choice (see <see cref="SelectSetupCardAbove"/>).</summary>
    private static void TrackSetupCard(GamepadScreen top)
    {
        if (top == null || top.TryCast<PlayerResourcesSetup_Decision>() == null)
        {
            _lastSetupCard = null;
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        if (selected != null && selected.GetComponent<GamepadCardSlot>() != null)
            _lastSetupCard = selected;
    }

    /// <summary>
    /// Selects the nearest interactive, visible control of the screen in the direction (dx, dy), the
    /// way Unity's automatic navigation would: distance along the direction plus twice the offset
    /// across it. False if there is none.
    /// </summary>
    private static bool MoveSelection(GamepadScreen top, int dx, int dy)
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        Vector3 origin = selected != null && selected.transform.IsChildOf(top.transform)
            ? Center(selected.transform)
            : Center(top.transform);

        var visibility = new UI.Visibility(top.transform);
        UnityEngine.UI.Selectable best = null;
        float bestScore = float.MaxValue;
        foreach (UnityEngine.UI.Selectable candidate in top.GetComponentsInChildren<UnityEngine.UI.Selectable>(false))
        {
            if ((selected != null && candidate.gameObject == selected) || !candidate.IsInteractable() ||
                candidate.name.EndsWith("ClickAway") || !visibility.IsVisible(candidate.transform))
                continue;
            Vector3 offset = Center(candidate.transform) - origin;
            float along = offset.x * dx + offset.y * dy;
            if (along <= 1f)
                continue;
            float across = Mathf.Abs(offset.x * dy) + Mathf.Abs(offset.y * dx);
            float score = along + 2f * across;
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        Plugin.Logger.LogInfo($"[Navigation] ({dx},{dy}) from {(selected == null ? "-" : selected.name)} at {origin}: {(best == null ? "nothing" : best.name + " at " + Center(best.transform))}");
        if (best == null)
            return false;
        best.Select();
        return true;
    }

    private static Vector3 Center(Transform transform)
    {
        RectTransform rect = transform.GetComponent<RectTransform>();
        return rect == null ? transform.position : rect.TransformPoint(rect.rect.center);
    }

    /// <summary>
    /// The friend list has two parts, the platform friends above and the friends in the game below;
    /// only one of them takes input, and the controller's shoulder buttons (LB, RB) switch between
    /// them. An arrow on a friend of the part that does not take input switches to that part first;
    /// Up from the friends in the game and Down from the search row go to the platform friends, Down
    /// from the platform friends goes to the friends in the game. False if nothing switched.
    /// </summary>
    private static bool SwitchFriendList(GamepadScreen top, int dy)
    {
        FriendsList_Screen friends = top.TryCast<FriendsList_Screen>();
        if (friends == null)
            return false;
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        bool inPlatform = selected != null && friends._platformFriendsParent != null && selected.transform.IsChildOf(friends._platformFriendsParent);
        bool inGame = selected != null && friends._inGameFriendsParent != null && selected.transform.IsChildOf(friends._inGameFriendsParent);
        bool platformActive = friends._platformFriendsCanvasGroup != null && friends._platformFriendsCanvasGroup.interactable;
        bool inGameActive = friends._inGameFriendsCanvasGroup != null && friends._inGameFriendsCanvasGroup.interactable;
        bool platformFriends = HasFriends(friends._platformFriendsButtons);
        bool inGameFriends = HasFriends(friends._inGameFriendsButtons);

        int button = 0;
        if (inPlatform && !platformActive)
            button = VirtualGamepad.Buttons.HabitatUp; // LB: the part the selection is in
        else if (inGame && !inGameActive)
            button = VirtualGamepad.Buttons.HabitatDown; // RB
        else if (dy > 0 && inGame && platformFriends)
            button = VirtualGamepad.Buttons.HabitatUp;
        else if (dy < 0 && inPlatform && inGameFriends)
            button = VirtualGamepad.Buttons.HabitatDown;
        else if (dy < 0 && !inPlatform && !inGame && platformFriends && !platformActive)
            button = VirtualGamepad.Buttons.HabitatUp;
        if (button == 0)
            return false;

        Plugin.Logger.LogInfo($"[Navigation] friend list: switching with {(button == VirtualGamepad.Buttons.HabitatUp ? "LB" : "RB")}");
        Press(button);
        return true;
    }

    private static bool HasFriends(Il2CppSystem.Collections.Generic.List<FriendButtonController> buttons)
    {
        for (int i = 0; buttons != null && i < buttons.Count; i++)
        {
            if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    /// <summary>The nearest interactive control below (or above) the field in the same screen.</summary>
    private static UnityEngine.UI.Selectable NearestControl(TMPro.TMP_InputField field, bool down)
    {
        GamepadScreen top = UI.Screens.Top();
        Transform scope = top != null && field.transform.IsChildOf(top.transform) ? top.transform : field.transform.parent;
        if (scope == null)
            return null;

        var visibility = new UI.Visibility(scope);
        float y = field.transform.position.y;
        UnityEngine.UI.Selectable best = null;
        float bestDistance = float.MaxValue;
        foreach (UnityEngine.UI.Selectable candidate in scope.GetComponentsInChildren<UnityEngine.UI.Selectable>(false))
        {
            if (candidate.gameObject == field.gameObject || !candidate.IsInteractable() || !visibility.IsVisible(candidate.transform))
                continue;
            float distance = down ? y - candidate.transform.position.y : candidate.transform.position.y - y;
            if (distance > 0f && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static bool TextFieldHasFocus()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        if (selected == null)
            return false;
        TMPro.TMP_InputField field = selected.GetComponent<TMPro.TMP_InputField>();
        return field != null && field.isFocused;
    }
}
