using System.Collections.Generic;
using UnityEngine;

namespace WingspanAccess.UI;

/// <summary>Access to the game's screen stack (GamepadController.screenStack).</summary>
internal static class Screens
{
    public static GamepadScreen Top()
    {
        GamepadController controller = GamepadController.I;
        if (controller == null)
            return null;

        AlmostStack<GamepadScreen> stack = controller.screenStack;
        return stack == null || stack.Count == 0 ? null : stack.Peek();
    }

    /// <summary>Type names of all screens on the stack, in storage order.</summary>
    public static string DescribeStack()
    {
        GamepadController controller = GamepadController.I;
        AlmostStack<GamepadScreen> stack = controller == null ? null : controller.screenStack;
        if (stack == null || stack.items == null)
            return string.Empty;

        var names = new List<string>();
        for (int i = 0; i < stack.items.Count; i++)
        {
            GamepadScreen screen = stack.items[i];
            names.Add(screen == null ? "null" : screen.GetIl2CppType().Name);
        }

        return string.Join(", ", names);
    }

    /// <summary>Visible texts of a screen that are not labels of interactive elements.</summary>
    public static List<string> PlainTexts(GameObject root)
    {
        var result = new List<string>();
        var visibility = new Visibility(root.transform);
        foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            if (text.GetComponentInParent<UnityEngine.UI.Selectable>() != null || !visibility.IsVisible(text.transform))
                continue;
            string clean = UiText.Clean(text.text);
            if (clean.Length > 0 && !result.Contains(clean))
                result.Add(clean);
        }

        return result;
    }
}
