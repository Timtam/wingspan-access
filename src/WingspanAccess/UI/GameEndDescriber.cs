using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using WingspanAccess.Localization;

namespace WingspanAccess.UI;

/// <summary>
/// The final scores are shown as stacked bars per player. This reads them as a ranking:
/// "1. Platz: Rot, 61 Punkte (Vögel 17, ...)".
/// </summary>
internal static class GameEndDescriber
{
    public const string ScreenType = "GameFinished_Screen";

    private static readonly Regex Number = new(@"-?\d+");

    /// <summary>Score bars by category, in the order of the game's legend.</summary>
    private static readonly (string Bar, string Category)[] Categories =
    {
        ("Birds", "Birds"), ("Bonus", "Bonus"), ("Goals", "Goals"), ("Eggs", "Eggs"),
        ("Cached", "Food"), ("Tucked", "Tucked"), ("Nectar", "Nectar"),
    };

    /// <summary>The ranking with each player's points by category; null for other screens.</summary>
    public static string Describe(GamepadScreen screen)
    {
        if (screen.GetIl2CppType().Name != ScreenType)
            return null;
        Transform sliders = screen.transform.Find("SliderPanel/Sliders");
        if (sliders == null)
            return null;

        var players = new List<(string Name, int Total, string Details)>();
        for (int i = 0; i < sliders.childCount; i++)
        {
            Transform player = sliders.GetChild(i);
            if (!player.gameObject.activeInHierarchy || !player.name.StartsWith("GameFinishedPlayerSliders"))
                continue;

            PlayerPortraitUI portrait = null;
            foreach (PlayerPortraitUI found in player.GetComponentsInChildren<PlayerPortraitUI>(false))
            {
                portrait = found;
                break;
            }

            Transform pointsText = portrait == null ? null : portrait.transform.Find("PointsTxt");
            int total = Value(pointsText);
            string name = portrait == null ? "?" : GoalMatDescriber.PlayerName(portrait.CurrentPlayerColorName.ToString());

            var details = new List<string>();
            int sum = 0;
            foreach (var (bar, category) in Categories)
            {
                Transform score = player.Find($"SliderMain/GameFinishedSliderScore_{bar}/Background");
                int value = Value(score);
                if (value < 0)
                    continue;
                sum += value;
                details.Add($"{Loc.Get("score." + category)} {value}");
            }

            // The total counts up while the bars animate; the categories are final right away.
            players.Add((name, System.Math.Max(total, sum), string.Join(", ", details)));
        }

        if (players.Count == 0)
            return null;

        players.Sort((a, b) => b.Total.CompareTo(a.Total));
        var parts = new List<string>();
        int place = 0, previousTotal = int.MinValue;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Total != previousTotal)
                place = i + 1;
            previousTotal = players[i].Total;
            parts.Add(Loc.Get("gameend.place", place, players[i].Name, Loc.Count("goalmat.points", players[i].Total), players[i].Details));
        }

        return UiText.JoinSentences(parts);
    }

    /// <summary>The number in the first text under a transform, or -1.</summary>
    private static int Value(Transform root)
    {
        if (root == null)
            return -1;
        foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            Match number = Number.Match(text.text ?? string.Empty);
            if (number.Success)
                return int.Parse(number.Value);
        }

        return -1;
    }
}
