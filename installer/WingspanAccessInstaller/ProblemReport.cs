using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WingspanAccessInstaller;

/// <summary>One file that goes into the report's ZIP.</summary>
internal sealed class ReportFile
{
    public ReportFile(string path, string zipName, bool text)
    {
        Path = path;
        ZipName = zipName;
        Text = text;
    }

    public string Path { get; }

    public string ZipName { get; }

    /// <summary>Text is cleaned of names and IDs and shortened if long; other files go as they are.</summary>
    public bool Text { get; }
}

/// <summary>One line of the attachment list: a log, or a crash with its dump.</summary>
internal sealed class ReportItem
{
    public string Title { get; set; }

    /// <summary>A short name for the report's list of attachments.</summary>
    public string Name { get; set; }

    public List<ReportFile> Files { get; } = new();

    public long Size { get; set; }

    public DateTime Time { get; set; }

    public bool Selected { get; set; }

    /// <summary>A crash dump: memory of the game, which can't be cleaned, so only on request.</summary>
    public bool IsDump { get; set; }

    public override string ToString() => Title;
}

/// <summary>The report's ZIP and what went into it.</summary>
internal sealed class ReportArchive
{
    public byte[] Zip { get; set; }

    public string FileName { get; set; }

    public List<ReportItem> Included { get; } = new();

    /// <summary>Chosen, but left out to stay under the size limit.</summary>
    public List<ReportItem> LeftOut { get; } = new();

    /// <summary>Long logs were shortened in the middle.</summary>
    public bool Shortened { get; set; }
}

/// <summary>
/// A log as its start and its end: <see cref="Head"/> (up to 128 KB, whole lines), then
/// <see cref="Skipped"/> bytes that were not read, then <see cref="Tail"/>. A short log is all
/// head and tail with nothing skipped.
/// </summary>
internal sealed class LogText
{
    public LogText(string head, string tail, long skipped)
    {
        Head = head ?? string.Empty;
        Tail = tail ?? string.Empty;
        Skipped = Math.Max(0, skipped);
    }

    public string Head { get; }

    public string Tail { get; }

    public long Skipped { get; }

    public string ZipName { get; set; }

    /// <summary>A whole text split after its first <paramref name="headChars"/> characters, at a line end.</summary>
    public static LogText Split(string text, int headChars)
    {
        if (text.Length <= headChars)
            return new LogText(text, string.Empty, 0);
        int cut = text.LastIndexOf('\n', headChars - 1);
        cut = cut > 0 ? cut + 1 : headChars;
        return new LogText(text.Substring(0, cut), text.Substring(cut), 0);
    }

    public bool CanShorten(int tailChars) => Tail.Length > tailChars + ProblemReport.Slack;

    public bool IsShortened(int tailChars) => Skipped > 0 || CanShorten(tailChars);

    /// <summary>The text with at most <paramref name="tailChars"/> characters of its end, and a note where lines are missing.</summary>
    public string Render(int tailChars)
    {
        string tail = Tail;
        long skipped = Skipped;
        if (CanShorten(tailChars))
        {
            tail = Tail.Substring(Tail.Length - tailChars);
            int start = tail.IndexOf('\n');
            if (start >= 0 && start < tail.Length - 1)
                tail = tail.Substring(start + 1);
            skipped += ProblemReport.Utf8.GetByteCount(Tail) - ProblemReport.Utf8.GetByteCount(tail);
        }

        return skipped > 0 ? Head + ProblemReport.Gap(skipped) + tail : Head + tail;
    }
}

/// <summary>
/// Replaces what identifies the user in text before it is sent: the Windows user name in profile
/// paths (C:\Users\name) and on its own, the computer name, Steam IDs (17 digits starting with
/// 7656119, which Steam's crash handler writes into the game's logs), and machine and login IDs
/// in crash reports.
/// </summary>
internal sealed class Scrubber
{
    private static readonly Regex ProfilePath = new(@"(?i)\b([a-z]:(?:\\\\|\\|/)users(?:\\\\|\\|/))[^\\/:*?""<>|\r\n]+");
    private static readonly Regex SteamId = new(@"(?<!\d)7656119\d{10}(?!\d)");
    private static readonly Regex CrashIds = new(@"<(MachineId|LoginId|EpicAccountId|UserName)>[^<]*</\1>");
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "user", "users", "admin", "administrator", "player", "owner", "default", "guest", "public", "test", "windows", "steam", "game", "home",
    };

    private readonly Regex _userName;
    private readonly Regex _machineName;

    public Scrubber(string userName, string machineName)
    {
        _userName = WordPattern(userName, 3);
        _machineName = WordPattern(machineName, 4);
    }

    public static Scrubber ForThisComputer() => new(Environment.UserName, Environment.MachineName);

    public string Apply(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        text = ProfilePath.Replace(text, match => match.Groups[1].Value + "<user>");
        text = SteamId.Replace(text, "<steamid>");
        text = CrashIds.Replace(text, match => $"<{match.Groups[1].Value}>(removed)</{match.Groups[1].Value}>");
        if (_userName != null)
            text = _userName.Replace(text, "<user>");
        if (_machineName != null)
            text = _machineName.Replace(text, "<computer>");
        return text;
    }

    private static Regex WordPattern(string name, int minLength)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length < minLength || GenericNames.Contains(name))
            return null;
        return new Regex(@"(?<![\p{L}\p{N}_])" + Regex.Escape(name) + @"(?![\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

/// <summary>
/// Gathers what a problem report can attach (BepInEx's log with the mod's messages, the mod's logs
/// of earlier game sessions, Unity's logs, the installer's log, the game's crash reports) and packs
/// the chosen ones into one ZIP under the size limit of Static Forms.
/// </summary>
internal static class ProblemReport
{
    /// <summary>Static Forms takes 4.5 MB per submission, the form fields included.</summary>
    public const long ZipBudget = 4_000_000;

    public const string LastReportFile = "last-report.txt";

    internal const int HeadChars = 128 * 1024;
    internal const int MaxTailChars = 6 * 1024 * 1024;
    internal const int MinTailChars = 160 * 1024;

    /// <summary>A log at most this much longer than what is kept is not cut at all.</summary>
    internal const int Slack = 4096;

    internal static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Local header, data descriptor and central directory entry of one ZIP entry, roughly.</summary>
    private const int EntryOverhead = 256;
    private const int SessionLogs = 3;
    private const int MaxCrashes = 3;
    private static readonly TimeSpan CrashAgeWithoutReport = TimeSpan.FromDays(14);

    /// <summary>
    /// What the mod writes when a game session starts: the first line of logs\session-1.log
    /// ("Wingspan Access session log, started ...") and a line of BepInEx's log ("Session log started ...").
    /// </summary>
    private static readonly Regex SessionStarted = new(@"[Ss]ession log,? started (\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)");

    /// <summary>The mod's logs folder: logs\session-1.log (this or the last game session) to -3.</summary>
    public static string LogsDir(string gameDir) => Path.Combine(GameInstall.PluginDir(gameDir), GameInstall.LogsFolderName);

    /// <summary>BepInEx's log of the current or last game session, with the mod's messages and Unity's.</summary>
    public static string BepInExLog(string gameDir) => Path.Combine(gameDir, "BepInEx", "LogOutput.log");

    /// <summary>Where Unity writes the game's own logs: Player.log and Player-prev.log (the session before).</summary>
    public static string UnityLogDir() => Path.Combine(LocalLowDir(), GameInstall.GameCompany, GameInstall.GameProduct);

    /// <summary>Where Unity's crash handler writes (Crash_*\crash.dmp with error.log).</summary>
    public static string CrashesDir() => Path.Combine(Path.GetTempPath(), GameInstall.GameCompany, GameInstall.GameProduct, "Crashes");

    /// <summary>When the last report was sent from this game folder (UTC); null if never.</summary>
    public static DateTime? LastReport(string gameDir)
    {
        try
        {
            string file = Path.Combine(LogsDir(gameDir), LastReportFile);
            if (File.Exists(file) && DateTime.TryParse(File.ReadAllText(file).Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime time))
                return time;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        {
            Log.Detail($"No last report time: {e.Message}");
        }

        return null;
    }

    public static void SetLastReport(string gameDir, DateTime utc)
    {
        try
        {
            string dir = LogsDir(gameDir);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, LastReportFile), utc.ToString("o", CultureInfo.InvariantCulture));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        {
            Log.Detail($"Could not write the last report time: {e.Message}");
        }
    }

    /// <summary>
    /// The attachments on offer. Logs are chosen at first; crash dumps (newer than the last report,
    /// or of the last 14 days, the newest three) only on request. The session log of the game
    /// session BepInEx's log is from is left out: BepInEx's log holds the same and more.
    /// </summary>
    public static List<ReportItem> Collect(string gameDir, string unityLogDir, string crashesDir, string installerLog, DateTime? lastReportUtc, DateTime nowUtc)
    {
        var items = new List<ReportItem>();
        if (!string.IsNullOrEmpty(gameDir))
        {
            string bepInExLog = BepInExLog(gameDir);
            string currentSession = null;
            if (TryInfo(bepInExLog, out FileInfo info))
            {
                currentSession = SessionStart(bepInExLog, firstLineOnly: false);
                items.Add(new ReportItem
                {
                    Title = Strings.Get("report_item_bepinex", Size(info.Length), Stamp(info.LastWriteTime)),
                    Name = "LogOutput.log",
                    Size = info.Length,
                    Time = info.LastWriteTimeUtc,
                    Selected = true,
                    Files = { new ReportFile(bepInExLog, "LogOutput.log", true) },
                });
            }

            for (int i = 1; i <= SessionLogs; i++)
            {
                string log = Path.Combine(LogsDir(gameDir), $"session-{i}.log");
                if (!TryInfo(log, out info))
                    continue;
                string started = SessionStart(log, firstLineOnly: true);
                if (started != null && started == currentSession)
                    continue;
                items.Add(new ReportItem
                {
                    Title = started == null
                        ? Strings.Get("report_item_session", Size(info.Length), Stamp(info.LastWriteTime))
                        : Strings.Get("report_item_session_from", Size(info.Length), Stamp(ParseStamp(started)), Stamp(info.LastWriteTime)),
                    Name = $"session-{i}.log",
                    Size = info.Length,
                    Time = info.LastWriteTimeUtc,
                    Selected = true,
                    Files = { new ReportFile(log, $"session-{i}.log", true) },
                });
            }
        }

        if (!string.IsNullOrEmpty(unityLogDir))
        {
            foreach ((string file, string key) in new[] { ("Player.log", "report_item_unity"), ("Player-prev.log", "report_item_unity_prev") })
            {
                string path = Path.Combine(unityLogDir, file);
                if (!TryInfo(path, out FileInfo unity))
                    continue;
                items.Add(new ReportItem
                {
                    Title = Strings.Get(key, Size(unity.Length), Stamp(unity.LastWriteTime)),
                    Name = file,
                    Size = unity.Length,
                    Time = unity.LastWriteTimeUtc,
                    Selected = true,
                    Files = { new ReportFile(path, file, true) },
                });
            }
        }

        if (!string.IsNullOrEmpty(installerLog) && TryInfo(installerLog, out FileInfo installer))
        {
            items.Add(new ReportItem
            {
                Title = Strings.Get("report_item_installer", Size(installer.Length)),
                Name = "installer.log",
                Size = installer.Length,
                Time = installer.LastWriteTimeUtc,
                Selected = true,
                Files = { new ReportFile(installerLog, "installer.log", true) },
            });
        }

        DateTime since = lastReportUtc ?? nowUtc - CrashAgeWithoutReport;
        items.AddRange(Crashes(crashesDir).Where(crash => crash.Time > since).Take(MaxCrashes));
        return items;
    }

    /// <summary>The game's crash reports (Unity's Crash_* folders), newest first.</summary>
    public static IEnumerable<ReportItem> Crashes(string crashesDir)
    {
        if (string.IsNullOrEmpty(crashesDir) || !Directory.Exists(crashesDir))
            return Enumerable.Empty<ReportItem>();
        var crashes = new List<ReportItem>();
        foreach (string dir in Directory.GetDirectories(crashesDir))
        {
            string dump = Path.Combine(dir, "crash.dmp");
            if (!TryInfo(dump, out FileInfo info))
                continue;
            string folder = "crash-" + info.LastWriteTime.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            var crash = new ReportItem
            {
                Title = Strings.Get("report_item_crash", Stamp(info.LastWriteTime), Size(info.Length)),
                Name = folder,
                Size = info.Length,
                Time = info.LastWriteTimeUtc,
                IsDump = true,
                Files = { new ReportFile(dump, folder + "/crash.dmp", false) },
            };
            foreach (string text in new[] { "error.log", "Player.log" })
            {
                string path = Path.Combine(dir, text);
                if (!TryInfo(path, out FileInfo textInfo))
                    continue;
                crash.Files.Add(new ReportFile(path, folder + "/" + text, true));
                crash.Size += textInfo.Length;
            }

            crashes.Add(crash);
        }

        return crashes.OrderByDescending(crash => crash.Time);
    }

    /// <summary>
    /// Packs report.txt and the chosen items into one ZIP of at most <paramref name="budget"/> bytes:
    /// long logs are shortened in the middle (their start and end stay), and if that is not enough,
    /// the oldest items are left out. <paramref name="reportText"/> writes report.txt for what is in.
    /// </summary>
    public static ReportArchive Build(IEnumerable<ReportItem> chosen, Func<ReportArchive, string> reportText, Scrubber scrubber, long budget, DateTime now)
    {
        var archive = new ReportArchive { FileName = $"WingspanAccess-report-{now:yyyyMMdd-HHmmss}.zip" };
        List<Prepared> prepared = chosen.Select(item => Prepare(item, scrubber)).Where(p => p != null).ToList();
        while (true)
        {
            archive.Included.Clear();
            archive.Included.AddRange(prepared.Select(p => p.Item));
            byte[] text = Utf8.GetBytes(reportText(archive));
            long textSize = CompressedSize(text) + EntryOverhead;
            for (int tail = MaxTailChars; ;)
            {
                // Each size is worked out once per length and kept, so trying shorter logs is quick.
                if (textSize + prepared.Sum(p => p.EstimatedSize(tail)) <= budget)
                {
                    byte[] zip = Pack(prepared, tail, text);
                    if (zip.LongLength <= budget)
                    {
                        archive.Shortened = prepared.Any(p => p.Texts.Any(t => t.IsShortened(tail)));
                        archive.Zip = zip;
                        return archive;
                    }
                }

                // Halve the logs' ends while any log can still be shortened at all.
                if (tail == MinTailChars || !prepared.Any(p => p.Texts.Any(t => t.CanShorten(MinTailChars))))
                    break;
                tail = Math.Max(MinTailChars, tail / 2);
            }

            if (prepared.Count == 0)
                throw new InvalidOperationException("The report text alone is over the size limit.");

            // The oldest goes first; among items of the same age the bigger one.
            Prepared drop = prepared.OrderBy(p => p.Item.Time).ThenByDescending(p => p.Item.Size).First();
            prepared.Remove(drop);
            archive.LeftOut.Add(drop.Item);
        }
    }

    /// <summary>
    /// When the mod's session started, as written into the log ("2026-11-24 13:09:35"): from the first
    /// line of a session log, or from the start of BepInEx's log; null if it is not there.
    /// </summary>
    public static string SessionStart(string path, bool firstLineOnly)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Utf8);
            for (int line = 0; line < (firstLineOnly ? 1 : 200); line++)
            {
                string text = reader.ReadLine();
                if (text == null)
                    break;
                Match match = SessionStarted.Match(text);
                if (match.Success)
                    return match.Groups[1].Value;
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
        }

        return null;
    }

    /// <summary>Reads a text file that may be in use; a long one as its start and its end.</summary>
    public static LogText ReadText(string path, int headChars = HeadChars, int tailChars = MaxTailChars)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long length = stream.Length;
        if (length <= (long)headChars + tailChars + Slack)
            return LogText.Split(Decode(ReadBytes(stream, 0, (int)length)), headChars);

        string head = Decode(ReadBytes(stream, 0, headChars));
        int headEnd = head.LastIndexOf('\n');
        if (headEnd > 0)
            head = head.Substring(0, headEnd + 1);
        string tail = Decode(ReadBytes(stream, length - tailChars, tailChars));
        int tailStart = tail.IndexOf('\n');
        if (tailStart >= 0 && tailStart < tail.Length - 1)
            tail = tail.Substring(tailStart + 1);
        return new LogText(head, tail, length - Utf8.GetByteCount(head) - Utf8.GetByteCount(tail));
    }

    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Format(CultureInfo.CurrentCulture, "{0:0} KB", bytes / 1024.0),
        _ => string.Format(CultureInfo.CurrentCulture, "{0:0.0} MB", bytes / (1024.0 * 1024.0)),
    };

    internal static string Gap(long bytes) =>
        $"{Environment.NewLine}[... {Size(bytes)} left out here to stay under the size limit of the report ...]{Environment.NewLine}";

    /// <summary>How big the data is in the ZIP (the same deflate the ZIP uses).</summary>
    internal static long CompressedSize(byte[] data)
    {
        var counter = new CountingStream();
        using (var deflate = new DeflateStream(counter, CompressionLevel.Optimal, true))
            deflate.Write(data, 0, data.Length);
        return counter.Length;
    }

    /// <summary>%USERPROFILE%\AppData\LocalLow, where Unity games keep their logs.</summary>
    private static string LocalLowDir()
    {
        if (NativeMethods.TryGetLocalLowFolder(out string path))
            return path;
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(Path.GetDirectoryName(local) ?? local, "LocalLow");
    }

    private static string Stamp(DateTime local) => local.ToString("g", CultureInfo.CurrentCulture);

    private static DateTime ParseStamp(string stamp) =>
        DateTime.ParseExact(stamp, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static bool TryInfo(string path, out FileInfo info)
    {
        info = null;
        try
        {
            info = new FileInfo(path);
            return info.Exists;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
        {
            return false;
        }
    }

    private static Prepared Prepare(ReportItem item, Scrubber scrubber)
    {
        var prepared = new Prepared { Item = item };
        try
        {
            foreach (ReportFile file in item.Files)
            {
                if (file.Text)
                {
                    LogText text = ReadText(file.Path);
                    prepared.Texts.Add(new LogText(scrubber.Apply(text.Head), scrubber.Apply(text.Tail), text.Skipped) { ZipName = file.ZipName });
                }
                else
                {
                    using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    prepared.Binaries.Add(new KeyValuePair<string, byte[]>(file.ZipName, ReadBytes(stream, 0, (int)Math.Min(stream.Length, int.MaxValue))));
                }
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Log.Detail($"Leaving out {item.Name}: {e.Message}");
            return null;
        }

        return prepared;
    }

    private static byte[] Pack(IEnumerable<Prepared> prepared, int tailChars, byte[] reportText)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true, Utf8))
        {
            Add(zip, "report.txt", reportText);
            foreach (Prepared item in prepared)
            {
                foreach (LogText text in item.Texts)
                    Add(zip, text.ZipName, Utf8.GetBytes(text.Render(tailChars)));
                foreach (KeyValuePair<string, byte[]> binary in item.Binaries)
                    Add(zip, binary.Key, binary.Value);
            }
        }

        return memory.ToArray();
    }

    private static void Add(ZipArchive zip, string name, byte[] data)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    private static byte[] ReadBytes(Stream stream, long offset, int count)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[count];
        int done = 0;
        while (done < count)
        {
            int read = stream.Read(buffer, done, count - done);
            if (read <= 0)
                break;
            done += read;
        }

        if (done == count)
            return buffer;
        var shorter = new byte[done];
        Buffer.BlockCopy(buffer, 0, shorter, 0, done);
        return shorter;
    }

    /// <summary>UTF-8 (BepInEx, Unity, the mod and the installer write it), or UTF-16 with a byte order mark.</summary>
    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Utf8.GetString(bytes, 3, bytes.Length - 3);
        return Utf8.GetString(bytes);
    }

    private sealed class Prepared
    {
        private readonly Dictionary<int, long> _sizes = new();
        private long _binarySize = -1;

        public ReportItem Item { get; set; }

        public List<LogText> Texts { get; } = new();

        public List<KeyValuePair<string, byte[]>> Binaries { get; } = new();

        public long EstimatedSize(int tailChars)
        {
            if (_binarySize < 0)
                _binarySize = Binaries.Sum(binary => CompressedSize(binary.Value) + EntryOverhead);
            // A tail longer than every text's own gives the same size, so it shares one entry.
            int key = Texts.Count == 0 ? 0 : Math.Min(tailChars, Texts.Max(text => text.Tail.Length));
            if (!_sizes.TryGetValue(key, out long size))
            {
                size = Texts.Sum(text => CompressedSize(Utf8.GetBytes(text.Render(tailChars))) + EntryOverhead);
                _sizes[key] = size;
            }

            return _binarySize + size;
        }
    }

    /// <summary>A stream that only counts what is written.</summary>
    private sealed class CountingStream : Stream
    {
        private long _length;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => _length += count;

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
