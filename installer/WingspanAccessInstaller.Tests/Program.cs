using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace WingspanAccessInstaller.Tests;

internal static class Program
{
    private static int _failed;
    private static int _passed;
    private static string _temp;

    [STAThread]
    private static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        _temp = Path.Combine(Path.GetTempPath(), "WingspanAccessInstallerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        // The installer's log goes to the test folder, not to %TEMP%\WingspanAccessInstaller.log.
        Log.FilePath = Path.Combine(_temp, "installer.log");
        try
        {
            Run(nameof(AltchaSolvesSha256), AltchaSolvesSha256);
            Run(nameof(AltchaSolvesSha1AndSha512), AltchaSolvesSha1AndSha512);
            Run(nameof(AltchaFindsZeroAndMax), AltchaFindsZeroAndMax);
            Run(nameof(AltchaReadsMaxNumberSpellings), AltchaReadsMaxNumberSpellings);
            Run(nameof(AltchaRefusesOtherFormats), AltchaRefusesOtherFormats);
            Run(nameof(AltchaFailsWithoutSolution), AltchaFailsWithoutSolution);
            Run(nameof(AltchaCanBeCanceled), AltchaCanBeCanceled);
            Run(nameof(AltchaIsQuickEnough), AltchaIsQuickEnough);
            Run(nameof(ScrubberReplacesProfilePaths), ScrubberReplacesProfilePaths);
            Run(nameof(ScrubberReplacesIdsAndNames), ScrubberReplacesIdsAndNames);
            Run(nameof(LogTextKeepsStartAndEnd), LogTextKeepsStartAndEnd);
            Run(nameof(BuildPacksCleanedFiles), BuildPacksCleanedFiles);
            Run(nameof(BuildShortensLongLogs), BuildShortensLongLogs);
            Run(nameof(BuildLeavesOutOldestWhenTooBig), BuildLeavesOutOldestWhenTooBig);
            Run(nameof(CollectFindsLogsAndNewCrashes), CollectFindsLogsAndNewCrashes);
            Run(nameof(LastReportTimeRoundTrips), LastReportTimeRoundTrips);
            Run(nameof(ClientSendsReportWithAltcha), ClientSendsReportWithAltcha);
            Run(nameof(ClientMapsErrors), ClientMapsErrors);
            Run(nameof(ClientReportsNoConnection), ClientReportsNoConnection);
            Run(nameof(ReportKeyMatchesTheBuild), ReportKeyMatchesTheBuild);
            Run(nameof(ReportWindowIsBuiltForScreenReaders), ReportWindowIsBuiltForScreenReaders);
            Run(nameof(MainWindowReportButtonFollowsTheKey), MainWindowReportButtonFollowsTheKey);
            Run(nameof(OptionsKnowReport), OptionsKnowReport);
            Run(nameof(ReportTextAndEmailCheck), ReportTextAndEmailCheck);
        }
        finally
        {
            try
            {
                Directory.Delete(_temp, true);
            }
            catch (IOException)
            {
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{_passed} passed, {_failed} failed (report key built in: {(ReportKey.Available ? "yes" : "no")})");
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            test();
            _passed++;
            Console.WriteLine($"PASS {name} ({watch.ElapsedMilliseconds} ms)");
        }
        catch (Exception e)
        {
            _failed++;
            Console.WriteLine($"FAIL {name}: {e}");
        }
    }

    // ----- ALTCHA -----

    /// <summary>A challenge made the way altcha-lib makes one: salt "hex?expires=...&amp;", challenge = hash(salt + number).</summary>
    private static string MakeChallenge(string algorithm, long number, long max, string maxName = "maxnumber", string salt = null)
    {
        salt ??= RandomHex(12) + "?expires=" + DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() + "&";
        string challenge = Altcha.HashHex(algorithm, salt, number);
        var map = new Dictionary<string, object>
        {
            ["algorithm"] = algorithm,
            ["challenge"] = challenge,
            [maxName] = max,
            ["salt"] = salt,
            ["signature"] = RandomHex(32),
        };
        return new JavaScriptSerializer().Serialize(map);
    }

    private static void AltchaSolvesSha256()
    {
        string json = MakeChallenge("SHA-256", 54321, 100_000);
        string payload = Altcha.SolveToPayload(json, CancellationToken.None);
        var decoded = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        var challenge = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
        Equal(54321L, Convert.ToInt64(decoded["number"]), "number");
        Equal("SHA-256", decoded["algorithm"], "algorithm");
        Equal(challenge["challenge"], decoded["challenge"], "challenge");
        Equal(challenge["salt"], decoded["salt"], "salt");
        Equal(challenge["signature"], decoded["signature"], "signature");
        True(decoded.ContainsKey("took"), "took is there");
        // The same field order as the widget's JSON.stringify.
        string text = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        True(text.StartsWith("{\"algorithm\":\"SHA-256\",\"challenge\":\"", StringComparison.Ordinal), "field order: " + text);
        True(text.IndexOf("\"number\":54321,\"salt\":", StringComparison.Ordinal) > 0, "number then salt: " + text);
        // What the server checks: hash(salt + number) == challenge.
        Equal(decoded["challenge"], Altcha.HashHex("SHA-256", (string)decoded["salt"], 54321), "server check");
    }

    private static void AltchaSolvesSha1AndSha512()
    {
        foreach (string algorithm in new[] { "SHA-1", "SHA-512", "sha-256" })
        {
            AltchaChallenge challenge = Altcha.Parse(MakeChallenge(algorithm, 777, 5000));
            Equal(777L, Altcha.Solve(challenge, CancellationToken.None).Number, algorithm);
        }
    }

    private static void AltchaFindsZeroAndMax()
    {
        Equal(0L, Altcha.Solve(Altcha.Parse(MakeChallenge("SHA-256", 0, 300_000)), CancellationToken.None).Number, "zero");
        Equal(300_000L, Altcha.Solve(Altcha.Parse(MakeChallenge("SHA-256", 300_000, 300_000)), CancellationToken.None).Number, "max");
        Equal(12L, Altcha.Solve(Altcha.Parse(MakeChallenge("SHA-256", 12, 12)), CancellationToken.None).Number, "tiny max");
    }

    private static void AltchaReadsMaxNumberSpellings()
    {
        Equal(5000L, Altcha.Parse(MakeChallenge("SHA-256", 1, 5000, "maxNumber")).MaxNumber, "maxNumber");
        Equal(6000L, Altcha.Parse(MakeChallenge("SHA-256", 1, 6000, "maxnumber")).MaxNumber, "maxnumber");
        string json = MakeChallenge("SHA-256", 1, 6000, "unused");
        Equal(Altcha.DefaultMaxNumber, Altcha.Parse(json).MaxNumber, "default");
    }

    private static void AltchaRefusesOtherFormats()
    {
        Throws<AltchaException>(() => Altcha.Parse("{\"parameters\":{\"algorithm\":\"PBKDF2/SHA-256\",\"nonce\":\"00\",\"salt\":\"00\",\"cost\":1,\"keyPrefix\":\"00\"},\"signature\":\"ab\"}"), "v2");
        Throws<AltchaException>(() => Altcha.Parse("{\"algorithm\":\"MD5\",\"challenge\":\"00\",\"salt\":\"s\",\"signature\":\"x\"}"), "MD5");
        Throws<AltchaException>(() => Altcha.Parse("not json"), "not json");
        Throws<AltchaException>(() => Altcha.Parse("[1,2]"), "array");
        Throws<AltchaException>(() => Altcha.Parse("{\"algorithm\":\"SHA-256\",\"salt\":\"x\"}"), "no challenge");
    }

    private static void AltchaFailsWithoutSolution()
    {
        // The number is above maxnumber: no number fits.
        AltchaChallenge challenge = Altcha.Parse(MakeChallenge("SHA-256", 2000, 1000));
        Throws<AltchaException>(() => Altcha.Solve(challenge, CancellationToken.None), "above max");
    }

    private static void AltchaCanBeCanceled()
    {
        AltchaChallenge challenge = Altcha.Parse(MakeChallenge("SHA-256", 50_000_000, 50_000_000));
        using var cancel = new CancellationTokenSource(100);
        var watch = Stopwatch.StartNew();
        Throws<OperationCanceledException>(() => Altcha.Solve(challenge, cancel.Token), "canceled");
        True(watch.ElapsedMilliseconds < 3000, $"stopped in {watch.ElapsedMilliseconds} ms");
    }

    private static void AltchaIsQuickEnough()
    {
        // The widget's default maximum, the number at its end: the worst case a form gives.
        AltchaChallenge challenge = Altcha.Parse(MakeChallenge("SHA-256", 999_999, 1_000_000));
        AltchaSolution solution = Altcha.Solve(challenge, CancellationToken.None);
        Equal(999_999L, solution.Number, "number");
        Console.WriteLine($"     1,000,000 hashes in {solution.TookMs} ms on {Environment.ProcessorCount} processors");
        True(solution.TookMs < 15000, "under 15 s");
    }

    // ----- Cleaning -----

    private static void ScrubberReplacesProfilePaths()
    {
        var scrubber = new Scrubber("Jörg Müller", "DESKTOP-AB12CD3");
        Equal(@"C:\Users\<user>\Documents\x.log", scrubber.Apply(@"C:\Users\Jörg Müller\Documents\x.log"), "backslashes");
        Equal("C:/Users/<user>/AppData", scrubber.Apply("C:/Users/Jörg Müller/AppData"), "slashes");
        Equal(@"C:\\Users\\<user>\\AppData", scrubber.Apply(@"C:\\Users\\Jörg Müller\\AppData"), "escaped");
        Equal(@"d:\users\<user>\x", scrubber.Apply(@"d:\users\someone else\x"), "other user, other drive");
        Equal(@"C:\Program Files (x86)\Steam\steamapps\common\Wingspan", scrubber.Apply(@"C:\Program Files (x86)\Steam\steamapps\common\Wingspan"), "game path stays");
        Equal(@"C:\Users\<user>\AppData\LocalLow\Monster Couch\Wingspan\Player.log",
            scrubber.Apply(@"C:\Users\Jörg Müller\AppData\LocalLow\Monster Couch\Wingspan\Player.log"), "Unity's log folder");
    }

    private static void ScrubberReplacesIdsAndNames()
    {
        var scrubber = new Scrubber("Toni", "DESKTOP-AB12CD3");
        Equal("Caching Steam ID:  <steamid> [API loaded no]", scrubber.Apply("Caching Steam ID:  76561198012345678 [API loaded no]"), "Steam ID");
        Equal("id 765611980123456789", scrubber.Apply("id 765611980123456789"), "18 digits stay");
        Equal("hello <user>, Tonio and toniX stay", scrubber.Apply("hello Toni, Tonio and toniX stay"), "user name as a word");
        Equal("on <computer> now", scrubber.Apply("on DESKTOP-AB12CD3 now"), "computer name");
        Equal("<MachineId>(removed)</MachineId><LoginId>(removed)</LoginId><UserName>(removed)</UserName>",
            scrubber.Apply("<MachineId>09841C9F464A54D9</MachineId><LoginId>09841c9f</LoginId><UserName>Toni</UserName>"), "crash context IDs");
        Equal("Player 1 joined", new Scrubber("Player", "PC").Apply("Player 1 joined"), "generic names stay");
    }

    // ----- Reading and packing -----

    private static void LogTextKeepsStartAndEnd()
    {
        string small = WriteFile("small.log", "line 1\nline 2\n");
        LogText whole = ProblemReport.ReadText(small);
        Equal("line 1\nline 2\n", whole.Render(ProblemReport.MaxTailChars), "small file whole");

        var text = new StringBuilder();
        for (int i = 0; i < 200_000; i++)
            text.Append("line ").Append(i).Append(" some log text\n");
        string big = WriteFile("big.log", text.ToString());
        long length = new FileInfo(big).Length;
        LogText cut = ProblemReport.ReadText(big, 1000, 2000);
        True(cut.Skipped > 0, "skipped");
        True(cut.Head.StartsWith("line 0 ", StringComparison.Ordinal) && cut.Head.EndsWith("\n", StringComparison.Ordinal), "head is whole lines");
        True(cut.Tail.StartsWith("line ", StringComparison.Ordinal) && cut.Tail.EndsWith("line 199999 some log text\n", StringComparison.Ordinal), "tail is whole lines to the end");
        Equal(length, ProblemReport.Utf8.GetByteCount(cut.Head) + cut.Skipped + ProblemReport.Utf8.GetByteCount(cut.Tail), "sizes add up");
        string rendered = cut.Render(ProblemReport.MaxTailChars);
        True(rendered.Contains("left out here"), "gap note");

        LogText split = LogText.Split(text.ToString(), ProblemReport.HeadChars);
        string shorter = split.Render(200_000);
        True(shorter.Length < 400_000 && shorter.Contains("left out here") && shorter.EndsWith("line 199999 some log text\n", StringComparison.Ordinal), "shorter tail");
    }

    private static void BuildPacksCleanedFiles()
    {
        string log = WriteFile("clean.log", "start\nC:\\Users\\Toni\\AppData\\LocalLow\\Monster Couch\\Wingspan\nSteam ID: 76561198012345678\nend\n");
        string dump = WriteBytes("clean.dmp", RandomBytes(5000));
        var items = new List<ReportItem>
        {
            new() { Name = "session-1.log", Time = DateTime.UtcNow, Files = { new ReportFile(log, "session-1.log", true) } },
            new() { Name = "crash-x", Time = DateTime.UtcNow, IsDump = true, Files = { new ReportFile(dump, "crash-x/crash.dmp", false) } },
        };
        ReportArchive archive = ProblemReport.Build(items, a => "report for " + a.Included.Count, new Scrubber("Toni", "PCNAME"), ProblemReport.ZipBudget, new DateTime(2026, 11, 23, 14, 5, 9));
        Equal("WingspanAccess-report-20261123-140509.zip", archive.FileName, "name");
        Equal(2, archive.Included.Count, "included");
        Equal(0, archive.LeftOut.Count, "left out");
        False(archive.Shortened, "not shortened");
        Dictionary<string, byte[]> entries = Unzip(archive.Zip);
        Equal("report for 2", Encoding.UTF8.GetString(entries["report.txt"]), "report.txt");
        Equal("start\nC:\\Users\\<user>\\AppData\\LocalLow\\Monster Couch\\Wingspan\nSteam ID: <steamid>\nend\n", Encoding.UTF8.GetString(entries["session-1.log"]), "cleaned log");
        True(entries["crash-x/crash.dmp"].SequenceEqual(File.ReadAllBytes(dump)), "dump unchanged");
    }

    private static void BuildShortensLongLogs()
    {
        // Text that does not compress well, so the budget forces shortening.
        var random = new Random(7);
        var text = new StringBuilder();
        for (int i = 0; text.Length < 3_000_000; i++)
            text.Append(i).Append(' ').Append(RandomHex(40, random)).Append('\n');
        text.Append("THE LAST LINE\n");
        string log = WriteFile("long.log", text.ToString());
        var items = new List<ReportItem> { new() { Name = "LogOutput.log", Time = DateTime.UtcNow, Files = { new ReportFile(log, "LogOutput.log", true) } } };
        ReportArchive archive = ProblemReport.Build(items, _ => "report", new Scrubber("nobody-here", "nopc"), 600_000, DateTime.Now);
        True(archive.Zip.Length <= 600_000, $"under the budget: {archive.Zip.Length}");
        True(archive.Shortened, "shortened");
        Equal(0, archive.LeftOut.Count, "nothing left out");
        string packed = Encoding.UTF8.GetString(Unzip(archive.Zip)["LogOutput.log"]);
        True(packed.StartsWith("0 ", StringComparison.Ordinal), "start kept");
        True(packed.EndsWith("THE LAST LINE\n", StringComparison.Ordinal), "end kept");
        True(packed.Contains("left out here"), "gap note");
    }

    private static void BuildLeavesOutOldestWhenTooBig()
    {
        // Three dumps that do not compress: 700 KB each, a budget of 1.6 MB.
        var items = new List<ReportItem>();
        for (int i = 0; i < 3; i++)
        {
            string dump = WriteBytes($"dump{i}.dmp", RandomBytes(700 * 1024));
            items.Add(new ReportItem
            {
                Name = $"crash-{i}",
                Time = new DateTime(2026, 11, 20 + i, 0, 0, 0, DateTimeKind.Utc),
                Size = 700 * 1024,
                IsDump = true,
                Files = { new ReportFile(dump, $"crash-{i}/crash.dmp", false) },
            });
        }

        string small = WriteFile("small2.log", "a small log\n");
        items.Add(new ReportItem { Name = "session-1.log", Time = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc), Files = { new ReportFile(small, "session-1.log", true) } });
        ReportArchive archive = ProblemReport.Build(items, a => "left out: " + string.Join(",", a.LeftOut.Select(x => x.Name)), new Scrubber("x", "y"), 1_600_000, DateTime.Now);
        True(archive.Zip.Length <= 1_600_000, $"under the budget: {archive.Zip.Length}");
        Equal("crash-0", string.Join(",", archive.LeftOut.Select(x => x.Name)), "the oldest left out");
        Equal("crash-1,crash-2,session-1.log", string.Join(",", archive.Included.Select(x => x.Name)), "the rest included");
        Equal("left out: crash-0", Encoding.UTF8.GetString(Unzip(archive.Zip)["report.txt"]), "report.txt names it");
    }

    private static void CollectFindsLogsAndNewCrashes()
    {
        string game = Path.Combine(_temp, "game");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx"));
        File.WriteAllText(Path.Combine(game, "Wingspan.exe"), string.Empty);
        File.WriteAllText(ProblemReport.BepInExLog(game),
            "[Message: Preloader] BepInEx 6.0.0-be.793 - Wingspan (08.11.2026 22:15:44)\n" +
            "[Info   :Wingspan Access] Session log started 2026-11-24 13:09:35: logs\\session-1.log\n" +
            "[Info   :Wingspan Access] [Loc] game language: de, mod strings: de\n" +
            "[Info   :Wingspan Access] Wingspan Access 0.2.4 loaded, speech backend: NVDA\n" +
            " Version: 1.8.1180.1014.1622 \n");
        string logs = ProblemReport.LogsDir(game);
        Directory.CreateDirectory(logs);
        // The same game session as BepInEx's log: left out.
        File.WriteAllText(Path.Combine(logs, "session-1.log"), "Wingspan Access session log, started 2026-11-24 13:09:35\nmore\n");
        File.WriteAllText(Path.Combine(logs, "session-2.log"), "Wingspan Access session log, started 2026-11-23 20:00:00\nmore\n");
        File.WriteAllText(Path.Combine(logs, "session-3.log"), "no header\n");
        string unity = Path.Combine(_temp, "LocalLow", "Monster Couch", "Wingspan");
        Directory.CreateDirectory(unity);
        File.WriteAllText(Path.Combine(unity, "Player.log"), "Initialize engine version: 6000.3.10f1\n");
        File.WriteAllText(Path.Combine(unity, "Player-prev.log"), "Initialize engine version: 6000.3.10f1\n");
        string installerLog = WriteFile("inst.log", "Wingspan Access Installer\n");

        string crashes = Path.Combine(_temp, "Crashes");
        DateTime now = DateTime.UtcNow;
        MakeCrash(crashes, "Crash_OLD", now.AddDays(-3));
        MakeCrash(crashes, "Crash_NEW", now.AddHours(-1));
        MakeCrash(crashes, "Crash_ANCIENT", now.AddDays(-30));
        Directory.CreateDirectory(Path.Combine(crashes, "not-a-crash"));

        List<ReportItem> items = ProblemReport.Collect(game, unity, crashes, installerLog, now.AddDays(-2), now);
        Equal("LogOutput.log,session-2.log,session-3.log,Player.log,Player-prev.log,installer.log,crash-" + Local(now.AddHours(-1)),
            string.Join(",", items.Select(item => item.Name)), "items after the last report");
        True(items.Where(item => !item.IsDump).All(item => item.Selected), "logs chosen");
        True(items.Where(item => item.IsDump).All(item => !item.Selected), "dumps not chosen");
        Equal(2, items.Last().Files.Count, "dump and error.log");

        List<ReportItem> withoutReport = ProblemReport.Collect(game, unity, crashes, null, null, now);
        Equal(2, withoutReport.Count(item => item.IsDump), "the last 14 days without a report");

        // Without BepInEx's log, the latest session log is no longer left out.
        File.Delete(ProblemReport.BepInExLog(game));
        True(ProblemReport.Collect(game, null, null, null, null, now).Any(item => item.Name == "session-1.log"), "session 1 without BepInEx's log");
        File.WriteAllText(ProblemReport.BepInExLog(game), "[Info   :Wingspan Access] Session log started 2026-11-24 13:09:35: logs\\session-1.log\n" +
            "[Info   :Wingspan Access] [Loc] game language: de, mod strings: de\n" +
            "[Info   :Wingspan Access] Wingspan Access 0.2.4 loaded, speech backend: NVDA\n Version: 1.8.1180.1014.1622 \n");

        string system = ReportForm.SystemInfo(game);
        True(system.Contains("Speech: NVDA"), "speech: " + system);
        True(system.Contains("Wingspan: 1.8.1180.1014.1622"), "game version: " + system);
        True(system.Contains("game de, mod de"), "languages: " + system);
        True(system.Contains("BepInEx: not installed"), "BepInEx: " + system);
    }

    private static void LastReportTimeRoundTrips()
    {
        string game = Path.Combine(_temp, "game2");
        Directory.CreateDirectory(game);
        Equal(null, ProblemReport.LastReport(game), "none yet");
        var time = new DateTime(2026, 11, 23, 13, 24, 0, DateTimeKind.Utc);
        ProblemReport.SetLastReport(game, time);
        Equal(time, ProblemReport.LastReport(game), "written and read");
    }

    // ----- Static Forms client (against the stand-in) -----

    private static void ClientSendsReportWithAltcha()
    {
        string challengeJson = MakeChallenge("SHA-256", 4242, 50_000);
        using var server = new MockStaticForms(request =>
            request.Method == "GET" ? (200, challengeJson) : (200, "{\"success\":true,\"message\":\"ok\",\"id\":\"sub_123\"}"));
        using var client = new StaticFormsClient("sf_test_key", server.BaseUrl + "/submit", server.BaseUrl + "/api/altcha/challenge");
        string challenge = client.FetchChallenge(CancellationToken.None);
        string token = Altcha.SolveToPayload(challenge, CancellationToken.None);
        byte[] zip = RandomBytes(3000);
        string id = client.Submit(new List<KeyValuePair<string, string>>
        {
            new("message", "Das Spiel stürzt ab"),
            new("replyTo", null),
            new("altchaToken", token),
        }, "report", "WingspanAccess-report.zip", zip, CancellationToken.None);
        Equal("sub_123", id, "submission id");

        RecordedRequest get = server.Requests[0];
        Equal("GET", get.Method, "challenge method");
        Equal("/api/altcha/challenge?apiKey=sf_test_key", get.Target, "challenge URL with the key");
        Equal("application/json", get.Headers["Accept"], "accept");

        RecordedRequest post = server.Requests[1];
        Equal("POST", post.Method, "submit method");
        Equal("/submit", post.Target, "submit URL");
        True(post.Headers["Content-Type"].StartsWith("multipart/form-data; boundary=", StringComparison.Ordinal), "multipart");
        False(post.Headers.ContainsKey("Expect"), "no 100-continue");
        Equal("sf_test_key", post.Part("apiKey").Text, "apiKey");
        Equal("Das Spiel stürzt ab", post.Part("message").Text, "message in UTF-8");
        Equal(null, post.Part("replyTo"), "null fields left out");
        Equal(null, post.Part("apiKey").ContentType, "text parts without a type, like a browser");
        FormPart file = post.Part("report");
        Equal("WingspanAccess-report.zip", file.FileName, "file name");
        Equal("application/zip", file.ContentType, "file type");
        True(file.Data.SequenceEqual(zip), "file bytes");

        var payload = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(Encoding.UTF8.GetString(Convert.FromBase64String(post.Part("altchaToken").Text)));
        Equal(4242L, Convert.ToInt64(payload["number"]), "solved number sent");
        False(File.ReadAllText(Log.FilePath).Contains("sf_test_key"), "the key is not in the log");
    }

    private static void ClientMapsErrors()
    {
        var cases = new (int Status, string Body, StaticFormsFailure Failure, string Message)[]
        {
            (400, "{\"error\":\"Missing apiKey\"}", StaticFormsFailure.Rejected, "Missing apiKey"),
            (401, "{\"error\":\"Invalid API key\"}", StaticFormsFailure.InvalidKey, "Invalid API key"),
            (403, "{\"error\":\"Captcha verification failed\"}", StaticFormsFailure.Forbidden, "Captcha verification failed"),
            (413, "{\"error\":{\"code\":\"413\",\"message\":\"Request Entity Too Large\"}}", StaticFormsFailure.TooLarge, "Request Entity Too Large"),
            (429, "{\"error\":\"Too many requests\"}", StaticFormsFailure.RateLimited, "Too many requests"),
            (500, "<html>oops</html>", StaticFormsFailure.Server, "<html>oops</html>"),
            (200, "{\"success\":false,\"error\":\"Form paused\"}", StaticFormsFailure.Rejected, "Form paused"),
        };
        foreach (var c in cases)
        {
            using var server = new MockStaticForms(_ => (c.Status, c.Body));
            using var client = new StaticFormsClient("k", server.BaseUrl + "/submit", server.BaseUrl + "/c");
            var e = Throws<StaticFormsException>(() => client.Submit(new List<KeyValuePair<string, string>>(), "report", "r.zip", new byte[1], CancellationToken.None), c.Status.ToString());
            Equal(c.Failure, e.Failure, $"{c.Status} failure");
            Equal(c.Message, e.ServerMessage, $"{c.Status} message");
        }
    }

    private static void ClientReportsNoConnection()
    {
        // A port nobody listens on.
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var client = new StaticFormsClient("k", $"http://127.0.0.1:{port}/submit", $"http://127.0.0.1:{port}/c", TimeSpan.FromSeconds(10));
        var e = Throws<StaticFormsException>(() => client.FetchChallenge(CancellationToken.None), "refused");
        Equal(StaticFormsFailure.Network, e.Failure, "network failure");
    }

    // ----- The built-in key -----

    /// <summary>Built and run in the same environment: the key comes back as WA_STATICFORMS_KEY has it (never printed).</summary>
    private static void ReportKeyMatchesTheBuild()
    {
        string env = Environment.GetEnvironmentVariable("WA_STATICFORMS_KEY")?.Trim();
        if (string.IsNullOrEmpty(env))
        {
            False(ReportKey.Available, "no WA_STATICFORMS_KEY now, but the build has a key (built in another environment?)");
            return;
        }

        True(ReportKey.Available, "WA_STATICFORMS_KEY is set, but the build has no key (built in another environment?)");
        True(env == ReportKey.Value, "the built-in key differs from WA_STATICFORMS_KEY");
        byte[] exe = File.ReadAllBytes(typeof(ReportKey).Assembly.Location);
        string asLatin1 = Encoding.GetEncoding(28591).GetString(exe);
        False(asLatin1.Contains(env) || Encoding.Unicode.GetString(exe).Contains(env), "the key is readable as plain text in the EXE");
    }

    // ----- The windows, built but not shown -----

    private static void ReportWindowIsBuiltForScreenReaders()
    {
        using var form = new ReportForm(null);
        List<System.Windows.Forms.Control> controls = AllControls(form).ToList();
        var inputs = controls.Where(c => c is System.Windows.Forms.TextBox || c is System.Windows.Forms.CheckedListBox).ToList();
        Equal(4, inputs.Count, "description, e-mail, attachments, system information");
        foreach (System.Windows.Forms.Control input in inputs)
        {
            True(!string.IsNullOrEmpty(input.AccessibleName), $"{input.GetType().Name} has a name");
            // The label right before it: Alt+its letter goes to the field, and screen readers pair them.
            System.Windows.Forms.Control before = input.Parent.Controls.Cast<System.Windows.Forms.Control>().FirstOrDefault(c => c.TabIndex == input.TabIndex - 1);
            True(before is System.Windows.Forms.Label label && label.Text.Replace("&", string.Empty) == input.AccessibleName, $"label before {input.AccessibleName}");
        }

        var buttons = controls.OfType<System.Windows.Forms.Button>().ToList();
        System.Windows.Forms.Button send = buttons.Single(b => b.Text == Strings.Get("report_send"));
        False(send.Enabled, "Send waits until the attachments are listed");
        True(buttons.Any(b => b.Text == Strings.Get("cancel")), "a Cancel button");
        Equal(System.Windows.Forms.AccessibleRole.Dialog, form.AccessibleRole, "dialog role, so screen readers read its texts");
        True(controls.OfType<System.Windows.Forms.Label>().Any(l => l.Text == Strings.Get("report_privacy")), "the privacy note is a visible text");
    }

    private static void MainWindowReportButtonFollowsTheKey()
    {
        using var form = new MainForm(Options.Parse(new string[0]));
        List<System.Windows.Forms.Control> controls = AllControls(form).ToList();
        System.Windows.Forms.Button report = controls.OfType<System.Windows.Forms.Button>().Single(b => b.Text == Strings.Get("report_button"));
        Equal(ReportKey.Available, report.Enabled, "the report button is on only with a key");
        True(controls.OfType<System.Windows.Forms.Label>().Any(l => l.Text == Strings.Get("report_unavailable")), "the reason is there as a text");
    }

    private static IEnumerable<System.Windows.Forms.Control> AllControls(System.Windows.Forms.Control parent)
    {
        foreach (System.Windows.Forms.Control child in parent.Controls)
        {
            yield return child;
            foreach (System.Windows.Forms.Control grandchild in AllControls(child))
                yield return grandchild;
        }
    }

    // ----- Options and texts -----

    private static void OptionsKnowReport()
    {
        Options options = Options.Parse(new[] { "--report", "--game", @"C:\Games\Wingspan" });
        True(options.Report, "report");
        Equal(0, options.Unknown.Count, "nothing unknown");
        List<string> args = options.ToArguments(null, "--relaunched");
        True(args.Contains("--report") && args.Contains("--relaunched"), "passed on: " + string.Join(" ", args));
    }

    private static void ReportTextAndEmailCheck()
    {
        True(ReportForm.LooksLikeEmail("a.b@example.org"), "good address");
        False(ReportForm.LooksLikeEmail("no address"), "no @");
        False(ReportForm.LooksLikeEmail("a@b"), "no dot");
        var archive = new ReportArchive { Shortened = true };
        archive.Included.Add(new ReportItem { Name = "session-1.log" });
        archive.Included.Add(new ReportItem { Name = "crash-x", IsDump = true });
        archive.LeftOut.Add(new ReportItem { Name = "session-3.log" });
        string text = ReportForm.ReportText("Es hängt.", string.Empty, "Wingspan Access: dev", archive, new DateTime(2026, 11, 23, 14, 0, 0));
        True(text.Contains("What happened:" + Environment.NewLine + "Es hängt."), "description");
        True(text.Contains("E-mail for questions: none"), "no e-mail");
        True(text.Contains("- crash-x (crash dump)"), "dump named");
        True(text.Contains("Left out to stay under the size limit: session-3.log"), "left out named");
        True(text.Contains("shortened in the middle"), "shortened noted");
    }

    // ----- helpers -----

    private static void MakeCrash(string crashes, string name, DateTime utc)
    {
        string dir = Path.Combine(crashes, name);
        Directory.CreateDirectory(dir);
        string dump = Path.Combine(dir, "crash.dmp");
        File.WriteAllBytes(dump, RandomBytes(1000));
        File.SetLastWriteTimeUtc(dump, utc);
        File.WriteAllText(Path.Combine(dir, "error.log"), "Wingspan.exe caused an Access Violation\n");
    }

    private static string Local(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);

    private static Dictionary<string, byte[]> Unzip(byte[] zip)
    {
        var entries = new Dictionary<string, byte[]>();
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            using Stream stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            entries[entry.FullName] = memory.ToArray();
        }

        return entries;
    }

    private static string WriteFile(string name, string text)
    {
        string path = Path.Combine(_temp, name);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    private static string WriteBytes(string name, byte[] data)
    {
        string path = Path.Combine(_temp, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static byte[] RandomBytes(int count)
    {
        var data = new byte[count];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(data);
        return data;
    }

    private static string RandomHex(int bytes, Random random = null)
    {
        var data = new byte[bytes];
        if (random != null)
            random.NextBytes(data);
        else
            data = RandomBytes(bytes);
        return string.Concat(data.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static void True(bool condition, string what)
    {
        if (!condition)
            throw new Exception("expected true: " + what);
    }

    private static void False(bool condition, string what) => True(!condition, what);

    private static void Equal(object expected, object actual, string what)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{what}: expected <{expected}>, got <{actual}>");
    }

    private static T Throws<T>(Action action, string what) where T : Exception
    {
        try
        {
            action();
        }
        catch (T e)
        {
            return e;
        }
        catch (AggregateException e) when (e.InnerException is T inner)
        {
            return inner;
        }

        throw new Exception($"{what}: expected {typeof(T).Name}");
    }
}
