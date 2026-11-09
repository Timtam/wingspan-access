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

/// <summary>
/// Some decisions (e.g. a bird power's dice roll) do not handle the gamepad's confirm button and
/// only offer the shared Next button to the mouse. When the confirm button reaches the base
/// implementation for such a decision, this presses that Next button instead.
/// </summary>
[HarmonyPatch(typeof(GamepadScreen), nameof(GamepadScreen.GamepadAccept))]
internal static class GamepadAcceptPatch
{
    private static readonly System.Collections.Generic.Dictionary<string, bool> HandlesAccept = new();

    private static void Postfix(GamepadScreen __instance)
    {
        if (__instance == null || __instance.TryCast<Decision>() == null || OverridesAccept(__instance))
            return;
        GamepadScreen top = UI.Screens.Top();
        if (top == null || top.GetInstanceID() != __instance.GetInstanceID())
            return;

        if (!Game.NextButton.IsAvailable())
            return;

        Plugin.Logger.LogInfo($"[Next] {__instance.GetIl2CppType().Name} has no confirm of its own, pressing Next");
        Game.NextButton.Find().InvokeClickWithStateTransition();
    }

    /// <summary>Whether the screen's class (below GamepadScreen) declares its own GamepadAccept.</summary>
    private static bool OverridesAccept(GamepadScreen screen)
    {
        Il2CppSystem.Type type = screen.GetIl2CppType();
        if (HandlesAccept.TryGetValue(type.FullName, out bool cached))
            return cached;

        const Il2CppSystem.Reflection.BindingFlags Declared = Il2CppSystem.Reflection.BindingFlags.Instance |
            Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic |
            Il2CppSystem.Reflection.BindingFlags.DeclaredOnly;
        bool overrides = false;
        for (Il2CppSystem.Type current = type; current != null && current.Name != nameof(GamepadScreen); current = current.BaseType)
        {
            if (current.GetMethod(nameof(GamepadScreen.GamepadAccept), Declared) != null)
            {
                overrides = true;
                break;
            }
        }

        HandlesAccept[type.FullName] = overrides;
        return overrides;
    }
}
