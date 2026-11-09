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
        try
        {
            new HarmonyLib.Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("Harmony patches applied");
        }
        catch (System.Exception e)
        {
            Log.LogError($"Harmony patching failed: {e}");
        }

        AddComponent<ModBehaviour>();
        Log.LogInfo($"{Name} {Version} loaded, speech backend: {ScreenReader.BackendName}");
    }
}
