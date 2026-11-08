using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WingspanAccess.Input;

namespace WingspanAccess.UI;

/// <summary>
/// Keyboard control of the game's own gamepad UI: arrow keys move the EventSystem selection
/// along the Selectable navigation graph, Enter submits the selection, Backspace triggers "Back".
/// Proof-of-concept key layout; it does nothing while a text input field is selected.
/// </summary>
internal static class Navigator
{
    public static void Tick()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null && selected.GetComponent<TMPro.TMP_InputField>() != null)
            return;

        if (Keys.Pressed(KeyCode.UpArrow))
            Move(selected, s => s.FindSelectableOnUp());
        else if (Keys.Pressed(KeyCode.DownArrow))
            Move(selected, s => s.FindSelectableOnDown());
        else if (Keys.Pressed(KeyCode.LeftArrow))
            Move(selected, s => s.FindSelectableOnLeft());
        else if (Keys.Pressed(KeyCode.RightArrow))
            Move(selected, s => s.FindSelectableOnRight());
        else if (Keys.Pressed(KeyCode.Return) || Keys.Pressed(KeyCode.KeypadEnter))
            Activate(eventSystem, selected);
        else if (Keys.Pressed(KeyCode.Backspace))
            Back();
    }

    private static void Move(GameObject selected, System.Func<Selectable, Selectable> neighbour)
    {
        if (selected == null)
        {
            GamepadScreen top = Screens.Top();
            Plugin.Logger.LogInfo($"[Nav] nothing selected, asking {top?.GetIl2CppType().Name ?? "no screen"} to select something");
            top?.TrySelectSomethingIfNothingSelected();
            return;
        }

        Selectable current = selected.GetComponent<Selectable>();
        Selectable next = current == null ? null : neighbour(current);
        if (next == null)
        {
            Plugin.Logger.LogInfo($"[Nav] no neighbour from {selected.name}");
            return;
        }

        next.Select();
    }

    private static void Activate(EventSystem eventSystem, GameObject selected)
    {
        if (selected != null)
        {
            var data = new BaseEventData(eventSystem);
            Button button = selected.GetComponent<Button>();
            if (button != null)
            {
                Plugin.Logger.LogInfo($"[Nav] submit button {selected.name}");
                button.OnSubmit(data);
                return;
            }

            Toggle toggle = selected.GetComponent<Toggle>();
            if (toggle != null)
            {
                Plugin.Logger.LogInfo($"[Nav] submit toggle {selected.name}");
                toggle.OnSubmit(data);
                return;
            }
        }

        Plugin.Logger.LogInfo($"[Nav] GamepadController.Submit for {selected?.name ?? "nothing"}");
        GamepadController.I?.Submit();
    }

    private static void Back()
    {
        Plugin.Logger.LogInfo("[Nav] GamepadController.Back");
        GamepadController.I?.Back();
    }
}
