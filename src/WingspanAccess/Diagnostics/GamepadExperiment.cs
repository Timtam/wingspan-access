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
        Plugin.Logger.LogInfo($"[Experiment] hand before: {UI.Zones.HandZone.State()}");

        foreach (PlayerHandUI hand in Object.FindObjectsOfType<PlayerHandUI>())
        {
            Plugin.Logger.LogInfo($"[Experiment] PlayerHandUI {UiText.PathOf(hand.gameObject)} InputMode={hand.InputMode} -> Gamepad");
            hand.InputMode = InputMode.Gamepad;
        }

        // The friend list: which controller buttons jump into its lists (their glyph images).
        foreach (FriendsList_Screen friends in Object.FindObjectsOfType<FriendsList_Screen>())
        {
            foreach (GameObject glyph in new[] { friends._jumpToPlatformFriendsGlyph, friends._jumpToInGameFriendsGlyph })
            {
                if (glyph == null)
                    continue;
                var sprites = new System.Collections.Generic.List<string>();
                foreach (UnityEngine.UI.Image image in glyph.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    sprites.Add($"{image.name}:{(image.sprite == null ? "-" : image.sprite.name)}");
                foreach (TMPro.TMP_Text text in glyph.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    sprites.Add($"text {text.name}:{text.text}");
                Plugin.Logger.LogInfo($"[Experiment] glyph {UiText.PathOf(glyph)} active={glyph.activeInHierarchy}: {string.Join(", ", sprites)}");
            }

            Plugin.Logger.LogInfo($"[Experiment] platform group interactable={friends._platformFriendsCanvasGroup?.interactable} alpha={friends._platformFriendsCanvasGroup?.alpha}, in-game group interactable={friends._inGameFriendsCanvasGroup?.interactable}");
        }

        GamepadScreen top = Screens.Top();
        if (top != null)
        {
            top.SetGamepadInteractive(true);
            top.TrySelectDefault();
        }

        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        Plugin.Logger.LogInfo($"[Experiment] selection after default: {(selected == null ? "<none>" : UiText.PathOf(selected))}");
        Plugin.Logger.LogInfo($"[Experiment] hand after: {UI.Zones.HandZone.State()}");
        Speech.ScreenReader.SayTransient("Experiment done");
    }
}
