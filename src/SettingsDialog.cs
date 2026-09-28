using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

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
        private readonly CheckBox delay = new CheckBox { Text = "启用启动倒计时", AutoSize = true };
        private readonly string[] monitorNames;
        internal Preferences Result { get; private set; }

        internal SettingsDialog(Preferences current)
        {
            draft = current.Clone();
            Text = "摸鱼 · 设置";
            Icon = AppIcon.Load();
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(560, 420);
            MinimumSize = new Size(480, 380);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = Color.FromArgb(248, 248, 248);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 2 };
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
            monitorNames = new string[screens.Length + 1];
            monitorNames[0] = "";
            for (int i = 0; i < screens.Length; i++)
            {
                monitorNames[i + 1] = screens[i].DeviceName;
                monitor.Items.Add(string.Format("{0}  {1} × {2}{3}", screens[i].DeviceName,
                    screens[i].Bounds.Width, screens[i].Bounds.Height, screens[i].Primary ? "（主屏）" : ""));
            }
            Row(display, "状态显示器", monitor);
            Row(display, "延迟启动", delay);
            Row(display, "倒计时（秒）", seconds);
            Row(display, "动画刷新", eco);
            delay.CheckedChanged += delegate { seconds.Enabled = delay.Checked; };
            var about = new TabPage("关于与更新");
            var text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = BackColor };
            string log = "";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Changelog"))
                if (stream != null) using (var reader = new StreamReader(stream)) log = reader.ReadToEnd();
            text.Text = "摸鱼 " + AppVersion.Value + "\r\n\r\n" +
                "本地状态模拟工具，不执行真实更新或重启。\r\n圆点动画为自绘近似，浅色为自定义主题。\r\n\r\n" +
                "首页快捷键\r\nCtrl+1 / 2 / 3：选择状态\r\nCtrl+P：预览\r\nCtrl+Enter：开始\r\nCtrl+,：设置\r\nEsc：退出全屏或取消倒计时\r\n\r\n" + log.Replace("\r\n", "\n").Replace("\n", "\r\n");
            about.Padding = new Padding(16);
            about.Controls.Add(text);
            tabs.TabPages.Add(about);
            root.Controls.Add(tabs, 0, 0);
            var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
            var apply = new Button { Text = "应用", Size = new Size(88, 36) };
            var cancel = new Button { Text = "取消", Size = new Size(88, 36), DialogResult = DialogResult.Cancel };
            var reset = new Button { Text = "恢复默认", Size = new Size(112, 36) };
            apply.Click += delegate { Result = ReadOptions(); DialogResult = DialogResult.OK; Close(); };
            reset.Click += delegate {
                if (MessageBox.Show(this, "恢复所有默认选项？点击应用后生效。", "恢复默认", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes) ResetDraft();
            };
            footer.Controls.Add(apply); footer.Controls.Add(cancel); footer.Controls.Add(reset);
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
                ClientSize = new Size((int)(560 * scale), (int)(420 * scale));
                MinimumSize = new Size((int)(480 * scale), (int)(380 * scale));
                palette.ItemHeight = (int)Math.Ceiling(Font.GetHeight(graphics) + 8 * scale);
            }
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
            monitor.SelectedIndex = Math.Max(0, Array.IndexOf(monitorNames, draft.Monitor));
            seconds.Value = draft.DelaySeconds;
            delay.Checked = draft.DelayStart;
            seconds.Enabled = delay.Checked;
            eco.Checked = draft.EcoMode;
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
            result.DelayStart = delay.Checked;
            result.EcoMode = eco.Checked;
            return result;
        }
    }
}
