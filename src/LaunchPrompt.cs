using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFish
{
    internal sealed class LaunchPrompt : Form
    {
        private readonly Label countdown;
        internal event Action CancelRequested;
        internal event Action StartRequested;

        internal LaunchPrompt(string scene, string mode, int seconds)
        {
            Text = "摸鱼 · 即将开始";
            Icon = AppIcon.Load();
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(248, 248, 248);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.Controls.Add(new Label { Text = (scene == "win10" ? "Windows 10" : "Windows 11") + " · " + mode,
                AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
            countdown = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Font.FontFamily, 18F) };
            root.Controls.Add(countdown, 0, 1);
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var cancel = new Button { Text = "取消", Dock = DockStyle.Fill };
            var start = new Button { Text = "立即开始", Dock = DockStyle.Fill };
            cancel.Click += delegate { if (CancelRequested != null) CancelRequested(); };
            start.Click += delegate { if (StartRequested != null) StartRequested(); };
            buttons.Controls.Add(cancel, 0, 0); buttons.Controls.Add(start, 1, 0);
            root.Controls.Add(buttons, 0, 2);
            Controls.Add(root);
            AcceptButton = start;
            CancelButton = cancel;
            using (var g = CreateGraphics())
            {
                float scale = g.DpiX / 96F;
                root.Scale(new SizeF(scale, scale));
                ClientSize = new Size((int)(360 * scale), (int)(220 * scale));
            }
            SetRemaining(seconds);
            FormClosing += delegate { if (CancelRequested != null) CancelRequested(); };
        }

        internal void SetRemaining(int seconds) { countdown.Text = seconds + " 秒后开始伪装"; }
    }
}
