using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFish
{
    internal sealed class UpdateDialog : Form
    {
        private readonly IntroForm owner;
        private readonly bool silentRestart;
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill };
        private readonly TextBox notes = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
        private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Height = 14 };
        private readonly Button primary = new Button { Text = "检查更新", Dock = DockStyle.Fill };
        private readonly Button close = new Button { Text = "关闭", Dock = DockStyle.Fill };
        private CancellationTokenSource operation;
        private ReleaseInfo release;
        private bool installing;
        private bool shutdownForUpdate;
        private string stagedFile;
        private UpdatePlan activePlan;

        internal UpdateDialog(IntroForm owner, bool silentRestart)
        {
            this.owner = owner;
            this.silentRestart = silentRestart;
            Text = "摸鱼 · 应用更新";
            Icon = AppIcon.Load();
            Font = new Font("Microsoft YaHei UI", 10F);
            BackColor = Color.FromArgb(248, 248, 248);
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = MaximizeBox = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.Controls.Add(new Label { Text = "摸鱼  " + AppVersion.Value, AutoSize = true, Font = new Font(Font.FontFamily, 16F), Margin = new Padding(0, 0, 0, 12) }, 0, 0);
            status.Margin = new Padding(0, 0, 0, 12);
            root.Controls.Add(status, 0, 1);
            root.Controls.Add(progress, 0, 2);
            notes.Margin = new Padding(0, 12, 0, 12);
            root.Controls.Add(notes, 0, 3);
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            var site = new Button { Text = "发行页面", Dock = DockStyle.Fill };
            site.Click += delegate { try { Process.Start(new ProcessStartInfo(UpdateService.ReleasesUrl) { UseShellExecute = true }); } catch { status.Text = "无法打开浏览器，请稍后重试。"; } };
            buttons.Controls.Add(site, 0, 0); buttons.Controls.Add(primary, 1, 0); buttons.Controls.Add(close, 2, 0);
            root.Controls.Add(buttons, 0, 4);
            Controls.Add(root);
            close.Click += delegate { Close(); };
            primary.Click += async delegate { if (release == null) await CheckAsync(); else await InstallAsync(); };
            Shown += async delegate { await CheckAsync(); };
            CancelButton = close;
            using (var g = CreateGraphics())
            {
                float scale = g.DpiX / 96F;
                root.Scale(new SizeF(scale, scale));
                ClientSize = new Size((int)(560 * scale), (int)(420 * scale));
                MinimumSize = new Size((int)(400 * scale), (int)(320 * scale));
            }
            Shown += delegate {
                Rectangle area = Screen.FromControl(owner).WorkingArea;
                MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            };
        }

        private async Task CheckAsync()
        {
            BeginOperation("正在检查正式版本…", true);
            release = null;
            notes.Clear();
            primary.Text = "检查更新";
            try
            {
                ReleaseInfo result = await UpdateService.CheckAsync(operation.Token);
                if (IsDisposed) return;
                notes.Text = (result.Notes ?? "此版本没有更新说明。").Replace("\r\n", "\n").Replace("\n", "\r\n");
                if (result.Version > UpdateService.ParseVersion(AppVersion.Value))
                {
                    release = result;
                    status.Text = "发现新版本 " + result.Tag + "，下载后将更新并重新启动。";
                    primary.Text = "下载并安装";
                }
                else status.Text = "当前已是最新版本。";
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!IsDisposed) status.Text = Friendly(error); }
            finally { FinishOperation(); }
        }

        private async Task InstallAsync()
        {
            BeginOperation("正在下载更新…", false);
            try
            {
                var feedback = new Progress<int>(value => { if (!IsDisposed) { progress.Value = value; status.Text = "正在下载并校验  " + value + "%"; } });
                stagedFile = await UpdateService.DownloadAsync(release, feedback, operation.Token);
                if (IsDisposed) return;
                installing = true;
                close.Enabled = false;
                progress.Style = ProgressBarStyle.Marquee;
                status.Text = "正在准备安装，请稍候…";
                activePlan = UpdateInstaller.CreatePlan(stagedFile, release.Version, silentRestart);
                await UpdateInstaller.PrepareAsync(activePlan);
                shutdownForUpdate = true;
                owner.ExitApplication();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed) { status.Text = Friendly(error); primary.Text = "重试安装"; }
            }
            finally { installing = false; FinishOperation(); }
        }

        private void BeginOperation(string message, bool marquee)
        {
            if (operation != null) operation.Dispose();
            operation = new CancellationTokenSource();
            primary.Enabled = false;
            status.Text = message;
            progress.Style = marquee ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            progress.Value = 0;
        }

        private void FinishOperation()
        {
            if (IsDisposed) return;
            primary.Enabled = close.Enabled = true;
            progress.Style = ProgressBarStyle.Continuous;
        }

        private static string Friendly(Exception error)
        {
            Trace.WriteLine(error);
            if (error is IOException || error is InvalidDataException) return error.Message;
            if (error is UnauthorizedAccessException) return "当前位置不可写，请将程序移到可写文件夹后更新。";
            return "更新未完成，原程序未改动，请重试。";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (installing && !shutdownForUpdate) { e.Cancel = true; return; }
            if (operation != null) operation.Cancel();
            base.OnFormClosing(e);
        }

        internal void PrepareOwnerExit()
        {
            if (!shutdownForUpdate) UpdateInstaller.Cancel(activePlan);
            shutdownForUpdate = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !shutdownForUpdate) UpdateInstaller.Cancel(activePlan);
            if (disposing && operation != null) { operation.Cancel(); operation.Dispose(); operation = null; }
            base.Dispose(disposing);
        }
    }
}
