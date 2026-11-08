using System.Collections.Generic;
using UnityEngine;

namespace WingspanAccess.UI;

/// <summary>
/// Reads the game's tooltips (BaseTooltip components). They hold localized explanations that
/// sighted players see on mouse-over or with the gamepad's tooltip button.
/// </summary>
internal static class Tooltips
{
    /// <summary>Tooltip texts on the element, its children and its two closest parents.</summary>
    public static List<string> TextsFor(GameObject element)
    {
        var result = new List<string>();
        foreach (BaseTooltip tooltip in element.GetComponentsInChildren<BaseTooltip>(true))
            Add(result, tooltip);

        Transform parent = element.transform.parent;
        for (int depth = 0; parent != null && depth < 2; depth++, parent = parent.parent)
        {
            foreach (BaseTooltip tooltip in parent.GetComponents<BaseTooltip>())
                Add(result, tooltip);
        }

        return result;
    }

    private static void Add(List<string> result, BaseTooltip tooltip)
    {
        string text = UiText.Clean(tooltip.GetText().ToString());
        if (text.Length > 0 && !result.Contains(text))
            result.Add(text);
    }
}
