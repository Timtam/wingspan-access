using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using WingspanAccess.Localization;

namespace WingspanAccess.Game;

/// <summary>
/// A habitat row of a player's board: the places taken, what a bird costs in eggs in the next place,
/// and what the habitat's action gives now and once a bird is in that place. The yields come from the
/// player mat the game uses (base or Oceania), one entry per number of places taken.
/// </summary>
internal sealed class HabitatRow
{
    /// <summary>The eggs a bird costs in each place of a row, on every mat.</summary>
    private static readonly int[] EggCosts = { 0, 1, 1, 2, 2 };

    /// <summary>Row states already written to the log, so each is logged once.</summary>
    private static readonly HashSet<string> Logged = new();

    public ActionType Habitat { get; private set; }

    /// <summary>Places taken (a bird on two places takes both) and places in the row.</summary>
    public int Used { get; private set; }

    public int Places { get; private set; }

    public bool Full { get; private set; }

    /// <summary>The next free place, counted from 1.</summary>
    public int NextPlace => Used + 1;

    /// <summary>What the action gives now (null if unknown).</summary>
    public TrackAbilityData Now { get; private set; }

    /// <summary>What it gives once a bird is in the next place (null if unknown or the row is full).</summary>
    public TrackAbilityData Next { get; private set; }

    public static HabitatRow Read(Rules.Player player, ActionType habitat)
    {
        Rules.Track track = player?.GetTrack(habitat);
        if (track == null)
            return null;

        int birds = track.GetBirdsCount();
        int slots = track.GetSlotsCount();
        int places = Rules.Track.MAX_SLOTS_COUNT;
        var row = new HabitatRow { Habitat = habitat, Places = places > 0 ? places : EggCosts.Length, Full = !track.HasEmptySlotsLeft() };
        // Should the slot count be the row's size rather than the places taken, the birds count.
        row.Used = slots >= row.Places && !row.Full ? birds : Math.Min(Math.Max(slots, birds), row.Places);

        (row.Now, row.Next, string source) = Abilities(player, habitat);
        string key = $"{player.P_ID}/{habitat}/{birds}/{slots}/{source}";
        if (Logged.Add(key))
            Plugin.Logger.LogInfo($"[Zone] {habitat} row: {birds} birds, {slots} slots, used {row.Used} of {row.Places}, full {row.Full}, " +
                $"yield from {source}: now {Yield(habitat, row.Now) ?? "-"}, next {Yield(habitat, row.Next) ?? "-"}");
        return row;
    }

    /// <summary>
    /// The eggs a bird costs in the place with this index (0-based). For the bird being played, the
    /// game's own rule (a discount the player chose, birds with their own cost); otherwise the mat's
    /// costs, the same for every bird.
    /// </summary>
    public static int EggCost(int place, BirdData bird = null, int discount = 0)
    {
        int standard = place >= 0 && place < EggCosts.Length ? EggCosts[place] : 0;
        if (bird == null)
            return standard;

        int eggs;
        try
        {
            eggs = Rulebook.EggsRequiredToPlayABird(place, bird, discount);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Rulebook.EggsRequiredToPlayABird failed: {e.Message}");
            return Math.Max(0, standard - discount);
        }

        if (Logged.Add($"eggs/{place}/{bird.Pointer}/{discount}"))
            Plugin.Logger.LogInfo($"[Zone] the bird costs {eggs} eggs in place {place + 1} (discount {discount}, the mat says {standard})");
        return eggs;
    }

    /// <summary>The eggs of all places in a row: "0, 1, 1, 2, 2".</summary>
    public static string EggCostList() => string.Join(", ", EggCosts);

    /// <summary>What an action gives: "2 Futter, Tausch: 1 Karte für 1 weiteres Futter"; null if unknown.</summary>
    public static string Yield(ActionType habitat, TrackAbilityData ability)
    {
        if (ability == null)
            return null;

        var parts = new List<string> { Loc.Count("yield." + habitat, ability.AmountToGain) };
        var give = new List<string>();
        if (ability.CanDiscardCardToGainMore)
            give.Add(Loc.Get("yield.give.card"));
        if (ability.CanDiscardFoodToGainMore)
            give.Add(Loc.Get("yield.give.food"));
        if (ability.CanDiscardEggToGainMore)
            give.Add(Loc.Get("yield.give.egg"));
        if (ability.CanDiscardNectarToGainMore)
            give.Add(Loc.Get("yield.give.nectar"));
        if (give.Count > 0)
        {
            string what = give[0];
            for (int i = 1; i < give.Count; i++)
                what = Loc.Get("yield.or", what, give[i]);
            string more = Loc.Get("yield.more." + habitat);
            parts.Add(ability.MaxExchangesCount > 1
                ? Loc.Get("yield.trade_times", what, more, ability.MaxExchangesCount)
                : Loc.Get("yield.trade", what, more));
        }

        if (ability.CanDiscardFoodToReRoll)
            parts.Add(Loc.Has("yield.reroll." + habitat) ? Loc.Get("yield.reroll." + habitat) : Loc.Get("yield.reroll"));
        return string.Join(", ", parts);
    }

    /// <summary>
    /// What the action gives now (the game's own lookup) and with one more place taken: the next
    /// entry on the mat the game took it from.
    /// </summary>
    private static (TrackAbilityData Now, TrackAbilityData Next, string Source) Abilities(Rules.Player player, ActionType habitat)
    {
        PlayerMatsDatabase database = PlayerMatsDatabase.Instance;
        Rules.GameState state = GameStateCache.State;
        if (database == null || state == null)
            return (null, null, "no mats");

        TrackAbilityData now;
        try
        {
            now = database.GetTrackAbility(state, player, habitat);
        }
        catch (Exception e)
        {
            return (null, null, $"failed lookup ({e.Message})");
        }

        if (now == null)
            return (null, null, "no entry");

        var mats = database.playerMats;
        for (int m = 0; mats != null && m < mats.Length; m++)
        {
            Il2CppReferenceArray<TrackAbilityData> levels = Levels(mats[m], habitat);
            for (int level = 0; levels != null && level < levels.Length; level++)
            {
                if (levels[level] != null && levels[level].Pointer == now.Pointer)
                    return (now, level + 1 < levels.Length ? levels[level + 1] : null, $"mat {mats[m].MatType} level {level}");
            }
        }

        // Not one of the mats' own entries: the level the game counts, on a mat with the same yield there.
        int counted = PlayerMatsDatabase.GetLevel(player, habitat);
        for (int m = 0; mats != null && m < mats.Length; m++)
        {
            Il2CppReferenceArray<TrackAbilityData> levels = Levels(mats[m], habitat);
            if (levels != null && counted >= 0 && counted < levels.Length && Same(levels[counted], now))
                return (now, counted + 1 < levels.Length ? levels[counted + 1] : null, $"mat {mats[m].MatType} level {counted} by value");
        }

        return (now, null, $"level {counted}, no mat");
    }

    private static Il2CppReferenceArray<TrackAbilityData> Levels(PlayerMatData mat, ActionType habitat)
    {
        if (mat == null)
            return null;
        return habitat switch
        {
            ActionType.GainFood => mat.forestAbilities,
            ActionType.LayEggs => mat.grasslandAbilities,
            ActionType.DrawBirds => mat.wetlandAbilities,
            _ => null,
        };
    }

    private static bool Same(TrackAbilityData a, TrackAbilityData b) =>
        a != null && b != null && a.AmountToGain == b.AmountToGain && a.CanDiscardCardToGainMore == b.CanDiscardCardToGainMore &&
        a.CanDiscardFoodToGainMore == b.CanDiscardFoodToGainMore && a.CanDiscardEggToGainMore == b.CanDiscardEggToGainMore &&
        a.CanDiscardNectarToGainMore == b.CanDiscardNectarToGainMore && a.MaxExchangesCount == b.MaxExchangesCount &&
        a.CanDiscardFoodToReRoll == b.CanDiscardFoodToReRoll;
}
