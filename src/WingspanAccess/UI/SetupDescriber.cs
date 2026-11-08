using System.Text.RegularExpressions;
using UnityEngine;
using WingspanAccess.Localization;

namespace WingspanAccess.UI;

/// <summary>Descriptions for the custom game setup (player slots), whose buttons only show icons.</summary>
internal static class SetupDescriber
{
    private static readonly Regex PanelNumber = new(@"\((\d+)\)");

    /// <summary>Returns a description for setup elements, or null for anything else.</summary>
    public static string Describe(GameObject target)
    {
        if (target.name != "PlayerTypeButton" && target.name != "PlayerSkinButton")
            return null;

        CustomGamePlayer_Panel panel = target.GetComponentInParent<CustomGamePlayer_Panel>();
        if (panel == null)
            return null;

        string player = Loc.Get("setup.player", PlayerNumber(panel));
        string type = PlayerTypeName(panel);
        if (target.name == "PlayerTypeButton")
            return $"{player}, {type}, {Loc.Get("setup.player_type")}, {Loc.Get("role.button")}";

        string color = Loc.Get("color." + panel.ColorName);
        return $"{player}, {type}, {Loc.Get("setup.color", color)}, {Loc.Get("setup.appearance")}, {Loc.Get("role.button")}";
    }

    /// <summary>
    /// Value names for the setup's icon toggles. Verified visually: GoalMat on shows the podium
    /// (green side, direct competition), Order on shows the shuffle icon (random order).
    /// </summary>
    public static string ToggleValue(UnityEngine.UI.Toggle toggle)
    {
        Transform parent = toggle.transform.parent;
        if (parent == null || parent.name != "RightCustomizationPanel")
            return null;

        return toggle.name switch
        {
            "GoalMat" => Loc.Get(toggle.isOn ? "setup.goalmat.on" : "setup.goalmat.off"),
            "Order" => Loc.Get(toggle.isOn ? "setup.order.on" : "setup.order.off"),
            _ => null,
        };
    }

    private static int PlayerNumber(CustomGamePlayer_Panel panel)
    {
        Match match = PanelNumber.Match(panel.name);
        return match.Success ? int.Parse(match.Groups[1].Value) : panel.transform.GetSiblingIndex() + 1;
    }

    /// <summary>The game's own label for the slot's player type, taken from its choice buttons.</summary>
    private static string PlayerTypeName(CustomGamePlayer_Panel panel)
    {
        CustomGamePlayerType type = panel.CustomGamePlayerType;
        foreach (ChoosePlayerTypeButton choice in panel.GetComponentsInChildren<ChoosePlayerTypeButton>(true))
        {
            if (choice.playerType != type)
                continue;
            // GetComponentInChildren<T> fails for abstract T under Il2CppInterop; the array variant works.
            foreach (TMPro.TMP_Text text in choice.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                string label = UiText.Clean(text.text);
                if (label.Length > 0)
                    return label;
            }
        }

        return UiText.HumanizeTypeName(type.ToString());
    }
}
