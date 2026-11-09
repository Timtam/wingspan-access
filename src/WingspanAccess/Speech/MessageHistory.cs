using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;

namespace WingspanAccess.Speech;

/// <summary>
/// The spoken game events (turns, other players' moves, instructions, results) for reading back:
/// Page Up/Down go to the previous/next message, Ctrl+Page Up/Down to the first/last.
/// Focus announcements and information asked for with a key are not kept.
/// </summary>
internal static class MessageHistory
{
    private const int Capacity = 200;

    private static readonly List<string> Messages = new();

    /// <summary>Message being read back; -1 means "after the newest", where every new message puts it.</summary>
    private static int _cursor = -1;

    public static void Add(string text)
    {
        Messages.Add(text);
        if (Messages.Count > Capacity)
            Messages.RemoveAt(0);
        _cursor = -1;
    }

    public static void Tick()
    {
        bool shift = Held(KeyCode.LeftShift) || Held(KeyCode.RightShift);
        if (shift)
            return;

        bool control = Held(KeyCode.LeftControl) || Held(KeyCode.RightControl);
        if (UnityEngine.Input.GetKeyDown(KeyCode.PageUp))
            Move(control ? int.MinValue : -1);
        else if (UnityEngine.Input.GetKeyDown(KeyCode.PageDown))
            Move(control ? int.MaxValue : 1);
    }

    private static void Move(int step)
    {
        if (Messages.Count == 0)
        {
            ScreenReader.SayTransient(Loc.Get("history.empty"));
            return;
        }

        int last = Messages.Count - 1;
        int current = _cursor < 0 ? Messages.Count : _cursor;
        int target = step == int.MinValue ? 0
            : step == int.MaxValue ? last
            : current + step;

        string edge = null;
        if (target < 0)
        {
            target = 0;
            edge = "history.first";
        }
        else if (target > last)
        {
            target = last;
            edge = "history.last";
        }

        _cursor = target;
        string text = Messages[target];
        ScreenReader.SayTransient(edge == null ? text : $"{Loc.Get(edge)}: {text}");
    }

    private static bool Held(KeyCode key) =>
        UnityEngine.Input.GetKey(key) || UnityEngine.Input.GetKeyDown(key) || UnityEngine.Input.GetKeyUp(key);
}
