using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// Announces what another player did on their turn by comparing their part of the game state
/// before and after: birds played (with habitat), eggs laid, food gained or spent, cards drawn.
/// </summary>
internal static class OpponentWatcher
{
    private sealed class Snapshot
    {
        public readonly Dictionary<int, ActionType> Birds = new();
        public int Eggs;
        public int Cached;
        public int Tucked;
        public int HandCards;
        public int BonusCards;
        public Dictionary<string, int> Food = new();
    }

    private static readonly Dictionary<string, Snapshot> Snapshots = new();

    /// <summary>
    /// The other players as last reported (at the local player's turn or after their own turn).
    /// Online the other player's moves arrive all at once while the game reloads, without the
    /// current player changing on this side; what changed since is read when the local player's
    /// turn begins.
    /// </summary>
    private static readonly Dictionary<string, Snapshot> AtLocalTurn = new();
    private static bool _turnPopupShown;
    /// <summary>Frame at which the local turn's summary is read (0: none pending).</summary>
    private static int _localTurnFrame;

    /// <summary>Frame at which the moves that came with a reloaded board are read (0: none pending).</summary>
    private static int _reloadFrame;

    /// <summary>A new game starts: nothing from the previous one counts.</summary>
    public static void Reset()
    {
        Snapshots.Clear();
        AtLocalTurn.Clear();
        _currentPlayer = null;
        _pendingPlayer = null;
        _localTurnFrame = 0;
        _reloadFrame = 0;
    }

    /// <summary>The game set up a new board state (online: another player's moves arrived).</summary>
    public static void BoardReloaded() => _reloadFrame = Time.frameCount + LocalTurnDelayFrames;
    private const int LocalTurnDelayFrames = 30;
    private static string _currentPlayer;
    private static string _pendingPlayer;
    private static int _pendingFrame = -1;
    private static int _nextCheck;

    /// <summary>The board is updated a little after the turn passes on (animations), so the summary waits.</summary>
    private const int SettleFrames = 90;

    public static void Tick()
    {
        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 15;

        Rules.GameState state = GameStateCache.State;
        if (state?.Players == null)
            return;

        if (_pendingPlayer != null && Time.frameCount >= _pendingFrame)
        {
            Summarize(state, _pendingPlayer);
            _pendingPlayer = null;
        }

        // Whose turn it is as the game last saved it, not the live value: against the AI the saved
        // copy changes as each turn starts, which the AI summaries rely on. Online it changes only
        // now and then; there the summary at the local turn covers the other player's moves.
        string current = state.CurrentPlayerID;
        if (current != _currentPlayer)
        {
            string previous = _currentPlayer;
            _currentPlayer = current;
            Plugin.Logger.LogInfo($"[Opponent] current player {previous ?? "-"} -> {current}");
            if (previous != null)
            {
                if (_pendingPlayer != null)
                    Summarize(state, _pendingPlayer);
                _pendingPlayer = previous;
                _pendingFrame = Time.frameCount + SettleFrames;
            }
        }

        // The AI acts within a few frames of its turn starting, so the "before" state of every
        // player who is not acting is kept fresh all the time.
        for (int i = 0; i < state.Players.Count; i++)
        {
            Rules.Player each = state.Players[i];
            if (each != null && !string.IsNullOrEmpty(each.ID) && each.ID != current && each.ID != _pendingPlayer)
                Snapshots[each.ID] = Take(each);
        }

        // Players seen for the first time (an online game just loaded, the other player's move still to
        // come): their state now is the "before" of the summary at the local turn.
        for (int i = 0; i < state.Players.Count; i++)
        {
            Rules.Player each = state.Players[i];
            if (each != null && !string.IsNullOrEmpty(each.ID) && !each.IsLocalPlayer() && !AtLocalTurn.ContainsKey(each.ID))
                AtLocalTurn[each.ID] = Take(each);
        }

        // The local player's turn: the summary follows the turn popup a moment later, when the board
        // shows the other players' moves (online they arrive all at once while the game reloads).
        GamepadScreen top = UI.Screens.Top();
        bool turnPopup = top?.TryCast<NewTurnAnnouncement_Popup>() != null;
        if (turnPopup && !_turnPopupShown)
            _localTurnFrame = Time.frameCount + LocalTurnDelayFrames;
        _turnPopupShown = turnPopup;
        if (_localTurnFrame > 0 && Time.frameCount >= _localTurnFrame)
        {
            _localTurnFrame = 0;
            OnLocalTurn(state);
        }

        // Online the other player's moves arrive with a new board state: read them right away, before
        // a power of yours that reacts to them or your turn.
        if (_reloadFrame > 0 && Time.frameCount >= _reloadFrame)
        {
            _reloadFrame = 0;
            OnLocalTurn(state);
        }
    }

    /// <summary>Reads what the other players changed since their last report (at the local turn, or when their moves arrive).</summary>
    private static void OnLocalTurn(Rules.GameState state)
    {
        if (_pendingPlayer != null)
        {
            Summarize(state, _pendingPlayer);
            _pendingPlayer = null;
        }

        for (int i = 0; i < state.Players.Count; i++)
        {
            Rules.Player player = state.Players[i];
            // Right at the start of a game a player can be without an id yet.
            if (player == null || string.IsNullOrEmpty(player.ID) || player.IsLocalPlayer())
                continue;
            Snapshot now = Take(player);
            if (AtLocalTurn.TryGetValue(player.ID, out Snapshot before))
            {
                string summary = Describe(player, before, now);
                Plugin.Logger.LogInfo($"[Opponent] changes of {player.ID}: {summary ?? "none"}");
                if (summary != null)
                    ScreenReader.Say(summary, interrupt: false);
            }

            AtLocalTurn[player.ID] = now;
            Snapshots[player.ID] = now;
        }

    }

    /// <summary>Announces the finished player's changes and remembers their new state.</summary>
    private static void Summarize(Rules.GameState state, string playerId)
    {
        Rules.Player player = state.GetPlayer(playerId);
        if (player == null)
            return;

        Snapshot after = Take(player);
        if (!player.IsLocalPlayer() && Snapshots.TryGetValue(playerId, out Snapshot before))
        {
            string summary = Describe(player, before, after);
            if (summary != null)
                ScreenReader.Say(summary, interrupt: false);
            // What was read counts as known: the summary at the local turn only adds what changed since.
            AtLocalTurn[playerId] = after;
        }

        Snapshots[playerId] = after;
    }

    private static Snapshot Take(Rules.Player player)
    {
        var snapshot = new Snapshot
        {
            HandCards = GameReader.HandCount(player),
            BonusCards = GameReader.BonusCount(player),
        };

        foreach (ActionType habitat in new[] { ActionType.GainFood, ActionType.LayEggs, ActionType.DrawBirds })
        {
            var birds = player.GetAllBirdsInHabitat(habitat);
            if (birds == null)
                continue;
            for (int i = 0; i < birds.Count; i++)
            {
                Rules.Bird bird = birds[i];
                snapshot.Birds[bird.ID] = habitat;
                snapshot.Eggs += bird.Eggs;
                snapshot.Cached += bird.CachedFoodCount;
                snapshot.Tucked += bird.TuckedCardsCount;
            }
        }

        Rules.Food food = player._pf;
        if (food != null)
        {
            snapshot.Food["food_invertebrate"] = food.Invertebrate;
            snapshot.Food["food_seed"] = food.Seed;
            snapshot.Food["food_fruit"] = food.Fruit;
            snapshot.Food["food_fish"] = food.Fish;
            snapshot.Food["food_rodent"] = food.Rodent;
            snapshot.Food["food_nectar"] = food.Nectar;
        }

        return snapshot;
    }

    private static string Describe(Rules.Player player, Snapshot before, Snapshot after)
    {
        var parts = new List<string>();
        int played = 0;
        foreach (var pair in after.Birds)
        {
            if (before.Birds.ContainsKey(pair.Key))
                continue;
            played++;
            parts.Add(Loc.Get("opponent.played", GameReader.BirdName(pair.Key), Loc.Get("habitat." + pair.Value)));
        }

        AddDelta(parts, after.Eggs - before.Eggs, "opponent.eggs");
        AddDelta(parts, after.Cached - before.Cached, "opponent.cached");
        AddDelta(parts, after.Tucked - before.Tucked, "opponent.tucked");
        // Played birds came from the hand; only report other changes of the hand size.
        AddDelta(parts, after.HandCards - before.HandCards + played, "opponent.cards");
        AddDelta(parts, after.BonusCards - before.BonusCards, "opponent.bonus");

        var gained = new List<string>();
        var spent = new List<string>();
        foreach (var pair in after.Food)
        {
            before.Food.TryGetValue(pair.Key, out int old);
            int delta = pair.Value - old;
            if (delta > 0)
                gained.Add($"{delta} {Loc.Get("icon." + pair.Key)}");
            else if (delta < 0)
                spent.Add($"{-delta} {Loc.Get("icon." + pair.Key)}");
        }

        if (gained.Count > 0)
            parts.Add(Loc.Get("opponent.food_gained", string.Join(", ", gained)));
        if (spent.Count > 0)
            parts.Add(Loc.Get("opponent.food_spent", string.Join(", ", spent)));

        if (parts.Count == 0)
            return null;
        return Loc.Get("opponent.turn", GameReader.PlayerName(player), string.Join(", ", parts));
    }

    private static void AddDelta(List<string> parts, int delta, string key)
    {
        if (delta > 0)
            parts.Add(Loc.Count(key + ".plus", delta));
        else if (delta < 0)
            parts.Add(Loc.Count(key + ".minus", -delta));
    }
}
