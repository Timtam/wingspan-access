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
        if (!TextFieldHasFocus())
            GameKeys();

        if (Keys.Pressed(KeyCode.F1))
            ScreenReader.Say(Loc.Get("help.keys"));
        if (Keys.Pressed(KeyCode.F2))
            ReadScreen();
        if (Keys.Pressed(KeyCode.F3))
            ReadTooltip();
        if (Keys.Pressed(KeyCode.F11))
        {
            bool control = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
            if (control)
                Diagnostics.GamepadExperiment.Run();
            else
                AnnounceStatus();
        }
        if (Keys.Pressed(KeyCode.F12))
        {
            bool control = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
            if (control && shift)
                Diagnostics.UiDump.DumpTerms(@"food|nest|habitat|egg|icon|predator|flock|tuck|cache|bonus|wingspan|power|action|round|goal|turn|score|point", 600);
            else if (control)
                Diagnostics.UiDump.DumpSpriteNames();
            else if (shift)
                ScreenReader.Say(Loc.Get("debug.dumped", Diagnostics.UiDump.DumpAllVisibleTexts()));
            else
                DumpTopScreen();
        }
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
        if (top.TryCast<Decision>() != null)
            parts.AddRange(DecisionAnnouncer.InstructionTexts(top));
        foreach (string text in Screens.PlainTexts(top.gameObject))
        {
            if (!parts.Contains(text))
                parts.Add(text);
        }

        ScreenReader.Say(UiText.JoinSentences(parts));
    }

    /// <summary>Number keys read the game state: 1-3 habitats, 4 birdfeeder, 5 tray, 6 supply, 7 hand, 8 round, 9 opponents.</summary>
    private static void GameKeys()
    {
        if (Keys.Pressed(KeyCode.Alpha1)) Game.GameReader.Habitat(ActionType.GainFood);
        else if (Keys.Pressed(KeyCode.Alpha2)) Game.GameReader.Habitat(ActionType.LayEggs);
        else if (Keys.Pressed(KeyCode.Alpha3)) Game.GameReader.Habitat(ActionType.DrawBirds);
        else if (Keys.Pressed(KeyCode.Alpha4)) Game.GameReader.Birdfeeder();
        else if (Keys.Pressed(KeyCode.Alpha5)) Game.GameReader.Tray();
        else if (Keys.Pressed(KeyCode.Alpha6)) Game.GameReader.Supply();
        else if (Keys.Pressed(KeyCode.Alpha7)) Game.GameReader.Hand();
        else if (Keys.Pressed(KeyCode.Alpha8)) Game.GameReader.Round();
        else if (Keys.Pressed(KeyCode.Alpha9)) Game.GameReader.Opponents();
        else if (Keys.Pressed(KeyCode.Alpha0)) Game.GameReader.Goals();
    }

    private static bool TextFieldHasFocus()
    {
        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        TMPro.TMP_InputField field = selected == null ? null : selected.GetComponent<TMPro.TMP_InputField>();
        return field != null && field.isFocused;
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

        // In the game, decisions are drawn by the shared HUD, so the visible texts of the whole canvas matter.
        Canvas canvas = top.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.rootCanvas != null)
            count += Diagnostics.UiDump.DumpVisibleTexts(canvas.rootCanvas.gameObject);

        ScreenReader.Say(Loc.Get("debug.dumped", count));
    }
}
