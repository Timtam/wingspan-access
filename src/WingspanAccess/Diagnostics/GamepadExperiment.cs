using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.UI;

namespace WingspanAccess.Diagnostics;

/// <summary>
/// Development experiment (Ctrl+F11): logs the game's controller state, switches the hand UI to
/// gamepad input and asks the top screen to select its default element.
/// </summary>
internal static class GamepadExperiment
{
    public static void Run()
    {
        GamepadController controller = GamepadController.I;
        Plugin.Logger.LogInfo(
            $"[Experiment] HasController={controller?.HasController()} mouseOrKeyboard={controller?.LastControllerIsMouseOrKeyboard()}");

        foreach (PlayerHandUI hand in Object.FindObjectsOfType<PlayerHandUI>())
        {
            Plugin.Logger.LogInfo($"[Experiment] PlayerHandUI {UiText.PathOf(hand.gameObject)} InputMode={hand.InputMode} -> Gamepad");
            hand.InputMode = InputMode.Gamepad;
        }

        GamepadScreen top = Screens.Top();
        if (top != null)
        {
            top.SetGamepadInteractive(true);
            top.TrySelectDefault();
        }

        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        Plugin.Logger.LogInfo($"[Experiment] selection after default: {(selected == null ? "<none>" : UiText.PathOf(selected))}");
        Speech.ScreenReader.Say("Experiment done");
    }
}
