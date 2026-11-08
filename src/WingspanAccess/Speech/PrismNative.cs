using System;
using System.Runtime.InteropServices;

namespace WingspanAccess.Speech;

/// <summary>
/// Minimal P/Invoke surface of the Prism C API (include/prism.h, v0.18.x).
/// On Windows every function uses __cdecl. Strings are null-terminated UTF-8.
/// </summary>
internal static class PrismNative
{
    internal const string Library = "prism";

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr prism_init(IntPtr config);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void prism_shutdown(IntPtr context);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr prism_registry_create_best(IntPtr context);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr prism_backend_name(IntPtr backend);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int prism_backend_output(IntPtr backend, byte[] utf8Text, [MarshalAs(UnmanagedType.U1)] bool interrupt);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int prism_backend_stop(IntPtr backend);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void prism_backend_free(IntPtr backend);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr prism_error_string(int error);
}
