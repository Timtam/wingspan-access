using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Input;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// Mod hotkeys that only read information: F1 lists the keys, F2 reads the whole top screen,
/// F11 announces the mod status, F12 writes the screen's texts to the log.
/// </summary>
internal static class ScreenReaderCommands
{
    public static void Tick()
    {
        if (Keys.Pressed(KeyCode.F1))
            ScreenReader.Say(Loc.Get("help.keys"));
        if (Keys.Pressed(KeyCode.F2))
            ReadScreen();
        if (Keys.Pressed(KeyCode.F3))
            ReadTooltip();
        if (Keys.Pressed(KeyCode.F11))
            AnnounceStatus();
        if (Keys.Pressed(KeyCode.F12))
            DumpTopScreen();
    }

    private static void ReadScreen()
    {
        GamepadScreen top = Screens.Top();
        if (top == null)
        {
            ScreenReader.Say(Loc.Get("debug.no_screen"));
            return;
        }

        var parts = new List<string> { UiText.ScreenName(top) };
        foreach (string text in Screens.PlainTexts(top.gameObject))
        {
            if (!parts.Contains(text))
                parts.Add(text);
        }

        ScreenReader.Say(UiText.JoinSentences(parts));
    }

    private static void ReadTooltip()
    {
        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        List<string> texts = selected == null ? new List<string>() : Tooltips.TextsFor(selected);
        ScreenReader.Say(texts.Count > 0 ? UiText.JoinSentences(texts) : Loc.Get("tooltip.none"));
    }

    private static void AnnounceStatus()
    {
        GamepadScreen top = Screens.Top();
        string screen = top == null ? "-" : UiText.ScreenName(top);
        string mode = Loc.Get(VirtualGamepad.IsActive ? "status.gamepad" : "status.keyboard");
        ScreenReader.Say(Loc.Get("status", ScreenReader.BackendName, Loc.Language, screen, mode));
    }

    private static void DumpTopScreen()
    {
        GamepadScreen top = Screens.Top();
        if (top == null)
        {
            ScreenReader.Say(Loc.Get("debug.no_screen"));
            return;
        }

        Plugin.Logger.LogInfo($"[Dump] ---- {top.GetIl2CppType().FullName} ({UiText.PathOf(top.gameObject)})");
        int count = 0;
        foreach (TMPro.TMP_Text text in top.gameObject.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            string clean = UiText.Clean(text.text);
            if (clean.Length == 0)
                continue;
            Plugin.Logger.LogInfo($"[Dump] {UiText.PathOf(text.gameObject)} => {clean}");
            count++;
        }

        foreach (UnityEngine.UI.Selectable selectable in top.gameObject.GetComponentsInChildren<UnityEngine.UI.Selectable>(false))
        {
            Plugin.Logger.LogInfo(
                $"[Dump] selectable {UiText.PathOf(selectable.gameObject)} type={selectable.GetIl2CppType().Name} " +
                $"interactable={selectable.interactable} => {UiText.Describe(selectable.gameObject)}");
        }

        ScreenReader.Say(Loc.Get("debug.dumped", count));
    }
}
