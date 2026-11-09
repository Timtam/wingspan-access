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
        public bool Control;
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
        new Binding { Key = KeyCode.Menu, Button = () => VirtualGamepad.Buttons.Context },           // X
        new Binding { Key = KeyCode.F10, Shift = true, Button = () => VirtualGamepad.Buttons.Context }, // X
        new Binding { Key = KeyCode.F10, Button = () => VirtualGamepad.Buttons.Pause },              // Start/Plus
        new Binding { Key = KeyCode.PageUp, Button = () => VirtualGamepad.Buttons.HabitatUp },       // LB
        new Binding { Key = KeyCode.PageDown, Button = () => VirtualGamepad.Buttons.HabitatDown },   // RB
        new Binding { Key = KeyCode.PageUp, Control = true, Button = () => VirtualGamepad.Buttons.PreviewPlayerLeft },   // LT
        new Binding { Key = KeyCode.PageDown, Control = true, Button = () => VirtualGamepad.Buttons.PreviewPlayerRight }, // RT
        new Binding { Key = KeyCode.Tab, Button = () => VirtualGamepad.Buttons.NextCard },
        new Binding { Key = KeyCode.Tab, Shift = true, Button = () => VirtualGamepad.Buttons.PreviousCard },
        new Binding { Key = KeyCode.H, Button = () => VirtualGamepad.Buttons.ToggleCardPreview },        // D-pad up: hand
        new Binding { Key = KeyCode.T, Button = () => VirtualGamepad.Buttons.ToggleDiscards },           // D-pad down: tray preview
        new Binding { Key = KeyCode.G, Button = () => VirtualGamepad.Buttons.ToggleGoals },              // Minus: round goals
        new Binding { Key = KeyCode.O, Button = () => VirtualGamepad.Buttons.ToggleHabitatsOverview },   // overview
    };

    private static int _upFrames, _downFrames, _leftFrames, _rightFrames;

    /// <summary>
    /// Carousel screens without horizontal navigation: there, Left/Right page through the carousel
    /// (the gamepad shoulder buttons) instead of moving the selection.
    /// </summary>
    private static readonly Dictionary<string, (Func<int> Left, Func<int> Right)> PagingScreens = new()
    {
        ["CustomGameChooseMode_Popup"] = (() => VirtualGamepad.Buttons.PreviewPlayerLeft, () => VirtualGamepad.Buttons.PreviewPlayerRight),
    };

    private static int _pageLeftFrames, _pageRightFrames;

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
        var pressedButtons = new HashSet<int>();
        foreach (Binding binding in ButtonBindings)
        {
            bool modifiersMatch = binding.Shift == shift && binding.Control == control;
            if (UnityEngine.Input.GetKeyDown(binding.Key) && modifiersMatch)
                binding.FramesLeft = MinimumHoldFrames;

            bool held = binding.FramesLeft > 0 || (UnityEngine.Input.GetKey(binding.Key) && modifiersMatch);
            if (binding.FramesLeft > 0)
                binding.FramesLeft--;
            if (held)
                pressedButtons.Add(binding.Button());
        }

        float x;
        if (paging)
        {
            var (left, right) = PagingScreens[topType];
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

        foreach (Binding binding in ButtonBindings)
        {
            int button = binding.Button();
            VirtualGamepad.SetButton(button, pressedButtons.Contains(button));
        }

        float y = Held(KeyCode.UpArrow, ref _upFrames) - Held(KeyCode.DownArrow, ref _downFrames);
        VirtualGamepad.SetAxis(VirtualGamepad.Axes.NavigationX, x);
        VirtualGamepad.SetAxis(VirtualGamepad.Axes.NavigationY, y);
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
        {
            binding.FramesLeft = 0;
            VirtualGamepad.SetButton(binding.Button(), false);
        }

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
