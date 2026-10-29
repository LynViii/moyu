using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFish
{
    internal sealed partial class IntroForm
    {
        private readonly bool desktopEnabled;
        private bool startupHandled;
        private bool exiting;
        private bool returnToTray;
        private NotifyIcon tray;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem trayStart;
        private ToolStripMenuItem trayReturn;
        private Label desktopStatus;
        private GlobalHotkey hotkey;
        private LaunchPrompt launchPrompt;
        private Form settingsWindow;
        private UpdateDialog updateWindow;
        private ToolStripMenuItem trayUpdate;

        private void InitializeDesktop()
        {
            UpdateService.CleanupStaging();
            preferences.StartWithWindows = StartupRegistration.IsEnabled();
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("打开摸鱼", null, delegate { Reveal(); });
            trayStart = new ToolStripMenuItem("开始伪装", null, delegate {
                if (launchTimer != null) CancelLaunch(); else StartLaunch(true);
            });
            trayReturn = new ToolStripMenuItem("结束伪装", null, delegate { if (fishMode != null) fishMode.Stop(); });
            trayMenu.Items.Add(trayStart);
            trayMenu.Items.Add(trayReturn);
            trayUpdate = new ToolStripMenuItem("检查更新", null, delegate { OpenUpdateDialog(); });
            trayMenu.Items.Add(trayUpdate);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("退出摸鱼", null, delegate { ExitApplication(); });
            tray = new NotifyIcon { Icon = AppIcon.Load(), Text = "摸鱼", ContextMenuStrip = trayMenu, Visible = true };
            trayMenu.Opening += delegate { UpdateTrayState(); };
            tray.DoubleClick += delegate { Reveal(); };
            ConfigureHotkey();
            UpdateTrayState();
        }

        private void ConfigureHotkey()
        {
            if (!desktopEnabled) return;
            if (hotkey != null) { hotkey.Dispose(); hotkey = null; }
            if (preferences.Hotkey == 0) { desktopStatus.Text = "快捷启动已关闭"; return; }
            hotkey = new GlobalHotkey(preferences.Hotkey, delegate {
                if (launchTimer != null) CancelLaunch(); else StartLaunch(true);
            });
            desktopStatus.Text = hotkey.Registered
                ? "快捷启动  " + GlobalHotkey.Label(preferences.Hotkey)
                : hotkey.ErrorCode == 1409 ? GlobalHotkey.Label(preferences.Hotkey) + " 已被占用，请在设置中更换"
                : "快捷启动暂不可用，请重新启动摸鱼";
        }

        private bool ApplyStartupSetting(Preferences settings)
        {
            if (settings.StartWithWindows == preferences.StartWithWindows && !settings.StartWithWindows) return true;
            string error;
            if (StartupRegistration.SetEnabled(settings.StartWithWindows, Application.ExecutablePath, out error)) return true;
            MessageBox.Show(this, "无法修改开机启动：" + error, "摸鱼", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        internal void HideToTray()
        {
            CancelLaunch();
            ShowInTaskbar = false;
            Hide();
            UpdateTrayState();
        }

        internal void ExitApplication()
        {
            exiting = true;
            CancelLaunch();
            if (fishMode != null) fishMode.Stop();
            if (previewWindow != null) previewWindow.Close();
            if (settingsWindow != null) settingsWindow.Close();
            if (updateWindow != null) { updateWindow.PrepareOwnerExit(); updateWindow.Close(); }
            Close();
        }

        private void UpdateTrayState()
        {
            if (tray == null) return;
            string exitLabel = ExitBinding.Format(preferences.ExitPattern);
            if (exitLabel.Length > 36) exitLabel = "自定义按键";
            tray.Text = fishMode != null ? "摸鱼 · 伪装中（" + exitLabel + " 返回）" : launchTimer != null ? "摸鱼 · 即将开始" : "摸鱼 · 待命";
            trayStart.Text = launchTimer != null ? "取消开始" : "开始伪装";
            trayStart.Enabled = fishMode == null && previewWindow == null && settingsWindow == null && updateWindow == null;
            trayReturn.Enabled = fishMode != null;
            trayUpdate.Enabled = fishMode == null && launchTimer == null && previewWindow == null && settingsWindow == null;
        }

        private void OpenUpdateDialog()
        {
            if (updateWindow != null) { updateWindow.Activate(); return; }
            if (fishMode != null || launchTimer != null || previewWindow != null || settingsWindow != null) return;
            updateWindow = new UpdateDialog(this, !Visible);
            updateWindow.FormClosed += delegate { updateWindow = null; if (!IsDisposed && !Disposing && !exiting) Enabled = true; UpdateTrayState(); };
            Enabled = false;
            updateWindow.Show();
            UpdateTrayState();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && desktopEnabled && preferences.CloseToTray && !exiting)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            exiting = true;
            if (fishMode != null) fishMode.Stop();
            base.OnFormClosing(e);
        }

        private void DisposeDesktop()
        {
            if (hotkey != null) { hotkey.Dispose(); hotkey = null; }
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            if (trayMenu != null) { trayMenu.Dispose(); trayMenu = null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                exiting = true;
                CancelLaunch();
                if (fishMode != null) fishMode.Stop();
                if (updateWindow != null) { updateWindow.Dispose(); updateWindow = null; }
                DisposeDesktop();
            }
            base.Dispose(disposing);
        }
    }
}
