using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WingspanAccess.Input;

/// <summary>
/// Key state for the mod's hotkeys. Uses Unity's legacy input, which sees every key event,
/// and falls back to polling GetAsyncKeyState if legacy input is disabled in this build.
/// </summary>
internal static class Keys
{
    private static readonly Dictionary<KeyCode, int> VirtualKeys = new()
    {
        [KeyCode.UpArrow] = 0x26, [KeyCode.DownArrow] = 0x28, [KeyCode.LeftArrow] = 0x25, [KeyCode.RightArrow] = 0x27,
        [KeyCode.Return] = 0x0D, [KeyCode.KeypadEnter] = 0x0D, [KeyCode.Backspace] = 0x08,
        [KeyCode.F1] = 0x70, [KeyCode.F2] = 0x71, [KeyCode.F3] = 0x72, [KeyCode.F10] = 0x79, [KeyCode.F11] = 0x7A, [KeyCode.F12] = 0x7B,
    };

    private static readonly Dictionary<KeyCode, bool> WasDown = new();
    private static bool _useFallback;

    public static bool Pressed(KeyCode key)
    {
        if (!_useFallback)
        {
            try
            {
                return UnityEngine.Input.GetKeyDown(key);
            }
            catch (Exception e)
            {
                _useFallback = true;
                Plugin.Logger.LogWarning($"Unity legacy input unavailable, using GetAsyncKeyState: {e.Message}");
            }
        }

        return PolledPress(key);
    }

    private static bool PolledPress(KeyCode key)
    {
        if (!VirtualKeys.TryGetValue(key, out int virtualKey))
            return false;

        bool down = GameHasFocus() && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        WasDown.TryGetValue(key, out bool wasDown);
        WasDown[key] = down;
        return down && !wasDown;
    }

    private static bool GameHasFocus()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint processId);
        return processId == (uint)Environment.ProcessId;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
