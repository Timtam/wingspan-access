using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Logging;

namespace WingspanAccess.Speech;

/// <summary>
/// Speech and braille output through Prism. Every message is also written to the BepInEx log,
/// so the mod can be debugged without a screen reader.
/// </summary>
internal static class ScreenReader
{
    private static ManualLogSource _log;
    private static IntPtr _context;
    private static IntPtr _backend;

    public static string BackendName { get; private set; } = "none";

    public static void Initialize(ManualLogSource log)
    {
        _log = log;
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(ScreenReader).Assembly, ResolvePrism);
            _context = PrismNative.prism_init(IntPtr.Zero);
            if (_context == IntPtr.Zero)
            {
                log.LogError("prism_init failed");
                return;
            }

            _backend = PrismNative.prism_registry_create_best(_context);
            if (_backend == IntPtr.Zero)
            {
                log.LogError("Prism found no usable speech backend");
                return;
            }

            BackendName = Marshal.PtrToStringUTF8(PrismNative.prism_backend_name(_backend)) ?? "unknown";
        }
        catch (Exception e)
        {
            log.LogError($"Prism could not be initialized: {e}");
        }
    }

    private static int _lastSpeechFrame = -1;

    /// <summary>
    /// Speaks and brailles a message. Messages spoken within the same frame are queued instead of
    /// interrupting each other, so "screen name" and "selected element" are both heard.
    /// </summary>
    public static void Say(string text, bool interrupt = true)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        int frame = UnityEngine.Time.frameCount;
        if (frame == _lastSpeechFrame)
            interrupt = false;
        _lastSpeechFrame = frame;

        _log?.LogInfo($"[Speech{(interrupt ? "" : "+")}] {text}");
        if (_backend == IntPtr.Zero)
            return;

        int error = PrismNative.prism_backend_output(_backend, Encoding.UTF8.GetBytes(text + "\0"), interrupt);
        if (error != 0)
            _log?.LogWarning($"Prism output failed: {ErrorString(error)} ({error})");
    }

    public static void Stop()
    {
        if (_backend != IntPtr.Zero)
            PrismNative.prism_backend_stop(_backend);
    }

    private static string ErrorString(int error) =>
        Marshal.PtrToStringUTF8(PrismNative.prism_error_string(error)) ?? error.ToString();

    private static IntPtr ResolvePrism(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != PrismNative.Library)
            return IntPtr.Zero;

        string path = Path.Combine(Path.GetDirectoryName(assembly.Location) ?? ".", "prism.dll");
        return NativeLibrary.TryLoad(path, out IntPtr handle) ? handle : IntPtr.Zero;
    }
}
