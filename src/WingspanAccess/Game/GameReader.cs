using System.Collections.Generic;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// Reads the game state on demand (number keys): habitats, birdfeeder, tray, supply, hand,
/// round and opponents. Everything comes from the game's rules model (Rules.*), so it is
/// independent of what is currently shown on screen.
/// </summary>
internal static class GameReader
{
    public static void Habitat(ActionType habitat)
    {
        Rules.Player player = GameStateCache.LocalPlayer;
        if (player == null)
        {
            ScreenReader.Say(Loc.Get("game.not_running"));
            return;
        }

        ScreenReader.Say(DescribeHabitat(player, habitat));
    }

    public static string DescribeHabitat(Rules.Player player, ActionType habitat)
    {
        string name = Loc.Get("habitat." + habitat);
        var birds = player.GetAllBirdsInHabitat(habitat);
        if (birds == null || birds.Count == 0)
            return Loc.Get("game.habitat_empty", name);

        var parts = new List<string>();
        for (int i = 0; i < birds.Count; i++)
            parts.Add(DescribeBoardBird(birds[i]));
        return Loc.Get(birds.Count == 1 ? "game.habitat.one" : "game.habitat", name, birds.Count, string.Join("; ", parts));
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

    public static void Birdfeeder()
    {
        Rules.GameState state = GameStateCache.State;
        if (state == null)
        {
            ScreenReader.Say(Loc.Get("game.not_running"));
            return;
        }

        string food = FoodList(state.BirdFeederState);
        ScreenReader.Say(Loc.Get("game.birdfeeder", food.Length == 0 ? Loc.Get("game.nothing") : food));
    }

    public static void Tray()
    {
        Rules.GameState state = GameStateCache.State;
        var visible = state?.BirdsDeckState?.Visible;
        if (visible == null)
        {
            ScreenReader.Say(Loc.Get("game.not_running"));
            return;
        }

        var names = new List<string>();
        for (int i = 0; i < visible.Count; i++)
            names.Add(BirdName(visible[i]));
        ScreenReader.Say(Loc.Get("game.tray", names.Count == 0 ? Loc.Get("game.nothing") : string.Join(", ", names)));
    }

    public static void Supply()
    {
        Rules.Player player = GameStateCache.LocalPlayer;
        if (player == null)
        {
            ScreenReader.Say(Loc.Get("game.not_running"));
            return;
        }

        string food = FoodList(player._pf);
        int birds = player.BirdCards?.Count ?? 0;
        int bonus = player.BonusCards?.Count ?? 0;
        ScreenReader.Say(Loc.Get("game.supply", food.Length == 0 ? Loc.Get("game.no_food") : food, birds, bonus));
    }

    public static void Hand()
    {
        Rules.Player player = GameStateCache.LocalPlayer;
        if (player == null)
        {
            ScreenReader.Say(Loc.Get("game.not_running"));
            return;
        }

        var birds = new List<string>();
        if (player.BirdCards != null)
        {
            for (int i = 0; i < player.BirdCards.Count; i++)
                birds.Add(BirdName(player.BirdCards[i]));
        }

        var bonuses = new List<string>();
        if (player.BonusCards != null)
        {
            for (int i = 0; i < player.BonusCards.Count; i++)
                bonuses.Add(BonusName(player.BonusCards[i]));
        }

        ScreenReader.Say(Loc.Get("game.hand",
            birds.Count == 0 ? Loc.Get("game.nothing") : string.Join(", ", birds),
            bonuses.Count == 0 ? Loc.Get("game.nothing") : string.Join(", ", bonuses)));
    }

    public static void Round()
    {
        Rules.GameState state = GameStateCache.State;
        Rules.Player player = GameStateCache.LocalPlayer;
        if (state == null || player == null)
        {
            ScreenReader.Say(Loc.Get("game.not_running"));
            return;
        }

        Rules.Player current = state.GetPlayer(state.CurrentPlayerID);
        string currentName = current == null ? "-" : PlayerName(current);
        ScreenReader.Say(Loc.Get("game.round", state.RoundsCounter + 1, player.TokensLeft, currentName));
    }

    public static void Opponents()
    {
        Rules.GameState state = GameStateCache.State;
        if (state?.Players == null)
        {
            ScreenReader.Say(Loc.Get("game.not_running"));
            return;
        }

        var parts = new List<string>();
        for (int i = 0; i < state.Players.Count; i++)
        {
            Rules.Player player = state.Players[i];
            if (player == null || player.IsLocalPlayer())
                continue;

            int forest = player.GetAllBirdsInHabitat(ActionType.GainFood)?.Count ?? 0;
            int grass = player.GetAllBirdsInHabitat(ActionType.LayEggs)?.Count ?? 0;
            int wetland = player.GetAllBirdsInHabitat(ActionType.DrawBirds)?.Count ?? 0;
            string food = FoodList(player._pf);
            parts.Add(Loc.Get("game.opponent", PlayerName(player), forest, grass, wetland,
                food.Length == 0 ? Loc.Get("game.no_food") : food, player.BirdCards?.Count ?? 0, player.TokensLeft));
        }

        ScreenReader.Say(parts.Count == 0 ? Loc.Get("game.no_opponents") : UiText.JoinSentences(parts));
    }

    /// <summary>The four round goals from the HUD goal board, using the goal tiles' tooltips where available.</summary>
    public static void Goals()
    {
        var parts = new List<string>();
        for (int round = 1; round <= 4; round++)
        {
            UnityEngine.GameObject slot = UnityEngine.GameObject.Find($"GAME/Game_Canvas/HabitatsNotchPadding/GoalsMiniBoard_HUD/TokenSlot{round}");
            if (slot == null)
                continue;

            List<string> texts = Tooltips.TextsFor(slot, includeParents: false);
            if (texts.Count == 0)
                texts = UiText.CollectTexts(slot);
            parts.Add(Loc.Get("game.goal", round, texts.Count == 0 ? Loc.Get("game.nothing") : string.Join(", ", texts)));
        }

        ScreenReader.Say(parts.Count == 0 ? Loc.Get("game.not_running") : UiText.JoinSentences(parts));
    }

    public static string PlayerName(Rules.Player player)
    {
        if (!string.IsNullOrEmpty(player.Username))
            return player.Username;
        return Loc.Get("color." + player.Color);
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
        if (amount > 0)
            parts.Add($"{amount} {Loc.Get("icon." + icon)}");
    }
}
