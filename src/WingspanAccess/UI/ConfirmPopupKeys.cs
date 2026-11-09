using System.Collections.Generic;
using UnityEngine;

namespace WingspanAccess.UI;

/// <summary>
/// The game's yes/no questions (ConfirmPopup, e.g. "join an online game?") ignore the gamepad's back
/// button, so Escape did nothing there. Escape now answers No, like the No button (ConfirmPopup.Back),
/// unless the popup handles the back button itself.
/// </summary>
internal static class ConfirmPopupKeys
{
    private static readonly Dictionary<string, bool> HandlesBack = new();

    /// <summary>
    /// True from the Escape press that answered a question until Escape is released: the keyboard
    /// bridge must not send the gamepad's back button then, or it would also leave the screen below.
    /// </summary>
    public static bool Swallowing { get; private set; }

    /// <summary>Whether Escape on this screen answers No (a question without its own back handling).</summary>
    public static bool Owns(GamepadScreen top) =>
        top != null && top.TryCast<ConfirmPopup>() != null && !OverridesBack(top) && !Zones.ZoneNavigator.IsActive;

    public static void Tick(GamepadScreen top)
    {
        if (Swallowing && !UnityEngine.Input.GetKey(KeyCode.Escape))
            Swallowing = false;
        if (!UnityEngine.Input.GetKeyDown(KeyCode.Escape) || Input.KeyboardBridge.TextFieldHasFocus() || !Owns(top))
            return;

        Plugin.Logger.LogInfo($"[Popup] Escape answers no in {top.GetIl2CppType().Name}");
        Swallowing = true;
        top.TryCast<ConfirmPopup>().Back();
    }

    /// <summary>Whether the screen's class (below GamepadScreen) declares its own GamepadBack.</summary>
    private static bool OverridesBack(GamepadScreen screen)
    {
        Il2CppSystem.Type type = screen.GetIl2CppType();
        if (HandlesBack.TryGetValue(type.FullName, out bool cached))
            return cached;

        const Il2CppSystem.Reflection.BindingFlags Declared = Il2CppSystem.Reflection.BindingFlags.Instance |
            Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic |
            Il2CppSystem.Reflection.BindingFlags.DeclaredOnly;
        bool overrides = false;
        for (Il2CppSystem.Type current = type; current != null && current.Name != nameof(GamepadScreen); current = current.BaseType)
        {
            if (current.GetMethod(nameof(GamepadScreen.GamepadBack), Declared) != null)
            {
                overrides = true;
                break;
            }
        }

        HandlesBack[type.FullName] = overrides;
        return overrides;
    }
}
