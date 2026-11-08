using System;
using Rewired;
using QaattButton = RewiredConsts.CustomController.CustomController_QAATT.Button;
using QaattController = RewiredConsts.CustomController.CustomController_QAATT;

namespace WingspanAccess.Input;

/// <summary>
/// A Rewired custom controller of the game's own QA automation type ("CustomController_QAATT").
/// Feeding it makes the game run in gamepad mode: screens select a default element, navigation
/// follows the console rules, and every gamepad action behaves exactly as on a controller.
/// </summary>
internal static class VirtualGamepad
{
    private static CustomController _controller;
    private static bool _attempted;

    public static bool IsActive => _controller != null;

    /// <summary>Creates the controller once the game's Rewired player exists. Safe to call every frame.</summary>
    public static void EnsureCreated()
    {
        if (_attempted)
            return;

        GamepadController gamepadController = GamepadController.I;
        Player player = gamepadController == null ? null : gamepadController.Player;
        if (player == null)
            return;

        _attempted = true;
        try
        {
            int category = RewiredConsts.Category.Default;
            int layout = RewiredConsts.Layout.CustomController.Default;
            CustomController controller = ReInput.controllers.CreateCustomController(QaattController.sourceId, "WingspanAccess");
            if (controller == null)
            {
                Plugin.Logger.LogError("[Gamepad] could not create the virtual controller");
                return;
            }

            player.controllers.AddController(controller, true);
            ControllerMap map = player.controllers.maps.GetMap(controller, category, layout);
            if (map == null)
            {
                ControllerMap template = ReInput.mapping.GetControllerMapInstance(controller, category, layout);
                if (template != null)
                    player.controllers.maps.AddMap(controller, template, true);
                map = player.controllers.maps.GetMap(controller, category, layout);
            }

            if (map == null)
            {
                Plugin.Logger.LogError("[Gamepad] the game has no controller map for the virtual controller");
                player.controllers.RemoveController(controller);
                ReInput.controllers.DestroyCustomController(controller);
                return;
            }

            map.enabled = true;
            _controller = controller;
            Plugin.Logger.LogInfo($"[Gamepad] virtual controller id={controller.id} with {map.elementMapCount} bindings");
            foreach (ActionElementMap element in map.GetElementMaps())
                Plugin.Logger.LogInfo($"[Gamepad]   {element.actionDescriptiveName} <- {element.elementIdentifierName} (#{element.elementIdentifierId})");

            SetGameKeyboardEnabled(player, false);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"[Gamepad] setup failed: {e}");
            _controller = null;
        }
    }

    public static void SetButton(int elementId, bool pressed) => _controller?.SetButtonValueById(elementId, pressed);

    public static void SetAxis(int elementId, float value) => _controller?.SetAxisValueById(elementId, value);

    /// <summary>
    /// The game's own keyboard bindings would switch it back to mouse/keyboard mode,
    /// so they are disabled while the mod translates the keyboard.
    /// </summary>
    private static void SetGameKeyboardEnabled(Player player, bool enabled)
    {
        var maps = new Il2CppSystem.Collections.Generic.List<ControllerMap>();
        player.controllers.maps.GetAllMaps(maps);
        for (int i = 0; i < maps.Count; i++)
        {
            if (maps[i].controllerType == ControllerType.Keyboard)
                maps[i].enabled = enabled;
        }

        Plugin.Logger.LogInfo($"[Gamepad] game keyboard bindings {(enabled ? "enabled" : "disabled")}");
    }

    internal static class Buttons
    {
        public static int Submit => QaattButton.Submit;
        public static int Back => QaattButton.Back;
        public static int Context => QaattButton.Context;
        public static int Accept => QaattButton.Accept;
        public static int HabitatUp => QaattButton.HabitatUp;
        public static int HabitatDown => QaattButton.HabitatDown;
        public static int ToggleCardPreview => QaattButton.ToggleCardPreview;
        public static int ToggleDiscards => QaattButton.ToggleDiscards;
        public static int NextCard => QaattButton.NextCard;
        public static int PreviousCard => QaattButton.PreviousCard;
        public static int Pause => QaattButton.Pause;
        public static int PreviewPlayerLeft => QaattButton.PreviewPlayerLeft;
        public static int PreviewPlayerRight => QaattButton.PreviewPlayerRight;
        public static int ToggleHabitatsOverview => QaattButton.ToggleHabitatsOverview;
        public static int ToggleGoals => QaattButton.ToggleGoals;
    }

    internal static class Axes
    {
        public static int NavigationX => QaattController.Axis.NavigationX;
        public static int NavigationY => QaattController.Axis.NavigationY;
    }
}
