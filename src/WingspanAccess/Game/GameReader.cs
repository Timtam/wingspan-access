using System.Collections.Generic;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// Reads the game state for zones and announcements: players, board birds, round goals, names and
/// food. Everything comes from the game's rules model (Rules.*), so it is independent of what is
/// currently shown on screen.
/// </summary>
internal static class GameReader
{
    private static readonly ActionType[] Habitats = { ActionType.GainFood, ActionType.LayEggs, ActionType.DrawBirds };

    /// <summary>One player at a glance: birds per habitat, food, hand size and actions left.</summary>
    /// <summary>
    /// The Automa (the solo opponent) plays no birds into habitats: it keeps birds face up and face
    /// down, counts the eggs it lays and, in some variants, hoard tokens. Null for everyone else.
    /// </summary>
    public static Rules.AutomaPlayerState AutomaState(Rules.Player player)
    {
        try
        {
            return player != null && player.IsAutoma() ? player.AutomaPlayerState : null;
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Automa state failed: {e.Message}");
            return null;
        }
    }

    /// <summary>"Automa: 3 face-up birds, 4 face-down birds, 5 eggs" (and hoard tokens where it has some).</summary>
    private static string AutomaSummary(string name, Rules.AutomaPlayerState automa)
    {
        string text = Loc.Get("automa.summary", name, Loc.Count("automa.face_up", automa.FaceUpBirdsList?.Count ?? 0),
            Loc.Count("automa.face_down", automa.FaceDownBirdsCount), Loc.Count("automa.eggs", automa.CollectedEggs));
        return automa.HoardTokensCount > 0 ? $"{text}, {Loc.Count("automa.hoard", automa.HoardTokensCount)}" : text;
    }

    public static string PlayerSummary(Rules.Player player)
    {
        string name = player.IsLocalPlayer() ? Loc.Get("zone.you") : PlayerName(player);
        Rules.AutomaPlayerState automa = AutomaState(player);
        if (automa != null)
            return AutomaSummary(name, automa);
        int forest = player.GetAllBirdsInHabitat(ActionType.GainFood)?.Count ?? 0;
        int grass = player.GetAllBirdsInHabitat(ActionType.LayEggs)?.Count ?? 0;
        int wetland = player.GetAllBirdsInHabitat(ActionType.DrawBirds)?.Count ?? 0;
        string food = FoodList(player._pf);
        return Loc.Get("game.opponent", name, forest, grass, wetland, food.Length == 0 ? Loc.Get("game.no_food") : food,
            Loc.Count("game.bird_cards", HandCount(player)), Loc.Count("status.actions", player.P_TokensLeft));
    }

    // The rules model keeps most values twice: the game works with obscured fields (read through the
    // "P_" properties) and copies them into plain fields only when it saves. Online the other
    // player's moves are replayed into the obscured fields, so the plain ones lag up to a turn behind
    // (their hand, whose turn it is, the tray). The mod reads the live values.

    /// <summary>Bird cards in a player's hand.</summary>
    public static int HandCount(Rules.Player player) => player.P_BirdCards?.Count ?? 0;

    /// <summary>Bonus cards a player holds.</summary>
    public static int BonusCount(Rules.Player player) => player.P_BonusCards?.Count ?? 0;

    /// <summary>The face-up birds in the tray.</summary>
    public static List<int> TrayBirds()
    {
        var ids = new List<int>();
        var visible = GameStateCache.State?.BirdsDeckState?.P_Visible;
        for (int i = 0; visible != null && i < visible.Count; i++)
        {
            int id = visible[i].GetDecrypted();
            if (id > 0)
                ids.Add(id);
        }

        return ids;
    }

    /// <summary>The player whose turn it is.</summary>
    public static Rules.Player CurrentPlayer(Rules.GameState state) => state.GetPlayer(state.P_CurrentPlayerID);

    /// <summary>A player line by line: each habitat with its birds, food, cards and actions left.</summary>
    public static List<string> PlayerLines(Rules.Player player)
    {
        var lines = new List<string> { player.IsLocalPlayer() ? Loc.Get("zone.you") : PlayerName(player) };
        Rules.AutomaPlayerState automa = AutomaState(player);
        if (automa != null)
        {
            var faceUp = automa.FaceUpBirdsList;
            var names = new List<string>();
            for (int i = 0; faceUp != null && i < faceUp.Count; i++)
                names.Add(BirdName(faceUp[i]));
            lines.Add(names.Count == 0 ? Loc.Get("automa.no_face_up") : Loc.Get("automa.birds_line", automa.FaceUpBirdsPoints, string.Join(", ", names)));
            lines.Add(Loc.Count("automa.face_down", automa.FaceDownBirdsCount));
            lines.Add(Loc.Count("automa.eggs", automa.CollectedEggs));
            if (automa.HoardTokensCount > 0)
                lines.Add(Loc.Count("automa.hoard", automa.HoardTokensCount));
            lines.Add(Loc.Count("status.actions", player.P_TokensLeft));
            return lines;
        }

        foreach (ActionType habitat in Habitats)
        {
            var birds = player.GetAllBirdsInHabitat(habitat);
            var names = new List<string>();
            for (int i = 0; birds != null && i < birds.Count; i++)
                names.Add(DescribeBoardBird(birds[i]));
            lines.Add(Loc.Get("zone.habitat_line", Loc.Get("habitat." + habitat),
                names.Count == 0 ? Loc.Get("game.nothing") : string.Join("; ", names)));
        }

        string food = FoodList(player._pf);
        lines.Add(Loc.Get("zone.supply", food.Length == 0 ? Loc.Get("game.no_food") : food));
        lines.Add($"{Loc.Count("game.bird_cards", HandCount(player))}, {Loc.Count("game.bonus_cards", BonusCount(player))}");
        lines.Add(Loc.Count("status.actions", player.P_TokensLeft));
        return lines;
    }

    /// <summary>Bird on a player board: name plus eggs, cached food and tucked cards.</summary>
    public static string DescribeBoardBird(Rules.Bird bird)
    {
        var parts = new List<string> { BirdName(bird.ID) };
        if (bird.Eggs > 0)
            parts.Add(Loc.Get(bird.Eggs == 1 ? "game.egg" : "game.eggs", bird.Eggs));
        if (bird.CachedFoodCount > 0)
            parts.Add(Loc.Get("game.cached", FoodList(bird.CachedFood)));
        if (bird.TuckedCardsCount > 0)
            parts.Add(Loc.Get("game.tucked", bird.TuckedCardsCount));
        return string.Join(", ", parts);
    }

    /// <summary>
    /// The game's explanation of a round goal (the tooltip sighted players see on the goal tile).
    /// It may refer to the tile's picture ("the nest type shown"), which the goal's name spells out.
    /// </summary>
    public static string GoalExplanation(int round)
    {
        try
        {
            Rules.Goal goal = GameStateCache.State?.GoalBoardState?.GetGoalForRound(round - 1);
            if (goal != null && !goal.IsEmpty)
            {
                string description = GoalTilesDatabase.Get(goal.GoalName)?.GetLocalizedDescription();
                if (!string.IsNullOrEmpty(description) && !description.StartsWith("KEY "))
                    return UiText.Clean(description);
            }
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Goal explanation for round {round} failed: {e.Message}");
        }

        return TooltipGoalText(round);
    }

    private static bool _goalsLogged;

    /// <summary>
    /// A round goal's name from the goal board (e.g. "Birds with bowl nests with eggs"), else the goal
    /// tile's tooltip in the HUD; null if neither exists.
    /// </summary>
    /// <summary>
    /// For round goals about the traits of birds (beak, power colour, points, nests, eggs, tucked
    /// cards): which of your birds count now, "Bei dir zählen: ..."; null for other goals. A beak
    /// pointing both ways counts for either side, a star nest for every kind of nest.
    /// </summary>
    public static string GoalBirds(int round)
    {
        try
        {
            Rules.GameState state = GameStateCache.State;
            Rules.Goal goal = state?.GoalBoardState?.GetGoalForRound(round - 1);
            Rules.Player player = GameStateCache.LocalPlayer;
            // Scored rounds keep their result; the birds of now only matter for this round and later ones.
            if (goal == null || goal.IsEmpty || player == null || round <= state.P_RoundsCounter)
                return null;

            System.Func<Rules.Bird, BirdData, bool> counts = goal.GoalName switch
            {
                GoalTileName.Beak_pointing_left_31 => (_, d) =>
                    d.BeakDirection is BeakDirection.Left or BeakDirection.LeftAndRight or BeakDirection.LeftAndStraight,
                GoalTileName.Beak_pointing_right_32 => (_, d) => d.BeakDirection is BeakDirection.Right or BeakDirection.LeftAndRight,
                GoalTileName.Brown_Powers_Count_20 => (_, d) => d.PowerType == BirdPowerType.Activated,
                GoalTileName.Birds_with_White_and_No_Powers_26 => (_, d) => d.PowerType is BirdPowerType.Played or BirdPowerType.None,
                GoalTileName.Birds_Worh_More_4_Points_18 => (_, d) => d.VictoryPoints.GetDecrypted() > 4,
                GoalTileName.Birds_worth_less_or_equal_3_points_34 => (_, d) => d.VictoryPoints.GetDecrypted() <= 3,
                GoalTileName.Birds_with_Tucked_Cards_21 => (b, _) => b.TuckedCardsCount > 0,
                GoalTileName.Birds_with_No_Eggs_23 => (b, _) => b.Eggs == 0,
                GoalTileName.Birds_with_Bowl_Nests_with_Egg_8 => (b, d) => b.Eggs > 0 && Nest(d, NestType.Bowl),
                GoalTileName.Birds_with_Cavity_Nests_with_Egg_9 => (b, d) => b.Eggs > 0 && Nest(d, NestType.Cavity),
                GoalTileName.Birds_with_Ground_Nests_with_Egg_10 => (b, d) => b.Eggs > 0 && Nest(d, NestType.Ground),
                GoalTileName.Birds_with_Platform_Nests_with_Egg_11 => (b, d) => b.Eggs > 0 && Nest(d, NestType.Platform),
                _ => null,
            };
            if (counts == null)
                return null;

            var names = new List<string>();
            foreach (ActionType habitat in Habitats)
            {
                var birds = player.GetAllBirdsInHabitat(habitat);
                for (int i = 0; birds != null && i < birds.Count; i++)
                {
                    Rules.Bird bird = birds[i];
                    BirdData data = bird == null ? null : CardDescriber.DataOf(bird.ID);
                    if (data != null && counts(bird, data))
                        names.Add(BirdName(bird.ID));
                }
            }

            return names.Count == 0 ? Loc.Get("goal.counting_none") : Loc.Get("goal.counting", string.Join(", ", names));
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Goal birds for round {round} failed: {e.Message}");
            return null;
        }
    }

    private static bool Nest(BirdData data, NestType nest) => data.NestType == nest || data.NestType == NestType.Wild;

    public static string GoalText(int round)
    {
        try
        {
            Rules.GoalBoardState board = GameStateCache.State?.GoalBoardState;
            if (board != null && !_goalsLogged)
            {
                _goalsLogged = true;
                for (int i = 0; i <= 4; i++)
                    Plugin.Logger.LogInfo($"[Goals] GetGoalForRound({i}) = {board.GetGoalForRound(i)?.GoalName}");
            }

            Rules.Goal goal = board?.GetGoalForRound(round - 1);
            if (goal != null && !goal.IsEmpty)
            {
                // The game's short names are placeholders for many tiles ("KEY Short/24 ..."), so the mod names them.
                string key = "goal." + goal.GoalName;
                if (Loc.Has(key))
                    return Loc.Get(key);
                string name = GoalTilesDatabase.Get(goal.GoalName)?.GetLocalizedName();
                if (!string.IsNullOrEmpty(name) && !name.StartsWith("KEY "))
                    return UiText.Clean(name);
            }
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Goal name for round {round} failed: {e.Message}");
        }

        return TooltipGoalText(round);
    }

    /// <summary>The goal tile's tooltip in the HUD (or its texts); null if the HUD is missing.</summary>
    private static string TooltipGoalText(int round)
    {
        UnityEngine.GameObject slot = UnityEngine.GameObject.Find($"GAME/Game_Canvas/HabitatsNotchPadding/GoalsMiniBoard_HUD/TokenSlot{round}");
        if (slot == null)
            return null;

        List<string> texts = Tooltips.TextsFor(slot, includeParents: false);
        if (texts.Count == 0)
            texts = UiText.CollectTexts(slot);
        return string.Join(", ", texts);
    }

    public static string PlayerName(Rules.Player player)
    {
        string name = player.P_Username;
        if (!string.IsNullOrEmpty(name))
            return name;
        if (AutomaState(player) != null)
            return Loc.Get("game.automa");
        return Loc.Get("color." + player.P_Color);
    }

    public static string BirdName(int id)
    {
        try
        {
            string term = TextLocalizationHelper.GetBirdCardNameTerm(id);
            string name = I2.Loc.LocalizationManager.GetTranslation(term, true, 0, true, false, null, null, true);
            if (!string.IsNullOrEmpty(name))
                return UiText.Clean(name);
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Bird name for {id} failed: {e.Message}");
        }

        return $"#{id}";
    }

    public static string BonusName(int id) => UiText.HumanizeTypeName(((BonusCardName)id).ToString());

    /// <summary>"2 Fisch, 1 Samen" from a food record; empty if there is none.</summary>
    public static string FoodTypeName(FoodType type) => type switch
    {
        FoodType.Any => Loc.Get("icon.food_any"),
        FoodType.Invertebrate => Loc.Get("icon.food_invertebrate"),
        FoodType.Seed => Loc.Get("icon.food_seed"),
        FoodType.Fruit => Loc.Get("icon.food_fruit"),
        FoodType.Fish => Loc.Get("icon.food_fish"),
        FoodType.Rodent => Loc.Get("icon.food_rodent"),
        FoodType.InvertebrateSeed => Loc.Get("icon.food_invertebrate_seed"),
        FoodType.Nectar => Loc.Get("icon.food_nectar"),
        FoodType.NectarFruit => Loc.Get("icon.food_nectar_fruit"),
        FoodType.NectarSeed => Loc.Get("icon.food_nectar_seed"),
        _ => Loc.Get("icon.food_none"),
    };

    public static string FoodList(Rules.Food food)
    {
        if (food == null)
            return string.Empty;
        var parts = new List<string>();
        AddFood(parts, food.Invertebrate, "food_invertebrate");
        AddFood(parts, food.Seed, "food_seed");
        AddFood(parts, food.Fruit, "food_fruit");
        AddFood(parts, food.Fish, "food_fish");
        AddFood(parts, food.Rodent, "food_rodent");
        AddFood(parts, food.InvertebrateSeed, "food_invertebrate_seed");
        AddFood(parts, food.Nectar, "food_nectar");
        AddFood(parts, food.NectarFruit, "food_nectar_fruit");
        AddFood(parts, food.NectarSeed, "food_nectar_seed");
        return string.Join(", ", parts);
    }

    private static void AddFood(List<string> parts, int amount, string icon)
    {
        if (amount <= 0)
            return;
        // "2 Früchte", not "2 Frucht"; a language without a plural form uses the singular ("2 Samen").
        string plural = "icon." + icon + ".plural";
        string name = amount != 1 && Loc.HasOwn(plural) ? Loc.Get(plural) : Loc.Get("icon." + icon);
        parts.Add($"{amount} {name}");
    }
}
