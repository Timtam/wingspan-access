using System;
using System.Windows.Forms;

namespace WingspanAccessInstaller;

/// <summary>"--uninstall" from Installed apps: two questions, the removal, the result.</summary>
internal static class UninstallDialogs
{
    public static int Run(Options options)
    {
        string title = Strings.Get("title");
        string game = GameInstall.Locate(options.GameDir);
        if (game == null)
        {
            MessageBox.Show(Strings.Get("game_missing"), title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }

        if (!Ask(Strings.Get("confirm_uninstall", game), title, defaultNo: false))
            return 0;
        bool removeBepInEx = options.RemoveBepInEx
            || (GameInstall.BepInExVersion(game) != null && Ask(Strings.Get("confirm_remove_bepinex"), title, defaultNo: true));

        while (GameInstall.IsGameRunning(game))
        {
            if (MessageBox.Show(Strings.Get("game_running"), title, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry)
                return 3;
        }

        try
        {
            GameInstall.Uninstall(game, removeBepInEx, !options.NoRegistry);
            MessageBox.Show(Strings.Get(removeBepInEx ? "done_uninstall_bepinex" : "done_uninstall"), title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (UnauthorizedAccessException e)
        {
            Log.Error(e);
            return Ask(Strings.Get("write_failed", game), title, defaultNo: false) && Program.RestartElevated(options, game) ? 0 : 1;
        }
        catch (Exception e)
        {
            if (e is InstallerException)
                Log.Info(e.Message);
            else
                Log.Error(e);
            string message = e is InstallerException ? e.Message : Strings.Get("error", e.Message, Log.FilePath);
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static bool Ask(string question, string title, bool defaultNo) =>
        MessageBox.Show(question, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            defaultNo ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1) == DialogResult.Yes;
}
