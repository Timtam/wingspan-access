using HarmonyLib;
using WingspanAccess.Input;

namespace WingspanAccess.Patches;

/// <summary>
/// The game only treats real joysticks as "a controller is connected". The mod's virtual gamepad
/// is a Rewired custom controller, so this patch reports a controller while it is active.
/// </summary>
[HarmonyPatch(typeof(GamepadController), nameof(GamepadController.HasController))]
internal static class HasControllerPatch
{
    private static void Postfix(ref bool __result)
    {
        if (VirtualGamepad.IsActive)
            __result = true;
    }
}
