using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;

namespace WingspanAccess.UI;

/// <summary>
/// The food toggles of a task's food panel (trading food with a power like the Green Heron's,
/// caching food on birds) only show an icon. Each is named by its food after the panel's header,
/// with the amount it shows: "Geben: Nagetier, 2 vorhanden, nicht ausgewählt".
/// </summary>
internal static class FoodToggleDescriber
{
    public static string Describe(GameObject target)
    {
        UnityEngine.UI.Toggle toggle = target.GetComponent<UnityEngine.UI.Toggle>();
        FoodTogglePanel panel = toggle == null ? null : target.GetComponentInParent<FoodTogglePanel>();
        if (panel == null)
            return null;

        string food = null;
        TMPro.TextMeshProUGUI amount = null;
        foreach (var (control, name, text) in Foods(panel))
        {
            if (control != null && control.gameObject.GetInstanceID() == target.GetInstanceID())
            {
                food = name;
                amount = text;
                break;
            }
        }

        if (food == null)
            return null;

        // The panel's header ("Geben", "Erhalten") leads, so the focus announcement does not repeat it.
        string header = UiText.GroupLabel(target, Screens.Top()?.transform);
        var parts = new List<string> { header == null ? food : $"{header}: {food}" };
        string count = amount != null && UiText.IsShown(amount) ? UiText.Clean(amount.text) : string.Empty;
        if (count.Length > 0)
            parts.Add(Loc.Get("food_toggle.amount", count));
        parts.Add(UiText.ToggleState(toggle));
        return string.Join(", ", parts);
    }

    private static IEnumerable<(UnityEngine.UI.Toggle Toggle, string Food, TMPro.TextMeshProUGUI Amount)> Foods(FoodTogglePanel panel)
    {
        yield return (panel.Invertebrate, Game.GameReader.FoodTypeName(FoodType.Invertebrate), panel.InvertebrateText);
        yield return (panel.Seed, Game.GameReader.FoodTypeName(FoodType.Seed), panel.SeedText);
        yield return (panel.Fruit, Game.GameReader.FoodTypeName(FoodType.Fruit), panel.FruitText);
        yield return (panel.Fish, Game.GameReader.FoodTypeName(FoodType.Fish), panel.FishText);
        yield return (panel.Rodent, Game.GameReader.FoodTypeName(FoodType.Rodent), panel.RodentText);
        yield return (panel.Nectar, Game.GameReader.FoodTypeName(FoodType.Nectar), panel.NectarText);
        // Seed cached on the two birds whose seed may be spent like seed from the supply.
        string seed = Game.GameReader.FoodTypeName(FoodType.Seed);
        yield return (panel.CachedSeed184, Loc.Get("food_toggle.cached", seed, Game.GameReader.BirdName(184)), panel.CachedSeed184Text);
        yield return (panel.CachedSeed185, Loc.Get("food_toggle.cached", seed, Game.GameReader.BirdName(185)), panel.CachedSeed185Text);
    }
}
