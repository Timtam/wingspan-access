using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "wingspan.access";
    public const string Name = "Wingspan Access";
    public const string Version = BuildInfo.Version;

    internal static ManualLogSource Logger { get; private set; }

    public override void Load()
    {
        Logger = Log;
        Loc.Initialize();
        ScreenReader.Initialize(Log);
        Updates.UpdateChecker.Initialize(Config);
        // Each patch class on its own, so one that no longer fits the game (a renamed or overloaded
        // method after an update) does not keep the others from being applied.
        var harmony = new HarmonyLib.Harmony(Guid);
        int failed = 0;
        foreach (System.Type type in HarmonyLib.AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
        {
            if (type.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), true).Length == 0)
                continue;
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (System.Exception e)
            {
                failed++;
                Log.LogError($"Harmony patch {type.Name} failed: {e.Message}");
            }
        }

        Log.LogInfo(failed == 0 ? "Harmony patches applied" : $"Harmony patches applied, {failed} failed");

        AddComponent<ModBehaviour>();
        Log.LogInfo($"{Name} {Version} loaded, speech backend: {ScreenReader.BackendName}");
    }
}
