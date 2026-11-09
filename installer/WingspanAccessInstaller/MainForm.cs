using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Automation;

namespace WingspanAccessInstaller;

/// <summary>
/// The installer window, also used for updates the mod starts ("--update"). Standard controls
/// only, so screen readers know them. Progress goes to a live region that screen readers announce;
/// results and questions come as message boxes.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly Options _options;
    private readonly CancellationTokenSource _cancel = new();

    private readonly Label _introLabel = NewLabel();
    private readonly Label _gameLabel = NewLabel();
    private readonly TextBox _gameBox = new() { ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _browseButton = NewButton();
    private readonly Label _installedLabel = NewLabel();
    private readonly Label _latestLabel = NewLabel();
    private readonly Label _bepinexLabel = NewLabel();
    private readonly Label _statusLabel = NewLabel();
    private readonly ProgressBar _progressBar = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, Maximum = 100 };
    private readonly Label _logLabel = NewLabel();
    private readonly TextBox _logBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly CheckBox _removeBepInExBox = new() { AutoSize = true };
    private readonly Button _installButton = NewButton();
    private readonly Button _uninstallButton = NewButton();
    private readonly Button _closeGameButton = NewButton();
    private readonly Button _closeButton = NewButton();

    private string _gameDir;
    private ReleaseInfo _release;
    private string _releaseError;
    private bool _releaseChecked;
    private Task _releaseCheck = Task.CompletedTask;
    private bool _busy;
    private volatile bool _installing;

    public MainForm(Options options)
    {
        _options = options;
        BuildLayout();
        Log.Progress += OnProgress;
        FormClosed += (_, _) => Log.Progress -= OnProgress;
        FormClosing += OnFormClosing;
        _browseButton.Click += (_, _) => ChooseGameFolder();
        _installButton.Click += async (_, _) => await InstallAsync();
        _uninstallButton.Click += async (_, _) => await UninstallAsync();
        _closeGameButton.Click += (_, _) => GameInstall.CloseGame(_gameDir);
        _closeButton.Click += (_, _) => Close();
    }

    private enum UpdateOutcome
    {
        Installed,
        UpToDate,
        Failed,
        HandedOver,
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        _gameDir = GameInstall.Locate(_options.GameDir);
        RefreshState();
        if (_options.Update)
        {
            _closeButton.Focus();
            _ = RunUpdateAsync();
            return;
        }

        if (GameInstall.IsGameFolder(_gameDir))
        {
            _installButton.Focus();
        }
        else
        {
            _browseButton.Focus();
            Log.Info(Strings.Get("game_missing"));
        }

        _releaseCheck = CheckLatestAsync();
    }

    private void BuildLayout()
    {
        SuspendLayout();
        Text = $"{Strings.Get("title")} {BuildInfo.Version}";
        // Screen readers read the texts of a dialog when it opens.
        AccessibleRole = AccessibleRole.Dialog;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        ClientSize = new Size(620, 520);
        MinimumSize = new Size(480, 400);
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        bool update = _options.Update;
        _introLabel.Text = Strings.Get("intro");
        _gameLabel.Text = Strings.Get("game_folder");
        _gameBox.AccessibleName = Plain(Strings.Get("game_folder"));
        _browseButton.Text = Strings.Get("change");
        _statusLabel.LiveSetting = AutomationLiveSetting.Polite;
        _logLabel.Text = Strings.Get("log");
        _logBox.AccessibleName = Plain(Strings.Get("log"));
        _removeBepInExBox.Text = Strings.Get("remove_bepinex");
        _installButton.Text = Strings.Get("install");
        _uninstallButton.Text = Strings.Get("uninstall");
        _closeGameButton.Text = Strings.Get("close_game");
        _closeButton.Text = Strings.Get(update ? "cancel" : "close");

        _introLabel.Visible = !update;
        _browseButton.Visible = !update;
        _removeBepInExBox.Visible = !update;
        _installButton.Visible = !update;
        _uninstallButton.Visible = !update;
        _closeGameButton.Visible = update;
        _closeGameButton.Enabled = false;

        // The label right before its text box, so screen readers do not read the name twice.
        var gamePanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0),
        };
        gamePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gamePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gamePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gamePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gamePanel.Controls.Add(_gameLabel, 0, 0);
        gamePanel.SetColumnSpan(_gameLabel, 2);
        gamePanel.Controls.Add(_gameBox, 0, 1);
        gamePanel.Controls.Add(_browseButton, 1, 1);

        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 6, 0, 0) };
        buttons.Controls.AddRange(new Control[] { _installButton, _uninstallButton, _closeGameButton, _closeButton });

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Control[] rows = { _introLabel, gamePanel, _installedLabel, _latestLabel, _bepinexLabel, _statusLabel, _progressBar, _logLabel, _logBox, _removeBepInExBox, buttons };
        foreach (Control row in rows)
        {
            layout.RowStyles.Add(row == _logBox ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(row, 0, layout.RowCount++);
        }

        Controls.Add(layout);
        AcceptButton = update ? null : _installButton;
        CancelButton = _closeButton;
        ResumeLayout();
    }

    private void RefreshState()
    {
        if (IsDisposed)
            return;
        bool found = GameInstall.IsGameFolder(_gameDir);
        string installed = found ? GameInstall.InstalledModVersion(_gameDir) : null;
        string bepinex = found ? GameInstall.BepInExVersion(_gameDir) : null;
        Version installedVersion = Releases.ParseVersion(installed);
        bool newer = _release?.Version != null && installedVersion != null && _release.Version > installedVersion;

        _gameBox.Text = _gameDir ?? string.Empty;
        _installedLabel.Text = installed == null ? Strings.Get("not_installed") : Strings.Get("installed", installed);
        _latestLabel.Text = LatestText();
        _bepinexLabel.Text = bepinex == null ? Strings.Get("bepinex_missing") : Strings.Get("bepinex", bepinex);
        _installButton.Text = installed == null ? Strings.Get("install")
            : newer ? Strings.Get("update", _release.VersionText)
            : Strings.Get("reinstall");
        _installButton.Enabled = found && !_busy;
        _uninstallButton.Enabled = found && !_busy && (installed != null || bepinex != null);
        _removeBepInExBox.Enabled = !_busy && bepinex != null;
        _browseButton.Enabled = !_busy;
    }

    private string LatestText()
    {
        if (string.IsNullOrEmpty(Program.Repository))
            return Strings.Get("latest_unknown", Strings.Get("no_repo"));
        if (_releaseError != null)
            return Strings.Get("latest_unknown", Strings.Get("check_failed", _releaseError));
        if (!_releaseChecked)
            return Strings.Get("latest_checking");
        return _release == null ? Strings.Get("latest_none") : Strings.Get("latest", _release.VersionText);
    }

    private async Task CheckLatestAsync()
    {
        if (string.IsNullOrEmpty(Program.Repository))
            return;
        try
        {
            _release = await Task.Run(() => Releases.Latest(Program.Repository));
            _releaseError = null;
        }
        catch (Exception e) when (Releases.IsCheckFailure(e))
        {
            Log.Error(e);
            _releaseError = e.Message;
        }

        _releaseChecked = true;
        if (IsDisposed)
            return;
        RefreshState();
        Log.Info(_latestLabel.Text);
    }

    private async Task InstallAsync()
    {
        if (_busy || !EnsureGameClosed())
            return;
        SetBusy(true);
        try
        {
            await _releaseCheck;
            if (Program.HasNewerInstaller(_release, _options) && Confirm(Strings.Get("new_installer", _release.VersionText)))
            {
                await Task.Run(() => Program.StartNewerInstaller(_release, _options, _gameDir, ReportProgress, _cancel.Token));
                Close();
                return;
            }

            ReleaseInfo release = _release;
            string version = await Task.Run(() =>
            {
                using Package package = PackageSource.Get(_options.ZipPath, release, ReportProgress, _cancel.Token);
                return InstallPackage(package.Zip);
            });
            SetBusy(false);
            ShowInfo(Strings.Get("done_install", version));
            _closeButton.Focus();
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            HandleError(e);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task UninstallAsync()
    {
        if (_busy || !Confirm(Strings.Get("confirm_uninstall", _gameDir)) || !EnsureGameClosed())
            return;
        bool removeBepInEx = _removeBepInExBox.Checked;
        SetBusy(true);
        try
        {
            await Task.Run(() => GameInstall.Uninstall(_gameDir, removeBepInEx, !_options.NoRegistry));
            SetBusy(false);
            ShowInfo(Strings.Get(removeBepInEx ? "done_uninstall_bepinex" : "done_uninstall"));
            _closeButton.Focus();
        }
        catch (Exception e)
        {
            HandleError(e);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>The update the mod started: download, wait for the game to close, install, start it again.</summary>
    private async Task RunUpdateAsync()
    {
        UpdateOutcome outcome = await TryUpdateAsync();
        if (IsDisposed || outcome == UpdateOutcome.HandedOver)
            return;

        // The mod closes the game right after starting the installer; give it time to end.
        for (int i = 0; i < 20 && GameInstall.IsGameRunning(_gameDir); i++)
            await Task.Delay(500);
        if (IsDisposed)
            return;

        bool start = GameInstall.IsGameFolder(_gameDir) && !GameInstall.IsGameRunning(_gameDir) && (outcome switch
        {
            UpdateOutcome.Installed => _options.Restart || Confirm(Strings.Get("start_game_question")),
            UpdateOutcome.UpToDate => _options.Restart || Confirm(Strings.Get("start_game_now")),
            _ => Confirm(Strings.Get("start_game_now")),
        });
        if (start)
        {
            Program.StartGame(_gameDir);
            // Time for the screen reader to say so.
            await Task.Delay(1500);
        }

        if (!IsDisposed)
            Close();
    }

    private async Task<UpdateOutcome> TryUpdateAsync()
    {
        SetBusy(true);
        try
        {
            if (!GameInstall.IsGameFolder(_gameDir))
                throw new InstallerException(Strings.Get("game_missing"));

            if (string.IsNullOrEmpty(_options.ZipPath))
            {
                if (string.IsNullOrEmpty(Program.Repository))
                    throw new InstallerException(Strings.Get("latest_unknown", Strings.Get("no_repo")));
                Log.Info(Strings.Get("checking"));
                _release = await Task.Run(() => Releases.Latest(Program.Repository));
                _releaseChecked = true;
                RefreshState();
                if (_release?.Package == null)
                    throw new InstallerException(Strings.Get("no_package", _release?.PageUrl ?? Strings.Get("latest_none")));

                string installedText = GameInstall.InstalledModVersion(_gameDir);
                Version installed = Releases.ParseVersion(installedText);
                if (installed != null && _release.Version != null && _release.Version <= installed)
                {
                    SetBusy(false);
                    ShowInfo(Strings.Get("up_to_date", installedText));
                    return UpdateOutcome.UpToDate;
                }

                if (Program.HasNewerInstaller(_release, _options))
                {
                    await Task.Run(() => Program.StartNewerInstaller(_release, _options, _gameDir, ReportProgress, _cancel.Token));
                    Close();
                    return UpdateOutcome.HandedOver;
                }
            }

            ReleaseInfo release = _release;
            using Package package = await Task.Run(() => PackageSource.Get(_options.ZipPath, release, ReportProgress, _cancel.Token));
            if (GameInstall.IsGameRunning(_gameDir))
            {
                Log.Info(Strings.Get("update_waiting"));
                _closeGameButton.Enabled = true;
                _closeGameButton.Focus();
                while (GameInstall.IsGameRunning(_gameDir))
                    await Task.Delay(500, _cancel.Token);
                _closeButton.Focus();
                _closeGameButton.Enabled = false;
            }

            string version = await Task.Run(() => InstallPackage(package.Zip));
            Log.Info(Strings.Get("installed_short", version));
            return UpdateOutcome.Installed;
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
            Log.Info(Strings.Get("canceled"));
            return UpdateOutcome.Failed;
        }
        catch (Exception e)
        {
            return HandleError(e) ? UpdateOutcome.HandedOver : UpdateOutcome.Failed;
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Runs on a worker thread; questions go to the window's thread.</summary>
    private string InstallPackage(string zip)
    {
        _installing = true;
        try
        {
            return GameInstall.Install(new InstallRequest
            {
                GameDir = _gameDir,
                PackageZip = zip,
                WriteRegistry = !_options.NoRegistry,
                ReplaceOtherBepInEx = (installed, packaged) =>
                    (bool)Invoke(new Func<bool>(() => Confirm(Strings.Get("other_bepinex", installed, packaged)))),
            });
        }
        finally
        {
            _installing = false;
        }
    }

    private void ChooseGameFolder()
    {
        string start = GameInstall.IsGameFolder(_gameDir)
            ? _gameDir
            : SteamLocator.Libraries().Select(library => Path.Combine(library, "steamapps", "common")).FirstOrDefault(Directory.Exists);
        using var dialog = new OpenFileDialog
        {
            Title = Strings.Get("choose_folder"),
            Filter = "Wingspan.exe|Wingspan.exe",
            FileName = "Wingspan.exe",
            CheckFileExists = true,
            InitialDirectory = start ?? string.Empty,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        string dir = Path.GetDirectoryName(dialog.FileName);
        if (!GameInstall.IsGameFolder(dir))
        {
            MessageBox.Show(this, Strings.Get("not_game_folder", dir), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _gameDir = GameInstall.NormalizeDir(dir);
        RefreshState();
        _installButton.Focus();
    }

    private bool EnsureGameClosed()
    {
        while (GameInstall.IsGameRunning(_gameDir))
        {
            if (MessageBox.Show(this, Strings.Get("game_running"), Text, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry)
                return false;
        }

        return true;
    }

    /// <summary>Tells what went wrong. True if a copy with administrator rights took over.</summary>
    private bool HandleError(Exception e)
    {
        if (e is InstallerException)
            Log.Detail(e.Message);
        else
            Log.Error(e);
        if (IsDisposed)
            return false;

        SetBusy(false);
        if (e is UnauthorizedAccessException)
        {
            if (Confirm(Strings.Get("write_failed", _gameDir)) && Program.RestartElevated(_options, _gameDir))
            {
                Close();
                return true;
            }

            return false;
        }

        string message = e is InstallerException ? e.Message : Strings.Get("error", e.Message, Log.FilePath);
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (_installing && e.CloseReason == CloseReason.UserClosing)
        {
            // Stopping halfway would leave a broken installation.
            e.Cancel = true;
            SystemSounds.Beep.Play();
            return;
        }

        _cancel.Cancel();
    }

    private void SetBusy(bool busy)
    {
        if (IsDisposed)
            return;
        // A focused button is about to be disabled; keep the focus somewhere useful.
        if (busy && ActiveControl is ButtonBase focused && focused != _closeButton && focused != _closeGameButton)
            _logBox.Focus();
        _busy = busy;
        _progressBar.Value = 0;
        RefreshState();
    }

    private void ReportProgress(int percent) => OnUi(() => _progressBar.Value = Math.Max(0, Math.Min(100, percent)));

    private void OnProgress(string message) => OnUi(() =>
    {
        _logBox.AppendText(message + Environment.NewLine);
        _statusLabel.Text = message;
        // Screen readers announce changes of a live region.
        _statusLabel.AccessibilityObject.RaiseLiveRegionChanged();
    });

    /// <summary>Runs an action on the window's thread; does nothing once the window is gone.</summary>
    private void OnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        if (!InvokeRequired)
        {
            action();
            return;
        }

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // The window is closing.
        }
    }

    private bool Confirm(string question, bool defaultNo = false) =>
        MessageBox.Show(this, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            defaultNo ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1) == DialogResult.Yes;

    private void ShowInfo(string text) => MessageBox.Show(this, text, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static Label NewLabel() => new() { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 4, 3, 4) };

    private static Button NewButton() => new() { AutoSize = true, MinimumSize = new Size(90, 28), Padding = new Padding(6, 0, 6, 0) };

    private static string Plain(string text) => text.Replace("&", string.Empty);
}
