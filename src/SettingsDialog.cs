using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;

namespace WindowsFish
{
    internal sealed class SettingsDialog : Form
    {
        private Preferences draft;
        private readonly ComboBox scene = Choice("Windows 11", "Windows 10");
        private readonly ComboBox palette = Choice("跟随场景", "经典蓝", "纯黑", "浅色（自定义）");
        private readonly ComboBox language = Choice("简体中文", "English");
        private readonly ComboBox textSize = Choice("小", "标准", "大");
        private readonly ComboBox monitor = Choice("主显示器（自动）");
        private readonly NumericUpDown seconds = new NumericUpDown { Minimum = 1, Maximum = 60, Dock = DockStyle.Fill };
        private readonly CheckBox eco = new CheckBox { Text = "节能动画", AutoSize = true };
        private readonly CheckBox closeToTray = new CheckBox { Text = "关闭窗口后驻留托盘", AutoSize = true };
        private readonly CheckBox startup = new CheckBox { Text = "登录 Windows 后静默驻留", AutoSize = true };
        private readonly ComboBox hotkey = Choice("关闭", "Alt+L", "Ctrl+Alt+L", "Ctrl+Shift+L");
        private readonly CheckBox dismissStart = new CheckBox { Text = "伪装时自动收起开始菜单", AutoSize = true };
        private readonly NumericUpDown startSeconds = new NumericUpDown { Minimum = 1, Maximum = 30, Dock = DockStyle.Fill };
        private readonly List<string> monitorNames = new List<string>();
        private readonly ComboBox exitChoice = Choice("Esc", "连续两次 Esc", "↑ ↑ ↓ ↓ ← → ← →", "Ctrl+Alt+Q", "自定义按键");
        private readonly ExitRecorder exitRecorder = new ExitRecorder();
        private readonly Button recordExit = new Button { Text = "录制按键", Anchor = AnchorStyles.Left, Size = new Size(120, 32) };
        private readonly Label exitError = new Label { AutoSize = true, ForeColor = Color.FromArgb(160, 45, 40), Dock = DockStyle.Fill };
        private string recordedExit;
        internal Preferences Result { get; private set; }

        internal SettingsDialog(Preferences current)
        {
            draft = current.Clone();
            Text = "摸鱼 · 设置";
            Icon = AppIcon.Load();
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(620, 460);
            MinimumSize = new Size(480, 380);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = Color.FromArgb(248, 248, 248);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var appearance = Page(tabs, "场景与外观");
            Row(appearance, "系统场景", scene);
            Row(appearance, "背景颜色", palette);
            Row(appearance, "状态语言", language);
            Row(appearance, "文字大小", textSize);
            var display = Page(tabs, "显示与启动");
            var screens = Screen.AllScreens;
            monitorNames.Add("");
            for (int i = 0; i < screens.Length; i++)
            {
                monitorNames.Add(screens[i].DeviceName);
                monitor.Items.Add(string.Format("{0}  {1} × {2}{3}", screens[i].DeviceName,
                    screens[i].Bounds.Width, screens[i].Bounds.Height, screens[i].Primary ? "（主屏）" : ""));
            }
            if (!string.IsNullOrEmpty(draft.Monitor) && !monitorNames.Contains(draft.Monitor))
            {
                monitorNames.Add(draft.Monitor);
                monitor.Items.Add(draft.Monitor + "（当前未连接）");
            }
            Row(display, "状态显示器", monitor);
            Row(display, "倒计时（秒）", seconds);
            Row(display, "动画刷新", eco);
            Row(display, "开始菜单", dismissStart);
            Row(display, "收起等待（秒）", startSeconds);
            dismissStart.CheckedChanged += delegate { startSeconds.Enabled = dismissStart.Checked; };
            var resident = Page(tabs, "常驻与快捷键");
            Row(resident, "关闭窗口", closeToTray);
            Row(resident, "开机启动", startup);
            Row(resident, "快捷启动", hotkey);
            Row(resident, "退出伪装", exitChoice);
            Row(resident, "按键序列", exitRecorder);
            Row(resident, "", recordExit);
            Row(resident, "", exitError);
            exitChoice.SelectedIndexChanged += delegate {
                bool custom = exitChoice.SelectedIndex == 4;
                recordExit.Enabled = custom;
                exitRecorder.Recording = false;
                recordExit.Text = "录制按键";
                exitRecorder.Text = custom ? (recordedExit == null ? "尚未录制" : ExitBinding.Format(recordedExit)) : ExitBinding.Format(ExitBinding.Presets[Math.Max(0, exitChoice.SelectedIndex)]);
                exitError.Text = "";
            };
            recordExit.Click += delegate {
                if (!exitRecorder.Recording) { exitRecorder.Begin(); recordExit.Text = "完成录制"; exitError.Text = ""; }
                else FinishRecording();
            };
            var about = new TabPage("关于与更新");
            var text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = BackColor };
            string log = "";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Changelog"))
                if (stream != null) using (var reader = new StreamReader(stream)) log = reader.ReadToEnd();
            text.Text = "摸鱼 " + AppVersion.Value + "\r\n\r\n" +
                "本地状态模拟工具，不执行 Windows 系统更新或重启。\r\nWin11 使用圆弧，Win10 使用圆点；浅色为自定义主题。\r\n\r\n" +
                "首页快捷键\r\nCtrl+1 / 2 / 3：选择状态\r\nCtrl+P：预览\r\nCtrl+Enter：开始\r\nCtrl+,：设置\r\n退出伪装：" + ExitBinding.Format(current.ExitPattern) + "\r\nEsc：取消倒计时或关闭预览\r\n\r\n" + log.Replace("\r\n", "\n").Replace("\n", "\r\n");
            about.Padding = new Padding(16);
            about.Controls.Add(text);
            tabs.TabPages.Add(about);
            root.Controls.Add(tabs, 0, 0);
            var footer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 12, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            var apply = new Button { Text = "应用", Size = new Size(88, 36) };
            var cancel = new Button { Text = "取消", Size = new Size(88, 36), DialogResult = DialogResult.Cancel };
            var reset = new Button { Text = "恢复默认", Size = new Size(112, 36) };
            apply.Click += delegate {
                if (exitRecorder.Recording && !FinishRecording()) return;
                string binding = exitChoice.SelectedIndex == 4 ? recordedExit : ExitBinding.Presets[exitChoice.SelectedIndex];
                Keys[] keys;
                if (!ExitBinding.TryParse(binding, out keys)) { ShowExitError("请录制至少两个按键，或带 Ctrl/Alt 的组合键。"); return; }
                if (hotkey.SelectedIndex > 0 && Array.IndexOf(keys, ExitBinding.LaunchKey(hotkey.SelectedIndex)) >= 0)
                { ShowExitError("退出按键与快捷启动冲突，请更换。"); return; }
                Result = ReadOptions(); DialogResult = DialogResult.OK; Close();
            };
            reset.Click += delegate {
                if (MessageBox.Show(this, "恢复所有默认选项？点击应用后生效。", "恢复默认", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes) ResetDraft();
            };
            apply.Dock = cancel.Dock = reset.Dock = DockStyle.Fill;
            footer.Controls.Add(reset, 0, 0); footer.Controls.Add(cancel, 1, 0); footer.Controls.Add(apply, 2, 0);
            root.Controls.Add(footer, 0, 1);
            Controls.Add(root);
            AcceptButton = apply;
            CancelButton = cancel;
            FillOptions();
            palette.DrawMode = DrawMode.OwnerDrawFixed;
            palette.DrawItem += DrawPalette;
            scene.SelectedIndexChanged += delegate { palette.Invalidate(); };
            using (var graphics = CreateGraphics())
            {
                float scale = graphics.DpiX / 96F;
                root.Scale(new SizeF(scale, scale));
                ClientSize = new Size((int)(620 * scale), (int)(460 * scale));
                MinimumSize = new Size((int)(480 * scale), (int)(380 * scale));
                palette.ItemHeight = (int)Math.Ceiling(Font.GetHeight(graphics) + 8 * scale);
            }
            Shown += delegate {
                FitToWorkArea(Screen.FromControl(Owner ?? this).WorkingArea);
            };
        }

        internal void FitToWorkArea(Rectangle area)
        {
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
        }

        private void DrawPalette(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            int inset = (int)(6 * e.Graphics.DpiX / 96F);
            int size = Math.Max(8, e.Bounds.Height - inset * 2);
            Color color = e.Index == 1 || (e.Index == 0 && scene.SelectedIndex == 1)
                ? Color.FromArgb(0, 120, 215) : e.Index == 3 ? Color.FromArgb(245, 246, 248) : Color.Black;
            var swatch = new Rectangle(e.Bounds.Left + inset, e.Bounds.Top + inset, size, size);
            using (var brush = new SolidBrush(color)) e.Graphics.FillRectangle(brush, swatch);
            e.Graphics.DrawRectangle(SystemPens.ControlDark, swatch);
            var bounds = new Rectangle(swatch.Right + inset, e.Bounds.Top,
                Math.Max(0, e.Bounds.Width - size - inset * 3), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, palette.Items[e.Index].ToString(), Font, bounds,
                e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }

        private static ComboBox Choice(params string[] values)
        {
            var result = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            result.Items.AddRange(values);
            return result;
        }

        private static TableLayoutPanel Page(TabControl tabs, string title)
        {
            var page = new TabPage(title) { AutoScroll = true, Padding = new Padding(16) };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 0 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            page.Controls.Add(table); tabs.TabPages.Add(page);
            return table;
        }

        private static void Row(TableLayoutPanel table, string title, Control input)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 12, 12, 12) }, 0, row);
            input.Margin = new Padding(0, 8, 0, 8);
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.AccessibleName = title;
            table.Controls.Add(input, 1, row);
        }

        private void FillOptions()
        {
            scene.SelectedIndex = draft.Scene == "win10" ? 1 : 0;
            palette.SelectedIndex = Array.IndexOf(new[] { "auto", "blue", "black", "light" }, draft.Palette);
            language.SelectedIndex = draft.Language == "en" ? 1 : 0;
            textSize.SelectedIndex = draft.TextSize;
            monitor.SelectedIndex = Math.Max(0, monitorNames.IndexOf(draft.Monitor));
            seconds.Value = draft.DelaySeconds;
            closeToTray.Checked = draft.CloseToTray;
            startup.Checked = draft.StartWithWindows;
            hotkey.SelectedIndex = draft.Hotkey;
            dismissStart.Checked = draft.DismissStartMenu;
            startSeconds.Value = draft.StartMenuSeconds;
            startSeconds.Enabled = dismissStart.Checked;
            eco.Checked = draft.EcoMode;
            int preset = Array.IndexOf(ExitBinding.Presets, draft.ExitPattern);
            recordedExit = preset < 0 ? draft.ExitPattern : null;
            exitChoice.SelectedIndex = preset < 0 ? 4 : preset;
            recordExit.Enabled = preset < 0;
            exitRecorder.Text = ExitBinding.Format(draft.ExitPattern);
            exitRecorder.Recording = false;
            recordExit.Text = "录制按键";
            exitError.Text = "";
        }

        private bool FinishRecording()
        {
            Keys[] keys;
            if (!ExitBinding.TryParse(exitRecorder.Binding, out keys)) { ShowExitError("请录制至少两个按键，或带 Ctrl/Alt 的组合键。"); return false; }
            recordedExit = exitRecorder.Binding;
            exitRecorder.Recording = false;
            recordExit.Text = "重新录制";
            exitError.Text = "";
            return true;
        }

        private void ShowExitError(string message)
        {
            exitError.Text = message;
            var page = exitError.Parent.Parent as TabPage;
            if (page != null) { ((TabControl)page.Parent).SelectedTab = page; page.ScrollControlIntoView(exitError); }
        }

        internal void ResetDraft() { draft = new Preferences(); FillOptions(); }

        internal Preferences ReadOptions()
        {
            var result = draft.Clone();
            result.Scene = scene.SelectedIndex == 1 ? "win10" : "win11";
            result.Palette = new[] { "auto", "blue", "black", "light" }[palette.SelectedIndex];
            result.Language = language.SelectedIndex == 1 ? "en" : "zh";
            result.TextSize = textSize.SelectedIndex;
            result.Monitor = monitorNames[monitor.SelectedIndex];
            result.DelaySeconds = (int)seconds.Value;
            result.CloseToTray = closeToTray.Checked;
            result.StartWithWindows = startup.Checked;
            result.Hotkey = hotkey.SelectedIndex;
            result.DismissStartMenu = dismissStart.Checked;
            result.StartMenuSeconds = (int)startSeconds.Value;
            result.EcoMode = eco.Checked;
            result.ExitPattern = exitChoice.SelectedIndex == 4 ? recordedExit ?? draft.ExitPattern : ExitBinding.Presets[exitChoice.SelectedIndex];
            return result;
        }
    }
}
