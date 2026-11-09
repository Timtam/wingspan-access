using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.Game;

/// <summary>
/// Backspace takes the last picked item out again where the game only offers a small button for it:
/// food picked for a bird's payment, food cached on a bird.
/// </summary>
internal static class UndoKey
{
    public static void Tick(GamepadScreen top)
    {
        if (!UnityEngine.Input.GetKeyDown(KeyCode.Backspace) || top == null)
            return;
        bool modifier = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl) ||
            UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        if (modifier)
            return;

        PlayBird_Decision play = top.TryCast<PlayBird_Decision>();
        if (play != null && RemovePickedFood(play))
            return;

        CacheUpToAmountOfFoodFromSupplyOnBird_Decision cache = top.TryCast<CacheUpToAmountOfFoodFromSupplyOnBird_Decision>();
        if (cache != null && (cache.foodToCache?.Count ?? 0) > 0)
        {
            cache.RevertLastCachedFood();
            return;
        }

        if (play != null || cache != null)
            ScreenReader.SayTransient(Loc.Get("undo.nothing"));
    }

    /// <summary>Clicks the last food token picked in the manual payment panel, which puts it back.</summary>
    private static bool RemovePickedFood(PlayBird_Decision decision)
    {
        var cost = decision.CurrentState?.TryCast<View.Decisions.PlayBird.States.CostPaymentStateController>();
        var panel = cost?.ChangePaymentPanel;
        if (panel == null || !panel.gameObject.activeInHierarchy || panel.foodAboutToPay == null)
            return false;

        FoodButton last = null;
        var buttons = panel.foodAboutToPay.FoodButtons;
        for (int i = 0; buttons != null && i < buttons.Count; i++)
        {
            if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy)
                last = buttons[i];
        }

        if (last == null || last.Button == null)
            return false;
        last.Button.onClick.Invoke();
        return true;
    }
}
