using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.Win32;

[assembly: AssemblyTitle("摸鱼")]
[assembly: AssemblyProduct("摸鱼")]
[assembly: AssemblyDescription("Windows 11 style fullscreen update screen for breaks")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyVersion(WindowsFish.AppVersion.FileValue)]
[assembly: AssemblyFileVersion(WindowsFish.AppVersion.FileValue)]
[assembly: AssemblyInformationalVersion(WindowsFish.AppVersion.Value)]

namespace WindowsFish
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            new SingleInstanceApp().Run(args);
        }
    }

    internal sealed class SingleInstanceApp : WindowsFormsApplicationBase
    {
        public SingleInstanceApp()
        {
            IsSingleInstance = true;
            EnableVisualStyles = true;
            ShutdownStyle = ShutdownMode.AfterMainFormCloses;
        }

        protected override void OnCreateMainForm()
        {
            var form = new IntroForm();
            MainForm = form;
            form.Shown += delegate { form.ApplyArguments(new List<string>(CommandLineArgs).ToArray()); };
        }

        protected override void OnStartupNextInstance(StartupNextInstanceEventArgs e)
        {
            base.OnStartupNextInstance(e);
            e.BringToForeground = false;
            ((IntroForm)MainForm).Reveal();
            ((IntroForm)MainForm).ApplyArguments(new List<string>(e.CommandLine).ToArray());
        }
    }

    internal sealed class Preferences
    {
        internal string Mode = "update";
        internal int Minutes;
        internal bool BlackoutSecondary = true;
        internal int Speed = 1;
        internal bool DelayStart;
        internal static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Moyu", "settings.xml");

        internal static Preferences Load(string path)
        {
            var settings = new Preferences();
            try
            {
                var root = XDocument.Load(path).Root;
                string mode = (string)root.Attribute("mode");
                int minutes;
                int speed;
                bool delay;
                if (int.TryParse((string)root.Attribute("speed"), out speed) && speed >= 0 && speed <= 2) settings.Speed = speed;
                if (bool.TryParse((string)root.Attribute("delayStart"), out delay)) settings.DelayStart = delay;
                bool blackout;
                if (bool.TryParse((string)root.Attribute("blackoutSecondary"), out blackout)) settings.BlackoutSecondary = blackout;
                if (mode == "update" || mode == "restart" || mode == "prepare") settings.Mode = mode;
                if (int.TryParse((string)root.Attribute("minutes"), out minutes) &&
                    minutes >= 0 && minutes <= 240) settings.Minutes = minutes;
            }
            catch (Exception error) { Trace.WriteLine(error.Message); }
            return settings;
        }

        internal void Save(string path)
        {
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                new XDocument(new XElement("preferences", new XAttribute("mode", Mode),
                    new XAttribute("minutes", Minutes), new XAttribute("blackoutSecondary", BlackoutSecondary),
                    new XAttribute("speed", Speed), new XAttribute("delayStart", DelayStart))).Save(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (Exception error) { Trace.WriteLine(error.Message); }
        }
    }

    internal sealed class IntroForm : Form
    {
        private FishMode fishMode;
        private UpdateScreenMode selectedMode;
        private Button selectedModeButton;
        private Label selectedModeLabel;
        private ComboBox returnAfter;
        private NumericUpDown customMinutes;
        private CheckBox blackoutSecondary;
        private UpdateForm previewWindow;
        private ComboBox speedChoice;
        private CheckBox delayStart;
        private Button startButton;
        private Timer launchTimer;
        private Stopwatch launchClock;
        private readonly Stopwatch sessionClock = new Stopwatch();
        private Label sessionSummary;
        private double SpeedFactor { get { return new[] { 0.75D, 1D, 1.5D }[speedChoice.SelectedIndex]; } }
        private static readonly Color Accent = Color.FromArgb(0, 111, 116);
        private readonly Preferences preferences;
        private readonly string settingsPath;
        private bool ready;

        public IntroForm() : this(Preferences.FilePath) { }

        internal IntroForm(string settingsPath)
        {
            this.settingsPath = settingsPath;
            preferences = Preferences.Load(settingsPath);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Text = "摸鱼";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 450);
            MinimumSize = new Size(640, 480);
            MaximizeBox = false;
            BackColor = Color.FromArgb(248, 248, 248);
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = AppIcon.Load();

            BuildLayout();
            using (Graphics graphics = CreateGraphics())
                ApplyLayoutScale(graphics.DpiX / 96F);
            ready = true;
        }

        public void Reveal()
        {
            if (previewWindow != null && !previewWindow.IsDisposed) { previewWindow.Activate(); return; }
            if (fishMode != null) { fishMode.ActivatePrimary(); return; }
            Show();
            WindowState = FormWindowState.Normal;
            Rectangle area = Screen.FromControl(this).WorkingArea;
            if (!area.Contains(Bounds)) Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2),
                area.Top + Math.Max(0, (area.Height - Height) / 2));
            Activate();
        }

        internal void ApplyArguments(string[] args)
        {
            if (args.Length == 0 || fishMode != null || previewWindow != null) return;
            string mode = selectedMode.Id;
            int minutes = SelectedMinutes;
            int speed = speedChoice.SelectedIndex;
            bool start = false;
            bool primaryOnly = false;
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string option = args[i];
                    if (option == "--start") { start = true; continue; }
                    if (option == "--primary-only") { primaryOnly = true; continue; }
                    if (++i >= args.Length) throw new ArgumentException("参数缺少取值：" + option);
                    string value = args[i];
                    if (option == "--mode" && (value == "update" || value == "restart" || value == "prepare")) mode = value;
                    else if (option == "--minutes" && int.TryParse(value, out minutes) && minutes >= 0 && minutes <= 240) { }
                    else if (option == "--speed" && (value == "slow" || value == "normal" || value == "fast"))
                        speed = value == "slow" ? 0 : value == "normal" ? 1 : 2;
                    else throw new ArgumentException("无效参数：" + option + " " + value);
                }
            }
            catch (ArgumentException error)
            {
                MessageBox.Show(this, error.Message, "启动参数", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            CancelLaunch();
            ready = false;
            try
            {
                foreach (Control control in Controls)
                {
                    var button = control as Button;
                    var state = button == null ? null : button.Tag as UpdateScreenMode;
                    if (state != null && state.Id == mode) SelectMode(state, button);
                }
                int preset = Array.IndexOf(new[] { 0, 15, 30, 60 }, minutes);
                customMinutes.Value = Math.Max(1, minutes);
                returnAfter.SelectedIndex = preset < 0 ? 4 : preset;
                speedChoice.SelectedIndex = speed;
                if (primaryOnly) blackoutSecondary.Checked = false;
                UpdateSelectedModeLabel();
            }
            finally { ready = true; }
            SavePreferences();
            if (start) StartButtonClick(this, EventArgs.Empty);
        }

        private void SavePreferences()
        {
            if (!ready) return;
            preferences.Mode = selectedMode.Id;
            preferences.Minutes = SelectedMinutes;
            preferences.BlackoutSecondary = blackoutSecondary.Checked;
            preferences.Speed = speedChoice.SelectedIndex;
            preferences.DelayStart = delayStart.Checked;
            preferences.Save(settingsPath);
        }

        private int SelectedMinutes
        {
            get { return returnAfter.SelectedIndex == 4 ? (int)customMinutes.Value : new[] { 0, 15, 30, 60 }[returnAfter.SelectedIndex]; }
        }

        private void ApplyLayoutScale(float scale)
        {
            // Point fonts already follow DPI; only scale logical pixel bounds.
            SuspendLayout();
            foreach (Control control in Controls)
            {
                Rectangle r = control.Bounds;
                control.Bounds = new Rectangle((int)Math.Round(r.X * scale),
                    (int)Math.Round(r.Y * scale), (int)Math.Round(r.Width * scale),
                    (int)Math.Round(r.Height * scale));
            }
            MinimumSize = Size.Empty;
            ClientSize = new Size((int)Math.Round(720 * scale), (int)Math.Round(520 * scale));
            ResumeLayout(false);
        }

        private void BuildLayout()
        {
            var logo = new PictureBox();
            Image logoImage = AppIcon.LoadImage();
            logo.Image = logoImage;
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.SetBounds(48, 30, 48, 48);
            if (logoImage != null)
            {
                Controls.Add(logo);
            }

            var title = new Label();
            title.Text = "摸鱼";
            title.Font = new Font("Microsoft YaHei UI", 19F, FontStyle.Regular, GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(24, 24, 24);
            title.AutoSize = false;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.SetBounds(110, 30, 200, 48);
            Controls.Add(title);

            var description = new Label();
            description.Text = AppVersion.Value;
            description.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            description.ForeColor = Color.FromArgb(70, 70, 70);
            description.AutoSize = false;
            description.TextAlign = ContentAlignment.MiddleRight;
            description.SetBounds(550, 40, 120, 24);
            Controls.Add(description);
            var separator = new Label();
            separator.BackColor = Color.FromArgb(224, 229, 231);
            separator.SetBounds(48, 102, 624, 1);
            Controls.Add(separator);

            var modeLabel = new Label();
            modeLabel.Text = "选择状态";
            modeLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            modeLabel.ForeColor = Color.FromArgb(48, 48, 48);
            modeLabel.AutoSize = false;
            modeLabel.TextAlign = ContentAlignment.MiddleLeft;
            modeLabel.SetBounds(48, 122, 540, 24);
            Controls.Add(modeLabel);

            var modes = UpdateScreenMode.All();
            selectedMode = modes[0];

            for (int i = 0; i < modes.Length; i++)
            {
                var modeButton = CreateModeButton(modes[i]);
                int column = i % 3;
                int row = i / 3;
                modeButton.SetBounds(48 + column * 212, 160 + row * 48, 200, 44);
                Controls.Add(modeButton);

                if (modes[i].Id == preferences.Mode)
                {
                    SelectMode(modes[i], modeButton);
                }
            }

            selectedModeLabel = new Label();
            selectedModeLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            selectedModeLabel.ForeColor = Color.FromArgb(72, 72, 72);
            selectedModeLabel.AutoSize = false;
            selectedModeLabel.TextAlign = ContentAlignment.MiddleCenter;
            selectedModeLabel.SetBounds(48, 220, 624, 26);
            Controls.Add(selectedModeLabel);
            UpdateSelectedModeLabel();

            var returnLabel = new Label();
            returnLabel.Text = "自动返回";
            returnLabel.TextAlign = ContentAlignment.MiddleLeft;
            returnLabel.SetBounds(48, 270, 110, 30);
            Controls.Add(returnLabel);

            returnAfter = new ComboBox();
            returnAfter.DropDownStyle = ComboBoxStyle.DropDownList;
            returnAfter.Items.AddRange(new object[] { "不限时", "15 分钟后", "30 分钟后", "60 分钟后", "自定义（分钟）" });
            int preset = Array.IndexOf(new[] { 0, 15, 30, 60 }, preferences.Minutes);
            returnAfter.SelectedIndex = preset < 0 ? 4 : preset;
            returnAfter.SetBounds(410, 270, 162, 30);
            Controls.Add(returnAfter);

            customMinutes = new NumericUpDown();
            customMinutes.Minimum = 1;
            customMinutes.Maximum = 240;
            customMinutes.Value = Math.Max(1, preferences.Minutes);
            customMinutes.Enabled = returnAfter.SelectedIndex == 4;
            customMinutes.SetBounds(584, 270, 88, 30);
            customMinutes.ValueChanged += delegate { SavePreferences(); };
            returnAfter.SelectedIndexChanged += delegate
            {
                customMinutes.Enabled = returnAfter.SelectedIndex == 4;
                SavePreferences();
            };
            Controls.Add(customMinutes);

            blackoutSecondary = new CheckBox();
            blackoutSecondary.Text = "扩展屏黑屏";
            blackoutSecondary.Checked = preferences.BlackoutSecondary;
            blackoutSecondary.SetBounds(48, 316, 220, 30);
            blackoutSecondary.CheckedChanged += delegate { SavePreferences(); };
            Controls.Add(blackoutSecondary);

            var preview = new Button();
            preview.Text = "预览";
            preview.FlatStyle = FlatStyle.Flat;
            preview.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 219);
            preview.ForeColor = Accent;
            preview.BackColor = Color.White;
            preview.SetBounds(510, 314, 162, 34);
            preview.Click += delegate { ShowPreview(); };
            Controls.Add(preview);

            delayStart = new CheckBox();
            delayStart.Text = "5 秒后开始";
            delayStart.Checked = preferences.DelayStart;
            delayStart.SetBounds(48, 358, 220, 30);
            delayStart.CheckedChanged += delegate { SavePreferences(); };
            Controls.Add(delayStart);

            speedChoice = new ComboBox();
            speedChoice.DropDownStyle = ComboBoxStyle.DropDownList;
            speedChoice.Items.AddRange(new object[] { "动画 · 慢速", "动画 · 标准", "动画 · 快速" });
            speedChoice.SelectedIndex = preferences.Speed;
            speedChoice.SetBounds(510, 358, 162, 30);
            speedChoice.SelectedIndexChanged += delegate { SavePreferences(); };
            Controls.Add(speedChoice);

            startButton = new Button();
            startButton.Text = "开始摸鱼";
            startButton.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            startButton.BackColor = Accent;
            startButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 92, 97);
            startButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 77, 81);
            startButton.ForeColor = Color.White;
            startButton.FlatStyle = FlatStyle.Flat;
            startButton.FlatAppearance.BorderSize = 0;
            startButton.SetBounds(250, 414, 220, 46);
            startButton.Click += StartButtonClick;
            Controls.Add(startButton);

            AcceptButton = startButton;
            sessionSummary = new Label();
            sessionSummary.TextAlign = ContentAlignment.MiddleCenter;
            sessionSummary.ForeColor = Color.FromArgb(100, 110, 112);
            sessionSummary.SetBounds(48, 474, 624, 24);
            Controls.Add(sessionSummary);
        }

        private void ShowPreview()
        {
            CancelLaunch();
            using (var preview = new UpdateForm(null, selectedMode, SpeedFactor))
            {
                previewWindow = preview;
                preview.Text = "预览 · " + selectedMode.DisplayName;
                preview.FormBorderStyle = FormBorderStyle.FixedDialog;
                preview.StartPosition = FormStartPosition.CenterParent;
                preview.MaximizeBox = false;
                preview.MinimizeBox = false;
                preview.ShowInTaskbar = false;
                preview.KeyPreview = true;
                using (Graphics g = CreateGraphics())
                    preview.ClientSize = new Size((int)(520 * g.DpiX / 96), (int)(300 * g.DpiY / 96));
                preview.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) preview.Close(); };
                try { preview.ShowDialog(this); }
                finally { previewWindow = null; }
            }
        }

        private Button CreateModeButton(UpdateScreenMode mode)
        {
            var button = new Button();
            button.Text = mode.DisplayName;
            button.Tag = mode;
            button.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(198, 198, 198);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(38, 38, 38);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Cursor = Cursors.Hand;
            button.Click += ModeButtonClick;
            return button;
        }

        private void ModeButtonClick(object sender, EventArgs e)
        {
            var button = (Button)sender;
            SelectMode((UpdateScreenMode)button.Tag, button);
            UpdateSelectedModeLabel();
            SavePreferences();
        }

        private void SelectMode(UpdateScreenMode mode, Button button)
        {
            if (selectedModeButton != null)
            {
                selectedModeButton.BackColor = Color.White;
                selectedModeButton.ForeColor = Color.FromArgb(38, 38, 38);
                selectedModeButton.FlatAppearance.BorderColor = Color.FromArgb(198, 198, 198);
            }

            selectedMode = mode;
            selectedModeButton = button;
            selectedModeButton.BackColor = Color.FromArgb(227, 244, 241);
            selectedModeButton.ForeColor = Accent;
            selectedModeButton.FlatAppearance.BorderColor = Accent;
        }

        private void UpdateSelectedModeLabel()
        {
            if (selectedModeLabel == null || selectedMode == null)
            {
                return;
            }

            string preview = selectedMode.Line1;
            if (!string.IsNullOrEmpty(selectedMode.Line2))
            {
                preview += "  " + selectedMode.Line2;
            }

            selectedModeLabel.Text = preview;
        }

        private void StartButtonClick(object sender, EventArgs e)
        {
            if (launchTimer != null) { CancelLaunch(); return; }
            if (!delayStart.Checked) { BeginSession(); return; }
            launchClock = Stopwatch.StartNew();
            startButton.Text = "取消开始 · 5";
            launchTimer = new Timer();
            launchTimer.Interval = 100;
            launchTimer.Tick += delegate
            {
                int remaining = Math.Max(0, 5 - (int)launchClock.Elapsed.TotalSeconds);
                startButton.Text = "取消开始 · " + remaining;
                if (remaining == 0) { CancelLaunch(); BeginSession(); }
            };
            launchTimer.Start();
        }

        private void CancelLaunch()
        {
            if (launchTimer == null) return;
            launchTimer.Stop();
            launchTimer.Dispose();
            launchTimer = null;
            startButton.Text = "开始摸鱼";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && launchTimer != null) { CancelLaunch(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CancelLaunch();
            base.OnFormClosed(e);
        }

        private void BeginSession()
        {
            fishMode = new FishMode(this, selectedMode, SelectedMinutes, blackoutSecondary.Checked, SpeedFactor);
            sessionClock.Restart();
            Hide();
            try
            {
                fishMode.Start();
            }
            catch (Exception error)
            {
                fishMode.Stop();
                MessageBox.Show(this, error.Message, "无法进入摸鱼状态", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ReturnFromFishMode()
        {
            if (sessionClock.IsRunning)
            {
                sessionClock.Stop();
                sessionSummary.Text = string.Format("本次用时  {0:00}:{1:00}:{2:00}",
                    (int)sessionClock.Elapsed.TotalHours, sessionClock.Elapsed.Minutes, sessionClock.Elapsed.Seconds);
            }
            fishMode = null;
            if (!IsDisposed && !Disposing) Reveal();
        }
    }

    internal sealed class FishMode
    {
        private readonly IntroForm owner;
        private readonly UpdateScreenMode screenMode;
        private readonly List<Form> windows = new List<Form>();
        private bool closing;
        private readonly int returnMinutes;
        private Timer returnTimer;
        private Stopwatch elapsed;
        private bool cursorHidden;
        private bool returned;
        private bool rebuilding;
        private bool displaySubscribed;
        private Timer displayTimer;
        private readonly bool blackoutSecondary;
        private readonly double speedFactor;
        internal double AnimationSeconds { get { return elapsed == null ? 0D : elapsed.Elapsed.TotalSeconds; } }

        public FishMode(IntroForm owner, UpdateScreenMode screenMode, int returnMinutes)
            : this(owner, screenMode, returnMinutes, true) { }

        public FishMode(IntroForm owner, UpdateScreenMode screenMode, int returnMinutes, bool blackoutSecondary)
            : this(owner, screenMode, returnMinutes, blackoutSecondary, 1D) { }

        public FishMode(IntroForm owner, UpdateScreenMode screenMode, int returnMinutes, bool blackoutSecondary, double speedFactor)
        {
            this.owner = owner;
            this.screenMode = screenMode;
            this.returnMinutes = returnMinutes;
            this.blackoutSecondary = blackoutSecondary;
            this.speedFactor = speedFactor;
        }

        public void Start()
        {
            Cursor.Hide();
            cursorHidden = true;
            elapsed = Stopwatch.StartNew();

            RebuildDisplays();
            displayTimer = new Timer();
            displayTimer.Interval = 350;
            displayTimer.Tick += delegate
            {
                displayTimer.Stop();
                try { RebuildDisplays(); }
                catch (Exception error) { Trace.WriteLine(error); Stop(); }
            };
            SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
            displaySubscribed = true;
            if (returnMinutes > 0)
            {
                returnTimer = new Timer();
                returnTimer.Interval = 500;
                returnTimer.Tick += delegate { CheckAutoReturn(elapsed.Elapsed.TotalMinutes); };
                returnTimer.Start();
            }
        }

        private void DisplaySettingsChanged(object sender, EventArgs e)
        {
            try
            {
                owner.BeginInvoke((MethodInvoker)delegate
                {
                    if (closing || displayTimer == null) return;
                    displayTimer.Stop();
                    displayTimer.Start();
                });
            }
            catch (InvalidOperationException) { }
        }

        internal void RebuildDisplays()
        {
            if (closing) return;
            rebuilding = true;
            try
            {
            foreach (Form old in windows.ToArray()) old.Close();
            windows.Clear();

            foreach (Screen screen in Screen.AllScreens)
            {
                if (!screen.Primary && !blackoutSecondary) continue;
                Form form = screen.Primary ? (Form)new UpdateForm(this, screenMode, speedFactor) : new BlackForm(this);
                form.StartPosition = FormStartPosition.Manual;
                form.FormBorderStyle = FormBorderStyle.None;
                form.AutoScaleMode = AutoScaleMode.None;
                form.Bounds = screen.Bounds;
                form.TopMost = true;
                form.KeyPreview = true;
                form.ShowInTaskbar = screen.Primary;
                form.KeyDown += FullscreenKeyDown;
                form.FormClosing += delegate(object sender, FormClosingEventArgs e)
                {
                    if (!closing && !rebuilding && e.CloseReason == CloseReason.UserClosing)
                        e.Cancel = true;
                };
                form.FormClosed += FullscreenClosed;
                windows.Add(form);
            }

            foreach (Form form in windows)
            {
                form.Show();
            }
            ActivatePrimary();
            }
            finally { rebuilding = false; }
        }

        public void ActivatePrimary()
        {
            foreach (Form form in windows)
                if (form is UpdateForm)
                {
                    form.WindowState = FormWindowState.Normal;
                    form.Bounds = Screen.PrimaryScreen.Bounds;
                    form.Activate();
                }
        }

        internal void CheckAutoReturn(double elapsedMinutes)
        {
            if (returnMinutes > 0 && elapsedMinutes >= returnMinutes) Stop();
        }

        public void Stop()
        {
            if (closing)
            {
                return;
            }

            closing = true;
            if (displaySubscribed)
            {
                SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
                displaySubscribed = false;
            }
            if (displayTimer != null)
            {
                displayTimer.Stop();
                displayTimer.Dispose();
                displayTimer = null;
            }
            if (returnTimer != null)
            {
                returnTimer.Stop();
                returnTimer.Dispose();
                returnTimer = null;
            }
            foreach (Form form in windows.ToArray())
            {
                if (!form.IsDisposed)
                {
                    form.Close();
                }
            }
            ReturnToOwner();
        }

        private void ReturnToOwner()
        {
            if (returned) return;
            returned = true;
            if (cursorHidden)
            {
                Cursor.Show();
                cursorHidden = false;
            }
            owner.ReturnFromFishMode();
        }

        private void FullscreenKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Stop();
            }
        }

        private void FullscreenClosed(object sender, FormClosedEventArgs e)
        {
            windows.Remove((Form)sender);

            if (windows.Count == 0 && !rebuilding)
            {
                Stop();
            }
        }
    }

    internal sealed class BlackForm : Form
    {
        public BlackForm(FishMode mode)
        {
            BackColor = Color.Black;
            Icon = AppIcon.Load();
        }
    }

    internal sealed class UpdateForm : Form
    {
        private readonly Timer timer;
        private readonly UpdateScreenMode screenMode;
        private readonly FishMode mode;
        private readonly Stopwatch previewClock = Stopwatch.StartNew();
        private readonly double speedFactor;
        private Rectangle spinnerBounds;
        private readonly Font statusFont = new Font("Microsoft YaHei UI", 15F, FontStyle.Regular, GraphicsUnit.Point);

        public UpdateForm(FishMode mode, UpdateScreenMode screenMode)
            : this(mode, screenMode, 1D) { }

        public UpdateForm(FishMode mode, UpdateScreenMode screenMode, double speedFactor)
        {
            this.speedFactor = speedFactor;
            this.screenMode = screenMode;
            this.mode = mode;
            BackColor = Color.Black;
            DoubleBuffered = true;
            Icon = AppIcon.Load();
            ResizeRedraw = true;

            timer = new Timer();
            timer.Interval = 16;
            timer.Tick += TimerTick;
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Black);

            float cx = ClientSize.Width / 2F;
            float lineGap = 28F * g.DpiY / 96F;
            bool hasSecondLine = !string.IsNullOrEmpty(screenMode.Line2);
            float textCenterY = ClientSize.Height / 2F + 6F;
            float firstLineY = hasSecondLine ? textCenterY - lineGap / 2F : textCenterY;
            float spinnerY = firstLineY - 64F * g.DpiY / 96F;
            float extent = 23F * g.DpiY / 96F;
            spinnerBounds = Rectangle.Ceiling(new RectangleF(cx - extent, spinnerY - extent, extent * 2, extent * 2));

            DrawSpinner(g, cx, spinnerY);
            if (e.ClipRectangle.Bottom < firstLineY - lineGap) return;
            DrawCenteredText(g, screenMode.Line1, cx, firstLineY);
            if (hasSecondLine)
            {
                DrawCenteredText(g, screenMode.Line2, cx, firstLineY + lineGap);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null) { timer.Stop(); timer.Dispose(); }
                statusFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (!spinnerBounds.IsEmpty) Invalidate(spinnerBounds);
            else Invalidate();
        }

        private void DrawCenteredText(
            Graphics g,
            string text,
            float x,
            float y)
        {
            using (var format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, statusFont, Brushes.White, new PointF(x, y), format);
            }
        }

        private void DrawSpinner(Graphics g, float cx, float cy)
        {
            float scale = g.DpiY / 96F;
            float radius = 18F * scale;
            double seconds = (mode == null ? previewClock.Elapsed.TotalSeconds : mode.AnimationSeconds) * speedFactor;
            for (int i = 0; i < 5; i++)
            {
                double local = (seconds + 4.8D - i * 0.12D) % 4.8D;
                if (local > 4.0D) continue;
                double turn = local / 2D;
                double fraction = turn - Math.Floor(turn);
                double eased = fraction - 0.72D * Math.Sin(fraction * Math.PI * 2D) / (Math.PI * 2D);
                double angle = (Math.Floor(turn) + eased) * Math.PI * 2D - Math.PI / 2D;
                double fade = Math.Min(1D, Math.Min(local / 0.12D, (4D - local) / 0.12D));
                float x = cx + (float)Math.Cos(angle) * radius;
                float y = cy + (float)Math.Sin(angle) * radius;
                float dot = 2F * scale;
                using (var brush = new SolidBrush(Color.FromArgb((int)(255 * Math.Max(0D, fade)), Color.White)))
                    g.FillEllipse(brush, x - dot, y - dot, dot * 2, dot * 2);
            }
        }
    }

    internal sealed class UpdateScreenMode
    {
        public readonly string DisplayName;
        public readonly string Id;
        public readonly string Line1;
        public readonly string Line2;

        private UpdateScreenMode(string id, string displayName, string line1, string line2)
        {
            Id = id;
            DisplayName = displayName;
            Line1 = line1;
            Line2 = line2;
        }

        public static UpdateScreenMode[] All()
        {
            return new[]
            {
                new UpdateScreenMode("update", "正在更新", "更新正在进行中。", "请不要关机。"),
                new UpdateScreenMode("restart", "正在重启", "正在重启", ""),
                new UpdateScreenMode("prepare", "准备 Windows", "正在准备 Windows", "请不要关闭电脑。")
            };
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal static class AppIcon
    {
        private static Icon cachedIcon;
        private static Image cachedImage;

        public static Icon Load()
        {
            if (cachedIcon != null)
            {
                return cachedIcon;
            }

            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fish.ico");
                if (File.Exists(path))
                {
                    cachedIcon = new Icon(path);
                    return cachedIcon;
                }
            }
            catch
            {
            }

            try
            {
                Icon executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (executableIcon != null)
                {
                    cachedIcon = executableIcon;
                    return cachedIcon;
                }
            }
            catch
            {
            }

            cachedIcon = SystemIcons.Application;
            return cachedIcon;
        }

        public static Image LoadImage()
        {
            if (cachedImage != null)
            {
                return cachedImage;
            }

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FishLogo"))
            {
                if (stream != null)
                    using (Image image = Image.FromStream(stream))
                        cachedImage = new Bitmap(image);
            }
            if (cachedImage != null) return cachedImage;

            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fish-icon.png");
                if (File.Exists(path))
                {
                    cachedImage = Image.FromFile(path);
                    return cachedImage;
                }
            }
            catch
            {
            }

            try
            {
                cachedImage = Load().ToBitmap();
                return cachedImage;
            }
            catch
            {
                return null;
            }
        }
    }
}
