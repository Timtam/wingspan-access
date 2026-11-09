namespace WingspanAccess.Game;

/// <summary>
/// Keeps the game's GameStateDriver. Every decision holds a reference to it; the mod remembers
/// the latest one so the game state can also be read while other players act.
/// </summary>
internal static class GameStateCache
{
    public static GameStateDriver Driver { get; private set; }

    public static void Update(GamepadScreen top)
    {
        Decision decision = top == null ? null : top.TryCast<Decision>();
        if (decision == null)
            return;

        Use(decision.gameStateDriver);
    }

    /// <summary>
    /// Takes the driver the board view is set up with, starts a turn with or a task works with. Online
    /// the game sets up a new one whenever another player's moves arrive; the opponent watcher then
    /// reads them right away.
    /// </summary>
    public static void Use(GameStateDriver driver)
    {
        if (driver == null)
            return;
        if (Driver != null && Driver.Pointer != driver.Pointer)
        {
            Plugin.Logger.LogInfo("[State] board driver changed");
            OpponentWatcher.BoardReloaded();
        }

        Driver = driver;
    }

    public static Rules.GameState State => Driver?.GameState;

    public static Rules.Player LocalPlayer
    {
        get
        {
            Rules.GameState state = State;
            if (state?.Players == null)
                return null;
            for (int i = 0; i < state.Players.Count; i++)
            {
                Rules.Player player = state.Players[i];
                if (player != null && player.IsLocalPlayer())
                    return player;
            }

            return null;
        }
    }
}

/// <summary>
/// Online the game can reload the board with a new state when another player's moves arrive; the
/// board view's turn start passes the current driver, so the cache follows before the next task.
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(LocalGameStateView), nameof(LocalGameStateView.OnTurnStarted), new[] { typeof(GameStateDriver), typeof(BasePlayerController) })]
internal static class TurnStartedPatch
{
    private static void Prefix(GameStateDriver gs) => GameStateCache.Use(gs);
}

[HarmonyLib.HarmonyPatch(typeof(LocalGameStateView), nameof(LocalGameStateView.Setup))]
internal static class BoardSetupPatch
{
    private static void Prefix(GameStateDriver driver) => GameStateCache.Use(driver);
}
