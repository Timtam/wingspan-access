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
    public static string PlayerSummary(Rules.Player player)
    {
        string name = player.IsLocalPlayer() ? Loc.Get("zone.you") : PlayerName(player);
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
