using System;
using UnityEngine;
using WingspanAccess.Diagnostics;
using WingspanAccess.Input;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess;

/// <summary>Unity component that runs the mod once per frame.</summary>
public class ModBehaviour : MonoBehaviour
{
    public ModBehaviour(IntPtr ptr) : base(ptr) { }

    private void Update() => ModRunner.Tick();
}

internal static class ModRunner
{
    private static bool _announcedLoad;
    private static int _errorCount;

    public static void Tick()
    {
        try
        {
            Loc.UpdateLanguage();
            if (!_announcedLoad && GamepadController.I != null)
            {
                _announcedLoad = true;
                ScreenReader.SayTransient(Loc.Get("mod.loaded"));
            }

            InputDiagnostics.DumpOnce();
            VirtualGamepad.EnsureCreated();
            if (VirtualGamepad.IsActive)
                KeyboardBridge.Tick();
            else
                Navigator.Tick();

            FocusAnnouncer.Tick();
            ScreenReaderCommands.Tick();
        }
        catch (Exception e)
        {
            if (_errorCount++ < 20)
                Plugin.Logger.LogError($"Tick failed: {e}");
        }
    }
}
