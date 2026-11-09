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

        if (TextFieldHasFocus())
        {
            ReleaseAll();
            return;
        }

        GamepadScreen top = UI.Screens.Top();
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
            if (zone && (binding.Key == KeyCode.Escape ||
                ((binding.Key == KeyCode.Return || binding.Key == KeyCode.KeypadEnter) && UI.Zones.ZoneNavigator.ConsumesEnter)))
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

    private static bool TextFieldHasFocus()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        if (selected == null)
            return false;
        TMPro.TMP_InputField field = selected.GetComponent<TMPro.TMP_InputField>();
        return field != null && field.isFocused;
    }
}
