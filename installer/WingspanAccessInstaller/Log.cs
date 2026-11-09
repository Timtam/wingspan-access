using System;
using System.IO;

namespace WingspanAccessInstaller;

/// <summary>Writes every step to %TEMP%\WingspanAccessInstaller.log and tells the window about it.</summary>
internal static class Log
{
    public static string FilePath { get; } = Path.Combine(Path.GetTempPath(), "WingspanAccessInstaller.log");

    /// <summary>Progress lines meant for the user.</summary>
    public static event Action<string> Progress;

    public static void Start(string[] args)
    {
        try
        {
            File.WriteAllText(FilePath, $"Wingspan Access Installer {BuildInfo.Version}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                $"Arguments: {string.Join(" ", args)}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Another installer instance may hold the file; logging is best effort.
        }
    }

    /// <summary>A step the user should see.</summary>
    public static void Info(string message)
    {
        Write(message);
        Progress?.Invoke(message);
    }

    /// <summary>Details for the log file only.</summary>
    public static void Detail(string message) => Write("  " + message);

    public static void Error(Exception exception) => Write("ERROR " + exception);

    private static void Write(string message)
    {
        try
        {
            File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }
}
