using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx.Logging;

namespace WingspanAccess.Diagnostics;

/// <summary>
/// The mod's own logs of the last three game sessions (logs\session-1.log, the current one, to -3 in
/// the plugin folder), for problem reports: BepInEx's LogOutput.log starts over with every game
/// start, so the log of a session that ended in a crash would be gone once the player reports it
/// from the next one. A session log starts with what BepInEx logged before the mod (versions,
/// plugins), then gets every further line, written at once.
/// </summary>
internal sealed class SessionLog : ILogListener
{
    private const int Sessions = 3;

    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    private SessionLog(StreamWriter writer) => _writer = writer;

    /// <summary>The levels BepInEx writes into LogOutput.log (all but Debug).</summary>
    public LogLevel LogLevelFilter => LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info;

    public static void Start(ManualLogSource log)
    {
        string started = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        try
        {
            string dir = Path.Combine(Path.GetDirectoryName(typeof(SessionLog).Assembly.Location) ?? ".", "logs");
            Directory.CreateDirectory(dir);
            string Session(int number) => Path.Combine(dir, $"session-{number}.log");
            for (int number = Sessions; number >= 1; number--)
            {
                if (!File.Exists(Session(number)))
                    continue;
                if (number == Sessions)
                    File.Delete(Session(number));
                else
                    File.Move(Session(number), Session(number + 1));
            }

            var stream = new FileStream(Session(1), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine($"Wingspan Access session log, started {started}");
            writer.Write(BepInExLogSoFar());
            BepInEx.Logging.Logger.Listeners.Add(new SessionLog(writer));
            // The same time in BepInEx's log tells the problem report which session log that one is.
            log.LogInfo($"Session log started {started}: logs\\session-1.log");
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            log.LogWarning($"No session log: {e.Message}");
        }
    }

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        if ((eventArgs.Level & LogLevelFilter) == 0)
            return;
        lock (_lock)
        {
            try
            {
                _writer.Write(eventArgs.ToStringLine());
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException)
            {
                // Logging is best effort; the game goes on.
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
            _writer.Dispose();
    }

    /// <summary>What BepInEx has written into LogOutput.log of this session so far.</summary>
    private static string BepInExLogSoFar()
    {
        try
        {
            string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "LogOutput.log");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return $"(BepInEx's log so far could not be read: {e.Message}){Environment.NewLine}";
        }
    }
}
