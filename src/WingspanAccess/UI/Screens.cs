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

    /// <summary>The topmost screen of the given type on the stack, e.g. the decision under a hint bubble.</summary>
    public static T Find<T>() where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        GamepadController controller = GamepadController.I;
        AlmostStack<GamepadScreen> stack = controller == null ? null : controller.screenStack;
        for (int i = (stack?.items?.Count ?? 0) - 1; i >= 0; i--)
        {
            T screen = stack.items[i]?.TryCast<T>();
            if (screen != null)
                return screen;
        }

        return null;
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

    /// <summary>Visible texts of the given object names, in the order of the names.</summary>
    public static List<string> NamedTexts(GameObject root, string[] objectNames)
    {
        var byName = new Dictionary<string, string>();
        var visibility = new Visibility(root.transform);
        foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            if (System.Array.IndexOf(objectNames, text.name) >= 0 && !byName.ContainsKey(text.name) &&
                visibility.IsVisible(text.transform))
                byName[text.name] = UiText.TextOf(text);
        }

        var result = new List<string>();
        foreach (string name in objectNames)
        {
            if (byName.TryGetValue(name, out string value) && value.Length > 0)
                result.Add(value);
        }

        return result;
    }

    /// <summary>Number of visible, interactable controls in a screen.</summary>
    public static int CountControls(GameObject root)
    {
        int count = 0;
        var visibility = new Visibility(root.transform);
        foreach (UnityEngine.UI.Selectable selectable in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(false))
        {
            if (selectable.interactable && visibility.IsVisible(selectable.transform))
                count++;
        }

        return count;
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
            string clean = UiText.TextOf(text);
            if (clean.Length > 0 && !result.Contains(clean))
                result.Add(clean);
        }

        return result;
    }
}
