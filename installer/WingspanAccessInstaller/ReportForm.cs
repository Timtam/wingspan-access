using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Automation;
using Microsoft.Win32;

namespace WingspanAccessInstaller;

/// <summary>
/// "Report a problem": a description, an e-mail address for questions and the logs and crash
/// dumps to attach, sent to the developer's Static Forms form. Standard controls only, so screen
/// readers read it on their own; progress goes to a live region. Opened with "--report" (the mod
/// starts it with F4 twice, the Start menu entry too) and from the installer's window.
/// </summary>
internal sealed class ReportForm : Form
{
    private const string FileField = "report";

    private readonly string _gameDir;
    private readonly Scrubber _scrubber = Scrubber.ForThisComputer();
    private readonly CancellationTokenSource _cancel = new();

    private readonly Label _introLabel = NewLabel();
    private readonly Label _descriptionLabel = NewLabel();
    private readonly TextBox _descriptionBox = new()
    {
        Multiline = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        MinimumSize = new Size(0, 90),
        MaxLength = 20000,
    };
    private readonly Label _emailLabel = NewLabel();
    private readonly TextBox _emailBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, MaxLength = 200 };
    private readonly Label _filesLabel = NewLabel();
    private readonly CheckedListBox _filesList = new()
    {
        CheckOnClick = true,
        IntegralHeight = false,
        Height = 110,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        HorizontalScrollbar = true,
    };
    private readonly Label _privacyLabel = NewLabel();
    private readonly Label _systemLabel = NewLabel();
    private readonly TextBox _systemBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Height = 100,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
    };
    private readonly Label _statusLabel = NewLabel();
    private readonly ProgressBar _progressBar = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0 };
    private readonly Button _sendButton = NewButton();
    private readonly Button _closeButton = NewButton();

    private List<ReportItem> _items = new();
    private string _systemInfo = string.Empty;
    private bool _busy;
    private bool _sent;

    public ReportForm(string gameDir)
    {
        _gameDir = GameInstall.IsGameFolder(gameDir) ? gameDir : null;
        BuildLayout();
        _sendButton.Click += async (_, _) => await SendAsync();
        _closeButton.Click += (_, _) => Close();
        FormClosing += OnFormClosing;
    }

    /// <summary>"--report": the window, or a message box when this build has no form key.</summary>
    public static int Run(Options options)
    {
        if (!ReportKey.Available)
        {
            MessageBox.Show(Strings.Get("report_unavailable"), Strings.Get("report_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        Application.Run(new ReportForm(GameInstall.Locate(options.GameDir)) { _returnToGame = true });
        return 0;
    }

    /// <summary>Opened on its own (F4 twice in the game): closing it brings the game back to the front.</summary>
    private bool _returnToGame;

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        // While this window still has the foreground, Windows lets it hand it on; afterwards the
        // focus would go to whatever window is next, often not the game.
        if (_returnToGame && _gameDir != null)
            GameInstall.ActivateGame(_gameDir);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        _descriptionBox.Focus();
        _ = LoadAsync();
    }

    private void BuildLayout()
    {
        SuspendLayout();
        Text = Strings.Get("report_title");
        // Screen readers read the texts of a dialog when it opens.
        AccessibleRole = AccessibleRole.Dialog;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        ClientSize = new Size(640, 640);
        MinimumSize = new Size(500, 560);
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        _introLabel.Text = Strings.Get("report_intro");
        _descriptionLabel.Text = Strings.Get("report_description");
        _descriptionBox.AccessibleName = Plain(_descriptionLabel.Text);
        _emailLabel.Text = Strings.Get("report_email");
        _emailBox.AccessibleName = Plain(_emailLabel.Text);
        _filesLabel.Text = Strings.Get("report_files");
        _filesList.AccessibleName = Plain(_filesLabel.Text);
        _privacyLabel.Text = Strings.Get("report_privacy");
        _systemLabel.Text = Strings.Get("report_system");
        _systemBox.AccessibleName = Plain(_systemLabel.Text);
        _statusLabel.LiveSetting = AutomationLiveSetting.Polite;
        _statusLabel.Text = Strings.Get("report_collecting");
        _sendButton.Text = Strings.Get("report_send");
        _sendButton.Enabled = false;
        _closeButton.Text = Strings.Get("cancel");

        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 6, 0, 0) };
        buttons.Controls.AddRange(new Control[] { _sendButton, _closeButton });

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Each label right before its control: the order screen readers and Tab follow.
        Control[] rows =
        {
            _introLabel, _descriptionLabel, _descriptionBox, _emailLabel, _emailBox, _filesLabel, _filesList, _privacyLabel,
            _systemLabel, _systemBox, _statusLabel, _progressBar, buttons,
        };
        foreach (Control row in rows)
        {
            layout.RowStyles.Add(row == _descriptionBox ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(row, 0, layout.RowCount++);
        }

        Controls.Add(layout);
        CancelButton = _closeButton;
        ResumeLayout();
    }

    /// <summary>Finds the logs and crash reports on a worker thread and lists them.</summary>
    private async Task LoadAsync()
    {
        string gameDir = _gameDir;
        try
        {
            (List<ReportItem> items, string system) = await Task.Run(() =>
            {
                DateTime? last = gameDir == null ? null : ProblemReport.LastReport(gameDir);
                List<ReportItem> found = ProblemReport.Collect(gameDir, ProblemReport.UnityLogDir(), ProblemReport.CrashesDir(), Log.FilePath, last, DateTime.UtcNow);
                return (found, _scrubber.Apply(SystemInfo(gameDir)));
            });
            if (IsDisposed)
                return;
            _items = items;
            _systemInfo = system;
            _filesList.BeginUpdate();
            foreach (ReportItem item in items)
                _filesList.Items.Add(item, item.Selected);
            _filesList.EndUpdate();
            _systemBox.Text = system;
            var status = new List<string>();
            if (gameDir == null)
                status.Add(Strings.Get("report_no_game"));
            status.Add(items.Count == 0 ? Strings.Get("report_nothing_found") : Strings.Get("report_found", items.Count));
            if (items.Any(item => item.IsDump))
                status.Add(Strings.Get("report_dumps_found", items.Count(item => item.IsDump)));
            SetStatus(string.Join(" ", status));
            _sendButton.Enabled = true;
        }
        catch (Exception e)
        {
            Log.Error(e);
            if (IsDisposed)
                return;
            SetStatus(Strings.Get("report_collect_failed", e.Message));
            _sendButton.Enabled = true;
        }
    }

    private async Task SendAsync()
    {
        if (_busy)
            return;
        string description = _descriptionBox.Text.Trim();
        if (description.Length == 0)
        {
            ShowMessage(Strings.Get("report_need_description"), MessageBoxIcon.Warning);
            _descriptionBox.Focus();
            return;
        }

        string email = _emailBox.Text.Trim();
        if (email.Length > 0 && !LooksLikeEmail(email))
        {
            ShowMessage(Strings.Get("report_bad_email"), MessageBoxIcon.Warning);
            _emailBox.Focus();
            _emailBox.SelectAll();
            return;
        }

        List<ReportItem> chosen = _filesList.CheckedItems.Cast<ReportItem>().ToList();
        SetBusy(true);
        try
        {
            SetStatus(Strings.Get("report_packing"));
            CancellationToken token = _cancel.Token;
            string system = _systemInfo;
            DateTime now = DateTime.Now;
            ReportArchive archive = await Task.Run(() =>
                ProblemReport.Build(chosen, a => ReportText(description, email, system, a, now), _scrubber, ProblemReport.ZipBudget, now), token);
            token.ThrowIfCancellationRequested();
            if (archive.LeftOut.Count > 0)
                Log.Info(Strings.Get("report_left_out", string.Join(", ", archive.LeftOut.Select(item => item.Name))));

            using var client = new StaticFormsClient(ReportKey.Value);
            SetStatus(Strings.Get("report_protecting"));
            string altcha = await Task.Run(() => SolveAltcha(client, token), token);

            SetStatus(Strings.Get("report_sending", ProblemReport.Size(archive.Zip.Length)));
            List<KeyValuePair<string, string>> fields = Fields(description, email, system, archive, altcha);
            string id = await Task.Run(() => client.Submit(fields, FileField, archive.FileName, archive.Zip, token), token);
            Log.Detail($"Report sent{(id == null ? string.Empty : ", ID " + id)}");

            if (_gameDir != null)
                ProblemReport.SetLastReport(_gameDir, DateTime.UtcNow);
            _sent = true;
            SetBusy(false);
            string done = Strings.Get("report_sent");
            if (archive.LeftOut.Count > 0)
                done += Environment.NewLine + Environment.NewLine + Strings.Get("report_left_out", string.Join(", ", archive.LeftOut.Select(item => item.Name)));
            SetStatus(Strings.Get("report_sent"));
            ShowMessage(done, MessageBoxIcon.Information);
            Close();
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
        }
        catch (StaticFormsException e)
        {
            Log.Detail(e.Message);
            Fail(FailureText(e));
        }
        catch (AltchaException e)
        {
            Log.Detail(e.Message);
            Fail(Strings.Get("report_altcha_failed", e.Message));
        }
        catch (Exception e)
        {
            Log.Error(e);
            Fail(Strings.Get("error", e.Message, Log.FilePath));
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// The solved ALTCHA challenge, or null if the form gives none (then it has no ALTCHA, and the
    /// submission tells whether one was needed after all).
    /// </summary>
    private static string SolveAltcha(StaticFormsClient client, CancellationToken token)
    {
        string challenge;
        try
        {
            challenge = client.FetchChallenge(token);
        }
        catch (StaticFormsException e) when (e.Failure != StaticFormsFailure.Network && e.Failure != StaticFormsFailure.RateLimited)
        {
            Log.Detail($"No ALTCHA challenge ({e.Message}); sending without");
            return null;
        }

        AltchaChallenge parsed = Altcha.Parse(challenge);
        AltchaSolution solution = Altcha.Solve(parsed, token);
        Log.Detail($"ALTCHA {parsed.Algorithm} solved in {solution.TookMs} ms (max {parsed.MaxNumber})");
        return Altcha.Payload(parsed, solution);
    }

    private static List<KeyValuePair<string, string>> Fields(string description, string email, string system, ReportArchive archive, string altcha)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("subject", $"Wingspan Access problem report ({BuildInfo.Version})"),
            new("message", description),
            new("replyTo", email.Length > 0 ? email : null),
            new("system", system),
            new("attachments", AttachmentList(archive)),
            new("altchaToken", altcha),
        };
        return fields;
    }

    /// <summary>report.txt in the ZIP: everything of the report in one file.</summary>
    internal static string ReportText(string description, string email, string system, ReportArchive archive, DateTime now)
    {
        var text = new StringBuilder();
        text.AppendLine("Wingspan Access problem report");
        text.AppendLine($"Written: {now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} (UTC{now:zzz})");
        text.AppendLine();
        text.AppendLine("What happened:");
        text.AppendLine(description);
        text.AppendLine();
        text.AppendLine("E-mail for questions: " + (email.Length > 0 ? email : "none"));
        text.AppendLine();
        text.AppendLine("System:");
        text.AppendLine(system);
        text.AppendLine();
        text.AppendLine("Attachments:");
        text.AppendLine(AttachmentList(archive));
        return text.ToString();
    }

    private static string AttachmentList(ReportArchive archive)
    {
        var lines = new List<string>();
        lines.AddRange(archive.Included.Select(item => "- " + item.Name + (item.IsDump ? " (crash dump)" : string.Empty)));
        if (lines.Count == 0)
            lines.Add("none");
        if (archive.LeftOut.Count > 0)
            lines.Add("Left out to stay under the size limit: " + string.Join(", ", archive.LeftOut.Select(item => item.Name)));
        if (archive.Shortened)
            lines.Add("Long logs were shortened in the middle (their start and end are there).");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>The versions and settings that help to find a problem; English, for the developer.</summary>
    internal static string SystemInfo(string gameDir)
    {
        var lines = new List<string>();
        bool found = GameInstall.IsGameFolder(gameDir);
        string installed = found ? GameInstall.InstalledModVersion(gameDir) : null;
        lines.Add($"Wingspan Access: {installed ?? "not installed"}, installer {BuildInfo.Version}");
        // The start of BepInEx's log of the current or last game session, else the latest session log.
        string firstLogs = found ? FirstLines(ProblemReport.BepInExLog(gameDir)) : string.Empty;
        string session = found ? FirstLines(Path.Combine(ProblemReport.LogsDir(gameDir), "session-1.log")) : string.Empty;
        string logs = firstLogs + "\n" + session;
        string bepInEx = found ? GameInstall.BepInExVersion(gameDir) : null;
        lines.Add("BepInEx: " + (bepInEx ?? "not installed"));
        string game = Regex.Match(logs, @"(?m)^\s*Version: (\d+(?:\.\d+)+)\s*$").Groups[1].Value;
        if (game.Length > 0)
            lines.Add("Wingspan: " + game);
        string speech = Regex.Match(logs, @"speech backend: ([^\r\n]+)").Groups[1].Value;
        if (speech.Length > 0)
            lines.Add("Speech: " + speech.Trim());
        Match language = Regex.Match(logs, @"game language: ([^,\s]+), mod strings: (\S+)");
        lines.Add($"Languages: Windows {CultureInfo.CurrentUICulture.Name}, " +
                  (language.Success ? $"game {language.Groups[1].Value}, mod {language.Groups[2].Value}" : "game unknown"));
        lines.Add("Windows: " + WindowsVersion());
        lines.Add("Game folder: " + (gameDir ?? "not found"));
        if (found)
            lines.Add("Game running: " + (GameInstall.IsGameRunning(gameDir) ? "yes" : "no"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FirstLines(string path)
    {
        try
        {
            if (!File.Exists(path))
                return string.Empty;
            LogText text = ProblemReport.ReadText(path, 64 * 1024, 0);
            return text.Head;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string WindowsVersion()
    {
        try
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string product = key?.GetValue("ProductName") as string ?? "Windows";
            string display = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string;
            string build = key?.GetValue("CurrentBuild") as string;
            object ubr = key?.GetValue("UBR");
            // Windows 11 still says "Windows 10" in ProductName.
            if (int.TryParse(build, out int number) && number >= 22000)
                product = product.Replace("Windows 10", "Windows 11");
            return $"{product}{(display != null ? " " + display : string.Empty)} (build {build}{(ubr != null ? "." + ubr : string.Empty)}), " +
                   (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");
        }
        catch (Exception e) when (e is System.Security.SecurityException || e is UnauthorizedAccessException || e is IOException)
        {
            return Environment.OSVersion.VersionString;
        }
    }

    internal static bool LooksLikeEmail(string text) => Regex.IsMatch(text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    private static string FailureText(StaticFormsException e) => e.Failure switch
    {
        StaticFormsFailure.Network => Strings.Get("report_failed_network", e.ServerMessage),
        StaticFormsFailure.Rejected => Strings.Get("report_failed_rejected", e.ServerMessage),
        StaticFormsFailure.InvalidKey => Strings.Get("report_failed_key", e.ServerMessage),
        StaticFormsFailure.Forbidden => Strings.Get("report_failed_forbidden", e.ServerMessage),
        StaticFormsFailure.TooLarge => Strings.Get("report_failed_too_large"),
        StaticFormsFailure.RateLimited => Strings.Get("report_failed_rate"),
        _ => Strings.Get("report_failed_server", e.Status, e.ServerMessage),
    };

    private void Fail(string message)
    {
        if (IsDisposed)
            return;
        SetBusy(false);
        SetStatus(message);
        ShowMessage(message, MessageBoxIcon.Error);
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_sent && !_busy && _descriptionBox.Text.Trim().Length > 0 &&
            MessageBox.Show(this, Strings.Get("report_discard"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            e.Cancel = true;
            _descriptionBox.Focus();
            return;
        }

        _cancel.Cancel();
    }

    private void SetBusy(bool busy)
    {
        if (IsDisposed)
            return;
        _busy = busy;
        // A focused control that is about to be disabled: keep the focus on the Cancel button.
        if (busy && ActiveControl != _closeButton)
            _closeButton.Focus();
        _descriptionBox.ReadOnly = busy;
        _emailBox.ReadOnly = busy;
        _filesList.Enabled = !busy;
        _sendButton.Enabled = !busy;
        _progressBar.MarqueeAnimationSpeed = busy ? 30 : 0;
    }

    private void SetStatus(string message)
    {
        if (IsDisposed)
            return;
        Log.Detail(message);
        _statusLabel.Text = message;
        // Screen readers announce changes of a live region.
        _statusLabel.AccessibilityObject.RaiseLiveRegionChanged();
    }

    private void ShowMessage(string text, MessageBoxIcon icon) => MessageBox.Show(this, text, Text, MessageBoxButtons.OK, icon);

    private static Label NewLabel() => new() { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 4, 3, 4) };

    private static Button NewButton() => new() { AutoSize = true, MinimumSize = new Size(90, 28), Padding = new Padding(6, 0, 6, 0) };

    private static string Plain(string text) => text.Replace("&", string.Empty);
}
