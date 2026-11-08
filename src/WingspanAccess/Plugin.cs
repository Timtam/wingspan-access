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
    public const string Version = "0.1.0";

    internal static ManualLogSource Logger { get; private set; }

    public override void Load()
    {
        Logger = Log;
        Loc.Initialize();
        ScreenReader.Initialize(Log);
        AddComponent<ModBehaviour>();
        Log.LogInfo($"{Name} {Version} loaded, speech backend: {ScreenReader.BackendName}");
    }
}
