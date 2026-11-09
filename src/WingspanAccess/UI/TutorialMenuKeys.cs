using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// The tutorial menu shows a course's chapters as a grid and the courses (base game, expansions) as
/// tabs that the gamepad's shoulder buttons switch. With the keyboard, Up and Down go through the
/// chapters in order, Home and End jump to the first and the last, and Left and Right switch the
/// course (KeyboardBridge.PagingScreens), like the goal board's tabs.
/// </summary>
internal static class TutorialMenuKeys
{
    public const string ScreenType = "TutorialGamepadScreen";

    /// <summary>Step 1 or -1 moves to the next or previous chapter, int.MinValue and int.MaxValue to the first and last; false if the key is not for this.</summary>
    public static bool TryMove(GamepadScreen top, int step)
    {
        if (top == null || top.GetIl2CppType().Name != ScreenType)
            return false;
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy || !IsChapter(selected.transform))
            return false;

        List<UnityEngine.UI.Selectable> chapters = Chapters(selected.transform.parent);
        int index = chapters.FindIndex(chapter => chapter.gameObject.GetInstanceID() == selected.GetInstanceID());
        if (index < 0)
            return false;

        int target = step == int.MinValue ? 0 : step == int.MaxValue ? chapters.Count - 1 : index + step;
        if (target < 0 || target >= chapters.Count || target == index)
        {
            // At the first or last chapter the key repeats it, as in the zones.
            ScreenReader.SayTransient(UiText.Describe(selected));
            return true;
        }

        chapters[target].Select();
        return true;
    }

    private static bool IsChapter(Transform transform) =>
        transform.GetComponent<View.UI.MainMenu.TutorialMenu.TutorialMenuButtonDynamic>() != null || transform.name.StartsWith("TutorialMenuSlot");

    /// <summary>The chapters next to each other, in the order the grid shows them.</summary>
    private static List<UnityEngine.UI.Selectable> Chapters(Transform content)
    {
        var chapters = new List<UnityEngine.UI.Selectable>();
        for (int i = 0; content != null && i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            UnityEngine.UI.Selectable selectable = child.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable != null && child.gameObject.activeInHierarchy && IsChapter(child))
                chapters.Add(selectable);
        }

        return chapters;
    }
}
