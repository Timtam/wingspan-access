using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WingspanAccess.Localization;

namespace WingspanAccess.UI;

/// <summary>
/// The habitat overview (O) shows every slot of the three habitats as a button without text of its
/// own. This names the slot ("Grasland 1") and what is in it: the bird with its eggs and food, the
/// habitat's action, or nothing.
/// </summary>
internal static class OverviewDescriber
{
    /// <summary>Whether the habitat overview is open: on the screen stack (it stays active while it fades out).</summary>
    public static bool IsOpen() => Screens.Find<HabitatsOverview_Screen>() != null;

    public static string Describe(GameObject selected)
    {
        HabitatsOverviewSlot slot = selected.GetComponentInParent<HabitatsOverviewSlot>();
        if (slot == null)
            return null;

        var parts = new List<string>();
        string place = Place(selected.GetComponentInParent<HabitatsOverview_Screen>(), slot);
        if (place != null)
            parts.Add(place);

        BaseBirdCardCanvas card = slot.BirdCard;
        if (card != null && card.gameObject.activeInHierarchy && (int)card.ID > 0)
        {
            // Name and what lies on the bird (eggs, food, cards); the zones read the whole card.
            parts.AddRange(CardDescriber.BirdLines((int)card.ID, card._GameStateBird_k__BackingField).Take(2));
        }
        else
        {
            parts.Add(Loc.Get(slot.IsActionSlot ? "overview.action" : "overview.empty"));
        }

        return string.Join(", ", parts);
    }

    /// <summary>"Wald 2": the habitat and the slot's number from the left.</summary>
    private static string Place(HabitatsOverview_Screen screen, HabitatsOverviewSlot slot)
    {
        if (screen == null)
            return null;
        var habitats = new (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<HabitatsOverviewSlot> Slots, string Icon)[]
        {
            (screen.forestSlots, "habitat_forest"),
            (screen.grassSlots, "habitat_grassland"),
            (screen.wetSlots, "habitat_wetland"),
        };
        int id = slot.GetInstanceID();
        foreach (var (slots, icon) in habitats)
        {
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].GetInstanceID() == id)
                    return $"{Loc.Get("icon." + icon)} {i + 1}";
            }
        }

        return null;
    }
}
