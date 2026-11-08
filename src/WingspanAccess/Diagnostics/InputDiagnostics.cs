using UnityEngine.EventSystems;

namespace WingspanAccess.Diagnostics;

/// <summary>Writes the game's Rewired controller maps (keyboard bindings in detail) to the log once.</summary>
internal static class InputDiagnostics
{
    private static bool _done;

    public static void DumpOnce()
    {
        if (_done)
            return;

        GamepadController controller = GamepadController.I;
        Rewired.Player player = controller == null ? null : controller.Player;
        if (player == null)
            return;

        _done = true;
        var maps = new Il2CppSystem.Collections.Generic.List<Rewired.ControllerMap>();
        player.controllers.maps.GetAllMaps(maps);

        EventSystem eventSystem = EventSystem.current;
        string inputModule = eventSystem == null || eventSystem.currentInputModule == null
            ? "none"
            : eventSystem.currentInputModule.GetIl2CppType().FullName;
        Plugin.Logger.LogInfo($"[Input] input module={inputModule}, hasKeyboard={player.controllers.hasKeyboard}, maps={maps.Count}");

        for (int i = 0; i < maps.Count; i++)
        {
            Rewired.ControllerMap map = maps[i];
            Plugin.Logger.LogInfo(
                $"[Input] map '{map.name}' type={map.controllerType} enabled={map.enabled} " +
                $"category={map.categoryId} layout={map.layoutId} elements={map.elementMapCount}");
            if (map.controllerType != Rewired.ControllerType.Keyboard)
                continue;

            foreach (Rewired.ActionElementMap element in map.GetElementMaps())
            {
                Plugin.Logger.LogInfo(
                    $"[Input]   {element.actionDescriptiveName} (#{element.actionId}) <- {element.elementIdentifierName} " +
                    $"key={element.keyboardKeyCode} enabled={element.enabled}");
            }
        }
    }
}
