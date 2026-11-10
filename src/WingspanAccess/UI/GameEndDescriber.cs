using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// The final scores are shown as stacked bars per player. This reads them as a ranking
/// ("1. Platz: OvsXMGdgvD, 76 Punkte") and per category ("Vögel: OvsXMGdgvD 31, du 37"): the
/// ranking when the screen opens, all of it line by line with Up and Down. The players come from
/// the game's scores by player id; their portrait colors can be the same online.
/// </summary>
internal static class GameEndDescriber
{
    public const string ScreenType = "GameFinished_Screen";

    private static readonly Regex Number = new(@"-?\d+");

    private sealed class Result
    {
        public string Name;
        public int Total;
        /// <summary>Food left, which decides a tie (-1: unknown).</summary>
        public int Food = -1;
        public readonly Dictionary<string, int> Points = new();
    }

    /// <summary>
    /// The categories in the order of the game's legend: the name key, the points and the bar on the
    /// screen (a category without its bar only counts when someone scored in it).
    /// </summary>
    private static readonly (string Category, Func<Rules.PlayerScore, int> Points, Func<GameFinishedPlayerSliders, UnityEngine.UI.Slider> Bar)[] Categories =
    {
        ("Birds", s => s.P_BirdPoints, b => b.birdsSlider),
        ("Bonus", s => s.P_BonusCardPoints, b => b.bonusSlider),
        ("Goals", s => s.P_GoalsPoints, b => b.goalSlider),
        ("Eggs", s => s.P_EggsPoints, b => b.eggsSlider),
        ("Food", s => s.P_CachedFoodPoints, b => b.cachedSlider),
        ("Tucked", s => s.P_TuckedCardsPoints, b => b.tuckedSlider),
        ("Nectar", s => s.P_SpentNectarPoints, b => b.nectarSlider),
        ("Duet", s => s.P_DuetLargestGroupPoints, b => b.duetSlider),
        ("Hoard", s => s.P_AutomaHoardPoints, _ => null),
    };

    /// <summary>The bars as the screen names them, for reading the scores off the screen if the game's are missing.</summary>
    private static readonly (string Bar, string Category)[] ScreenBars =
    {
        ("Birds", "Birds"), ("Bonus", "Bonus"), ("Goals", "Goals"), ("Eggs", "Eggs"),
        ("Cached", "Food"), ("Tucked", "Tucked"), ("Nectar", "Nectar"),
    };

    /// <summary>The line Up and Down are on.</summary>
    private static int _line = -1;

    /// <summary>The whole result (F2); null for other screens.</summary>
    public static string Describe(GamepadScreen screen)
    {
        List<string> lines = Lines(screen, out _);
        return lines == null ? null : UiText.JoinSentences(lines);
    }

    /// <summary>When the screen opens: the ranking; Up and Down read on from there (see the key hint).</summary>
    public static string Opening(GamepadScreen screen)
    {
        List<string> lines = Lines(screen, out int ranking);
        if (lines == null)
            return null;
        _line = ranking - 1;
        return UiText.JoinSentences(lines.GetRange(0, ranking));
    }

    /// <summary>Whether the result is shown, whose lines Up and Down read instead of moving the game's selection.</summary>
    public static bool TakesArrows(string topType) => topType == ScreenType;

    /// <summary>Up and Down read the result line by line, Home and End jump.</summary>
    public static void Tick(GamepadScreen top)
    {
        if (top == null || top.GetIl2CppType().Name != ScreenType || Zones.ZoneNavigator.IsActive)
            return;

        int step = UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) ? 1
            : UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) ? -1
            : 0;
        bool home = UnityEngine.Input.GetKeyDown(KeyCode.Home);
        bool end = UnityEngine.Input.GetKeyDown(KeyCode.End);
        if (step == 0 && !home && !end)
            return;

        List<string> lines = Lines(top, out _);
        if (lines == null || lines.Count == 0)
            return;
        _line = home ? 0 : end ? lines.Count - 1 : Mathf.Clamp(_line + step, 0, lines.Count - 1);
        ScreenReader.SayTransient(lines[_line]);
    }

    /// <summary>
    /// One line per player by place, then one per category with everybody's points, and the food
    /// left when it decides a tie. <paramref name="ranking"/> is the number of place lines.
    /// </summary>
    private static List<string> Lines(GamepadScreen screen, out int ranking)
    {
        ranking = 0;
        GameFinished_Screen end = screen == null || screen.GetIl2CppType().Name != ScreenType ? null : screen.TryCast<GameFinished_Screen>();
        if (end == null)
            return null;

        List<Result> results = FromScores(end, out List<string> categories) ?? FromBars(end, out categories);
        if (results == null || results.Count == 0)
            return null;

        // Most points first; equal points, then the food left decides (as in the game).
        results.Sort((a, b) => a.Total != b.Total ? b.Total.CompareTo(a.Total) : b.Food.CompareTo(a.Food));

        var lines = new List<string>();
        int place = 0;
        bool tie = false;
        for (int i = 0; i < results.Count; i++)
        {
            Result result = results[i];
            bool same = i > 0 && results[i - 1].Total == result.Total;
            tie |= same;
            if (!same || results[i - 1].Food != result.Food)
                place = i + 1;
            lines.Add(Loc.Get("gameend.rank", place, result.Name, Loc.Count("goalmat.points", result.Total)));
        }

        ranking = lines.Count;
        foreach (string category in categories)
        {
            var points = new List<string>();
            foreach (Result result in results)
            {
                if (result.Points.TryGetValue(category, out int value))
                    points.Add($"{result.Name} {value}");
            }

            if (points.Count > 0)
                lines.Add(Loc.Get("gameend.category", CategoryName(category), string.Join(", ", points)));
        }

        if (tie && results.TrueForAll(r => r.Food >= 0))
            lines.Add(Loc.Get("gameend.food_left", string.Join(", ", results.ConvertAll(r => $"{r.Name} {r.Food}"))));
        return lines;
    }

    /// <summary>The game's final scores, each with its player.</summary>
    private static List<Result> FromScores(GameFinished_Screen end, out List<string> categories)
    {
        categories = new List<string>();
        Rules.GameState state = end._savedGameState ?? Game.GameStateCache.State;
        var scores = new List<Rules.PlayerScore>();
        var current = end._currentScores;
        for (int i = 0; current != null && i < current.Length; i++)
        {
            if (current[i] != null)
                scores.Add(current[i]);
        }

        for (int i = 0; scores.Count == 0 && state?.Scores != null && i < state.Scores.Count; i++)
        {
            if (state.Scores[i] != null)
                scores.Add(state.Scores[i]);
        }

        if (scores.Count == 0)
            return null;

        GameFinishedPlayerSliders bars = null;
        var sliders = end._playersSliders;
        for (int i = 0; bars == null && sliders != null && i < sliders.Length; i++)
        {
            if (sliders[i] != null && sliders[i].gameObject.activeInHierarchy)
                bars = sliders[i];
        }

        foreach (var (category, points, bar) in Categories)
        {
            UnityEngine.UI.Slider slider = bars == null ? null : bar(bars);
            bool shown = slider != null && slider.gameObject.activeInHierarchy;
            if (shown || scores.Exists(score => points(score) != 0))
                categories.Add(category);
        }

        var results = new List<Result>();
        foreach (Rules.PlayerScore score in scores)
        {
            var result = new Result { Name = NameOf(state, score.P_ID), Total = score.P_Score, Food = score.P_FoodTokens };
            foreach (var (category, points, _) in Categories)
                result.Points[category] = points(score);
            results.Add(result);
        }

        return results;
    }

    private static string NameOf(Rules.GameState state, string playerId)
    {
        Rules.Player player = state == null || string.IsNullOrEmpty(playerId) ? null : state.GetPlayer(playerId);
        if (player == null)
            return Loc.Get("online.someone");
        return player.IsLocalPlayer() ? Loc.Get("goalmat.you") : Game.GameReader.PlayerName(player);
    }

    /// <summary>The scores as the bars show them, each named by its portrait.</summary>
    private static List<Result> FromBars(GameFinished_Screen end, out List<string> categories)
    {
        categories = new List<string>();
        foreach (var (_, category) in ScreenBars)
            categories.Add(category);

        Transform sliders = end.transform.Find("SliderPanel/Sliders");
        if (sliders == null)
            return null;

        var results = new List<Result>();
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

            var result = new Result { Name = portrait == null ? Loc.Get("online.someone") : GoalMatDescriber.PlayerName(portrait) };
            int sum = 0;
            foreach (var (bar, category) in ScreenBars)
            {
                int value = Value(player.Find($"SliderMain/GameFinishedSliderScore_{bar}/Background"));
                if (value < 0)
                    continue;
                sum += value;
                result.Points[category] = value;
            }

            // The total counts up while the bars animate; the categories are final right away.
            Transform pointsText = portrait == null ? null : portrait.transform.Find("PointsTxt");
            result.Total = Math.Max(Value(pointsText), sum);
            results.Add(result);
        }

        return results;
    }

    private static string CategoryName(string category)
    {
        // Duet tokens are only named by the game ("Duellmarker").
        if (category == "Duet")
        {
            string term = I2.Loc.LocalizationManager.GetTranslation("UI/Endgame/DuetTokens", true, 0, true, false, null, null, true);
            if (!string.IsNullOrEmpty(term))
                return UiText.Clean(term);
        }

        return Loc.Get("score." + category);
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
