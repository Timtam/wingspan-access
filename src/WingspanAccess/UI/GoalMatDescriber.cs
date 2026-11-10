using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// The goal board ("Zieltafel") and the nectar board show the standings as sliders with player
/// portraits. This reads them: per round the goal, each player's count and the points by place,
/// per habitat the nectar counts, and the local player's points by category.
/// </summary>
internal static class GoalMatDescriber
{
    public const string ScreenType = "GoalAndNectarMat_Screen";

    private static readonly Regex Number = new(@"-?\d+");
    private static readonly string[] NectarHabitats = { "habitat_forest", "habitat_grassland", "habitat_wetland" };
    private static readonly string[] ScoreCategories = { "Birds", "Bonus", "Goals", "Eggs", "Food", "Tucked", "Nectar", "Total" };

    private static int _screenId;
    private static string _panel;
    private static int _nextCheck;

    /// <summary>The line Up and Down are on (-1: none read yet on this board).</summary>
    private static int _line = -1;

    /// <summary>
    /// The open board line by line: one line per round (goal, standings, points by place) or per
    /// habitat of the nectar board, then the local player's points; null for other screens.
    /// </summary>
    public static List<string> Lines(GamepadScreen screen, bool withTotals = true)
    {
        if (screen.GetIl2CppType().Name != ScreenType)
            return null;
        Transform flow = screen.transform.Find("StandardFlow");
        if (flow == null)
            return null;

        var lines = new List<string>();
        string panel = OpenPanel(flow);
        if (panel == "Goals_Panel")
            lines.AddRange(GoalRows(flow.Find(panel)));
        else if (panel == "Nectar_Panel")
            lines.AddRange(NectarRows(flow.Find(panel)));

        var board = screen.TryCast<View.UI.Game.Windows.Goals.GoalAndNectarMat_Screen>();
        string totals = withTotals ? Totals(flow.Find("PlayerPointsLabel/Points"), board?._playerController?.Player) : null;
        if (totals != null)
            lines.Add(totals);
        return lines;
    }

    /// <summary>The whole open board at once (F2); null for other screens.</summary>
    public static string Describe(GamepadScreen screen, bool withTotals = true)
    {
        List<string> lines = Lines(screen, withTotals);
        return lines == null ? null : UiText.JoinSentences(lines);
    }

    /// <summary>When the board opens: its first line; Up and Down read the rest (see the key hint).</summary>
    public static string Opening(GamepadScreen screen)
    {
        List<string> lines = Lines(screen);
        if (lines == null)
            return null;
        _line = 0;
        return lines.Count == 0 ? string.Empty : lines[0];
    }

    /// <summary>Whether the board is open, whose lines Up and Down read instead of moving the game's selection.</summary>
    public static bool TakesArrows(string topType) => topType == ScreenType;

    /// <summary>Up and Down read the board line by line, Home and End jump; the other tab is read when it opens.</summary>
    public static void Tick(GamepadScreen top)
    {
        if (top == null || top.GetIl2CppType().Name != ScreenType)
        {
            _screenId = 0;
            return;
        }

        ReadLines(top);

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 10;

        Transform flow = top.transform.Find("StandardFlow");
        string panel = flow == null ? null : OpenPanel(flow);
        if (top.GetInstanceID() != _screenId)
        {
            _screenId = top.GetInstanceID();
            _panel = panel;
            return;
        }

        if (panel == _panel)
            return;
        _panel = panel;
        if (panel == null)
            return;
        List<string> lines = Lines(top);
        _line = 0;
        if (lines.Count > 0)
            ScreenReader.SayTransient(lines[0], interrupt: false);
    }

    private static void ReadLines(GamepadScreen top)
    {
        // A zone opened over the board (0, the round goals) has the arrow keys.
        if (Zones.ZoneNavigator.IsActive)
            return;
        int step = UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) ? 1
            : UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) ? -1
            : 0;
        bool home = UnityEngine.Input.GetKeyDown(KeyCode.Home);
        bool end = UnityEngine.Input.GetKeyDown(KeyCode.End);
        if (step == 0 && !home && !end)
            return;

        List<string> lines = Lines(top);
        if (lines == null || lines.Count == 0)
            return;
        _line = home ? 0 : end ? lines.Count - 1 : Mathf.Clamp(_line + step, 0, lines.Count - 1);
        ScreenReader.SayTransient(lines[_line]);
    }

    private static string OpenPanel(Transform flow)
    {
        foreach (string name in new[] { "Goals_Panel", "Nectar_Panel" })
        {
            Transform panel = flow.Find(name);
            if (panel != null && panel.gameObject.activeInHierarchy && new Visibility(flow.root).IsVisible(panel))
                return name;
        }

        return null;
    }

    private static IEnumerable<string> GoalRows(Transform panel)
    {
        for (int round = 1; round <= 4; round++)
        {
            Transform track = FindActive(panel, "WithSlider" + round);
            if (track == null)
                continue;
            string goal = Game.GameReader.GoalText(round) ?? string.Empty;
            yield return Loc.Get("goalmat.round", round, goal, ModelStandings(round) ?? Standings(track), Places(track));
        }
    }

    private static IEnumerable<string> NectarRows(Transform panel)
    {
        Transform tracks = FindActive(panel, "TracksNectar");
        if (tracks == null)
            yield break;

        int index = 0;
        for (int i = 0; i < tracks.childCount && index < NectarHabitats.Length; i++)
        {
            Transform track = tracks.GetChild(i);
            if (!track.gameObject.activeInHierarchy || !track.name.StartsWith("GoalMatNectarTrackSliders"))
                continue;
            yield return Loc.Get("goalmat.nectar", Loc.Get("icon." + NectarHabitats[index]), ModelNectarStandings(index) ?? Standings(track), Places(track));
            index++;
        }
    }

    /// <summary>
    /// "du 2, Rot 1": the nectar each player has spent in a habitat (0 forest, 1 grassland, 2
    /// wetland), from the rules model. The board's portraits name players by color, and online
    /// two players can have the same one.
    /// </summary>
    private static string ModelNectarStandings(int habitat)
    {
        try
        {
            var players = Game.GameStateCache.State?.Players;
            if (players == null)
                return null;

            var standings = new List<string>();
            for (int i = 0; i < players.Count; i++)
            {
                Rules.Player player = players[i];
                Rules.SpentNectarCounter nectar = player?.SpentNectarCounter;
                if (nectar == null)
                    continue;
                int count = habitat switch
                {
                    0 => nectar.Forest,
                    1 => nectar.Grass,
                    _ => nectar.Wet,
                };
                string entry = $"{Name(player)} {count}";
                if (player.IsLocalPlayer())
                    standings.Insert(0, entry);
                else
                    standings.Add(entry);
            }

            return standings.Count == 0 ? null : string.Join(", ", standings);
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Nectar standings for habitat {habitat} failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// "du 3, Rot 1 (4 Punkte)": each player's count for a round goal from the rules model, with
    /// the points once the round is scored. The sliders on the board animate, so they can lag behind.
    /// </summary>
    public static string ModelStandings(int round)
    {
        try
        {
            Rules.GameState state = Game.GameStateCache.State;
            Rules.Goal goal = state?.GoalBoardState?.GetGoalForRound(round - 1);
            var players = state?.Players;
            if (goal == null || goal.IsEmpty || players == null)
                return null;

            var standings = new List<string>();
            for (int i = 0; i < players.Count; i++)
            {
                Rules.Player player = players[i];
                if (player == null)
                    continue;
                string entry = $"{Name(player)} {goal.GetGoalCount(player.ID)}";
                // Rounds still running only have provisional points.
                Rules.GoalPlayer result = Result(goal, player.ID);
                if (result != null && result.P_Score > 0)
                    entry = $"{entry} ({Loc.Count(round > state.P_RoundsCounter ? "goalmat.points_so_far" : "goalmat.points", result.P_Score)})";
                if (player.IsLocalPlayer())
                    standings.Insert(0, entry);
                else
                    standings.Add(entry);
            }

            return standings.Count == 0 ? null : string.Join(", ", standings);
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Goal standings for round {round} failed: {e.Message}");
            return null;
        }
    }

    private static Rules.GoalPlayer Result(Rules.Goal goal, string playerId)
    {
        var results = goal.Players;
        for (int i = 0; results != null && i < results.Count; i++)
        {
            if (results[i] != null && results[i].P_ID == playerId)
                return results[i];
        }

        return null;
    }

    private static string Name(Rules.Player player) =>
        player.IsLocalPlayer() ? Loc.Get("goalmat.you") : Game.GameReader.PlayerName(player);

    /// <summary>"du 1, Rot 0": each portrait on the track with the count next to it.</summary>
    private static string Standings(Transform track)
    {
        var standings = new List<string>();
        foreach (PlayerPortraitUI portrait in track.GetComponentsInChildren<PlayerPortraitUI>(false))
        {
            Transform pointsText = portrait.transform.Find("PointsTxt");
            TMPro.TMP_Text text = pointsText == null ? null : pointsText.GetComponent<TMPro.TMP_Text>();
            string count = text == null ? "?" : UiText.Clean(text.text);
            string name = PlayerName(portrait);
            string entry = $"{name} {count}";
            if (name == Loc.Get("goalmat.you"))
                standings.Insert(0, entry); // the local player first
            else
                standings.Add(entry);
        }

        return string.Join(", ", standings);
    }

    /// <summary>Points for first, second (and third) place: "4, 1, 0".</summary>
    private static string Places(Transform track)
    {
        var points = new List<string>();
        foreach (string place in new[] { "1st", "2nd", "3rd" })
        {
            Transform child = track.Find(place);
            TMPro.TMP_Text text = child == null || !child.gameObject.activeInHierarchy ? null : FirstText(child);
            Match number = text == null ? Match.Empty : Number.Match(text.text);
            if (number.Success)
                points.Add(number.Value);
        }

        return string.Join(", ", points);
    }

    /// <summary>The points row; it shows the player the board was opened for (at round end possibly an opponent).</summary>
    private static string Totals(Transform points, Rules.Player shown)
    {
        if (points == null)
            return null;

        var parts = new List<string>();
        foreach (string category in ScoreCategories)
        {
            Transform child = points.Find(category);
            TMPro.TMP_Text text = child == null || !child.gameObject.activeInHierarchy ? null : FirstText(child);
            Match number = text == null ? Match.Empty : Number.Match(text.text);
            if (number.Success)
                parts.Add($"{Loc.Get("score." + category)} {number.Value}");
        }

        if (parts.Count == 0)
            return null;

        if (shown != null && !shown.IsLocalPlayer())
            return Loc.Get("goalmat.totals_of", Game.GameReader.PlayerName(shown), string.Join(", ", parts));
        return Loc.Get("goalmat.totals", string.Join(", ", parts));
    }

    /// <summary>
    /// The player a portrait shows: by the player id it was set up with, else by its color (online
    /// both players can have the same color).
    /// </summary>
    public static string PlayerName(PlayerPortraitUI portrait)
    {
        string id = portrait._targetPlayerId;
        Rules.Player player = string.IsNullOrEmpty(id) ? null : Game.GameStateCache.State?.GetPlayer(id);
        return player != null ? Name(player) : PlayerName(portrait.CurrentPlayerColorName.ToString());
    }

    /// <summary>A player by portrait color: "du" for the local player, else the player's name or color.</summary>
    public static string PlayerName(string color)
    {
        Rules.Player local = Game.GameStateCache.LocalPlayer;
        if (local != null && local.P_Color.ToString() == color)
            return Loc.Get("goalmat.you");
        var players = Game.GameStateCache.State?.Players;
        for (int i = 0; players != null && i < players.Count; i++)
        {
            if (players[i] != null && players[i].P_Color.ToString() == color)
                return Game.GameReader.PlayerName(players[i]);
        }

        return Loc.Has("color." + color) ? Loc.Get("color." + color) : color;
    }

    private static Transform FindActive(Transform root, string nameEnd)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(false))
        {
            if (child.name.EndsWith(nameEnd))
                return child;
        }

        return null;
    }

    private static TMPro.TMP_Text FirstText(Transform root)
    {
        foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
            return text;
        return null;
    }
}
