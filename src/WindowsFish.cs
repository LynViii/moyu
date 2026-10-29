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
        private static int Main(string[] args)
        {
            if (args.Length == 3 && args[0] == "--apply-update") return UpdateInstaller.RunHelper(args[1], args[2]);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { new SingleInstanceApp().Run(UpdateInstaller.ConsumeRestartArguments(args)); return 0; }
            catch (Exception error) { MessageBox.Show("无法启动摸鱼：" + error.Message, "摸鱼", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
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
            form.StartupArguments = new List<string>(CommandLineArgs).ToArray();
        }

        protected override void OnStartupNextInstance(StartupNextInstanceEventArgs e)
        {
            base.OnStartupNextInstance(e);
            e.BringToForeground = false;
            var form = (IntroForm)MainForm;
            string[] args = new List<string>(e.CommandLine).ToArray();
            if (Array.IndexOf(args, "--silent") < 0 && Array.IndexOf(args, "--exit") < 0) form.Reveal();
            form.ApplyArguments(args);
        }
    }

    internal sealed class Preferences
    {
        internal string Mode = "update";
        internal bool BlackoutSecondary = true;
        internal int Speed = 1;
        internal int DelaySeconds = 3;
        internal bool CloseToTray = true;
        internal bool StartWithWindows;
        internal int Hotkey = 1;
        internal bool DismissStartMenu = true;
        internal int StartMenuSeconds = 3;
        internal string Language = "zh";
        internal string Monitor = "";
        internal bool EcoMode;
        internal string Scene = "win11";
        internal string Palette = "auto";
        internal int TextSize = 1;
        internal string ExitPattern = "27";
        internal static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Moyu", "settings.xml");

        internal static Preferences Load(string path)
        {
            var settings = new Preferences();
            try
            {
                var root = SettingsFiles.Read(path).Root;
                string mode = (string)root.Attribute("mode");
                int speed;
                int seconds;
                int textSize;
                string scene = (string)root.Attribute("scene");
                string palette = (string)root.Attribute("palette");
                Keys[] exitKeys;
                string exitPattern = (string)root.Attribute("exitPattern");
                if (ExitBinding.TryParse(exitPattern, out exitKeys)) settings.ExitPattern = exitPattern;
                if (scene == "win10" || scene == "win11") settings.Scene = scene;
                if (palette == "auto" || palette == "black" || palette == "blue" || palette == "light") settings.Palette = palette;
                if (int.TryParse((string)root.Attribute("textSize"), out textSize) && textSize >= 0 && textSize <= 2) settings.TextSize = textSize;
                bool eco;
                bool option;
                int selection;
                if (bool.TryParse((string)root.Attribute("closeToTray"), out option)) settings.CloseToTray = option;
                if (bool.TryParse((string)root.Attribute("startWithWindows"), out option)) settings.StartWithWindows = option;
                if (bool.TryParse((string)root.Attribute("dismissStartMenu"), out option)) settings.DismissStartMenu = option;
                if (int.TryParse((string)root.Attribute("hotkey"), out selection) && selection >= 0 && selection <= 3) settings.Hotkey = selection;
                if (int.TryParse((string)root.Attribute("startMenuSeconds"), out selection) && selection >= 1 && selection <= 30) settings.StartMenuSeconds = selection;
                if (int.TryParse((string)root.Attribute("delaySeconds"), out seconds) && seconds >= 1 && seconds <= 60) settings.DelaySeconds = seconds;
                if ((string)root.Attribute("language") == "en") settings.Language = "en";
                settings.Monitor = (string)root.Attribute("monitor") ?? "";
                if (bool.TryParse((string)root.Attribute("ecoMode"), out eco)) settings.EcoMode = eco;
                if (int.TryParse((string)root.Attribute("speed"), out speed) && speed >= 0 && speed <= 2) settings.Speed = speed;
                bool blackout;
                if (bool.TryParse((string)root.Attribute("blackoutSecondary"), out blackout)) settings.BlackoutSecondary = blackout;
                if (mode == "update" || mode == "restart" || mode == "prepare") settings.Mode = mode;
            }
            catch (Exception error) { Trace.WriteLine(error.Message); }
            return settings;
        }

        internal bool Save(string path)
        {
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                new XDocument(ToXml()).Save(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception error) { Trace.WriteLine(error.Message); return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception error) { Trace.WriteLine(error.Message); }
            }
        }

        internal XElement ToXml()
        {
            return new XElement("preferences", new XAttribute("schema", 3), new XAttribute("mode", Mode), new XAttribute("exitPattern", ExitPattern),
                new XAttribute("blackoutSecondary", BlackoutSecondary), new XAttribute("speed", Speed),
                new XAttribute("closeToTray", CloseToTray), new XAttribute("startWithWindows", StartWithWindows),
                new XAttribute("hotkey", Hotkey), new XAttribute("dismissStartMenu", DismissStartMenu), new XAttribute("startMenuSeconds", StartMenuSeconds),
                new XAttribute("delaySeconds", DelaySeconds), new XAttribute("language", Language),
                new XAttribute("monitor", Monitor), new XAttribute("ecoMode", EcoMode),
                new XAttribute("scene", Scene), new XAttribute("palette", Palette), new XAttribute("textSize", TextSize));
        }

        internal Preferences Clone() { return (Preferences)MemberwiseClone(); }
    }

    internal sealed partial class IntroForm : Form
    {
        private FishMode fishMode;
        private UpdateScreenMode selectedMode;
        private Button selectedModeButton;
        private Label selectedModeLabel;
        private NumericUpDown delaySeconds;
        private CheckBox blackoutSecondary;
        private UpdateForm previewWindow;
        private ComboBox speedChoice;
        private RadioButton win11;
        private RadioButton win10;
        private Button startButton;
        private Timer launchTimer;
        private Stopwatch launchClock;
        private readonly Stopwatch sessionClock = new Stopwatch();
        private Label sessionSummary;
        private Label saveWarning;
        private Panel content;
        private Panel footer;
        private float layoutScale = 1F;
        internal Control.ControlCollection ContentControls { get { return content == null ? Controls : content.Controls; } }
        private double SpeedFactor { get { return new[] { 0.75D, 1D, 1.5D }[speedChoice.SelectedIndex]; } }
        private static readonly Color Accent = Color.FromArgb(0, 111, 116);
        private Preferences preferences;
        private readonly string settingsPath;
        private bool ready;

        public IntroForm() : this(Preferences.FilePath, true) { }

        internal IntroForm(string settingsPath) : this(settingsPath, false) { }

        internal IntroForm(string settingsPath, bool desktopEnabled)
        {
            this.desktopEnabled = desktopEnabled;
            this.settingsPath = settingsPath;
            preferences = Preferences.Load(settingsPath);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.Sizable;
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
            BuildScrollableLayout();
            FitToWorkArea(Screen.FromControl(this).WorkingArea);
            ready = true;
            if (desktopEnabled) InitializeDesktop();
        }

        internal string[] StartupArguments = new string[0];

        protected override void SetVisibleCore(bool value)
        {
            if (!startupHandled && value && (Array.IndexOf(StartupArguments, "--silent") >= 0 || Array.IndexOf(StartupArguments, "--exit") >= 0))
            {
                startupHandled = true;
                if (!IsHandleCreated) CreateHandle();
                base.SetVisibleCore(false);
                ApplyArguments(StartupArguments);
                UpdateInstaller.MarkReady();
                return;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!startupHandled) { startupHandled = true; ApplyArguments(StartupArguments); }
            UpdateInstaller.MarkReady();
            if (!string.IsNullOrEmpty(UpdateInstaller.StartupFailure)) saveWarning.Text = UpdateInstaller.StartupFailure;
        }

        public void Reveal()
        {
            if (launchPrompt != null) { launchPrompt.Activate(); NativeDesktop.Foreground(launchPrompt.Handle); return; }
            if (settingsWindow != null) { settingsWindow.Activate(); NativeDesktop.Foreground(settingsWindow.Handle); return; }
            if (updateWindow != null) { updateWindow.Activate(); NativeDesktop.Foreground(updateWindow.Handle); return; }
            if (previewWindow != null && !previewWindow.IsDisposed) { previewWindow.Activate(); return; }
            if (fishMode != null) { fishMode.ActivatePrimary(); return; }
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Rectangle area = Screen.FromControl(this).WorkingArea;
            FitToWorkArea(area);
            if (!area.Contains(Bounds)) Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2),
                area.Top + Math.Max(0, (area.Height - Height) / 2));
            Activate();
        }

        internal void ApplyArguments(string[] args)
        {
            if (Array.IndexOf(args, "--exit") >= 0) { ExitApplication(); return; }
            if (args.Length == 1 && args[0] == "--silent") return;
            if (args.Length == 0 || fishMode != null || previewWindow != null) return;
            string mode = selectedMode.Id;
            int ignoredMinutes;
            int speed = speedChoice.SelectedIndex;
            bool start = false;
            bool primaryOnly = false;
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string option = args[i];
                    if (option == "--start") { start = true; continue; }
                    if (option == "--silent") continue;
                    if (option == "--primary-only") { primaryOnly = true; continue; }
                    if (++i >= args.Length) throw new ArgumentException("参数缺少取值：" + option);
                    string value = args[i];
                    if (option == "--mode" && (value == "update" || value == "restart" || value == "prepare")) mode = value;
                    else if (option == "--minutes" && int.TryParse(value, out ignoredMinutes)) { }
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
                foreach (Control control in ContentControls)
                {
                    var button = control as Button;
                    var state = button == null ? null : button.Tag as UpdateScreenMode;
                    if (state != null && state.Id == mode) SelectMode(state, button);
                }
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
            preferences.BlackoutSecondary = blackoutSecondary.Checked;
            preferences.Speed = speedChoice.SelectedIndex;
            preferences.DelaySeconds = (int)delaySeconds.Value;
            bool saved = preferences.Save(settingsPath);
            if (saveWarning != null)
                saveWarning.Text = saved ? "" : "本次已生效，但未能记住设置。";
        }

        private void ApplyLayoutScale(float scale)
        {
            layoutScale = scale;
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
            ClientSize = new Size((int)Math.Round(720 * scale), (int)Math.Round(600 * scale));
            ResumeLayout(false);
        }

        private void BuildScrollableLayout()
        {
            var controls = new List<Control>();
            foreach (Control control in Controls) controls.Add(control);
            content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            footer = new Panel { Dock = DockStyle.Bottom, Height = (int)(110 * layoutScale) };
            foreach (Control control in controls)
                if (control != startButton && control != sessionSummary) content.Controls.Add(control);
            footer.Controls.Add(startButton);
            footer.Controls.Add(sessionSummary);
            saveWarning = new Label { ForeColor = Color.FromArgb(160, 45, 40), TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
            footer.Controls.Add(saveWarning);
            content.AutoScrollMinSize = new Size((int)(710 * layoutScale), (int)(490 * layoutScale));
            Controls.Add(content);
            Controls.Add(footer);
            footer.SizeChanged += delegate { LayoutFooter(); };
            sessionSummary.TextChanged += delegate { LayoutFooter(); };
            saveWarning.TextChanged += delegate { LayoutFooter(); };
            LayoutFooter();
        }

        private void LayoutFooter()
        {
            int logicalHeight = !string.IsNullOrEmpty(saveWarning.Text) ? 110 : !string.IsNullOrEmpty(sessionSummary.Text) ? 82 : 62;
            footer.Height = (int)(logicalHeight * layoutScale);
            int margin = (int)(16 * layoutScale);
            int width = Math.Max(1, footer.ClientSize.Width - margin * 2);
            int buttonWidth = Math.Min((int)(220 * layoutScale), width);
            startButton.SetBounds((footer.ClientSize.Width - buttonWidth) / 2, (int)(8 * layoutScale), buttonWidth, (int)(42 * layoutScale));
            sessionSummary.SetBounds(margin, (int)(54 * layoutScale), width, (int)(24 * layoutScale));
            saveWarning.SetBounds(margin, (int)(80 * layoutScale), width, (int)(24 * layoutScale));
        }

        internal void FitToWorkArea(Rectangle area)
        {
            MinimumSize = new Size(Math.Min((int)(400 * layoutScale), area.Width), Math.Min((int)(320 * layoutScale), area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)),
                Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
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
            title.SetBounds(110, 30, 160, 48);
            Controls.Add(title);

            var description = new Label();
            description.Text = AppVersion.Value;
            description.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            description.ForeColor = Color.FromArgb(70, 70, 70);
            description.AutoSize = false;
            description.TextAlign = ContentAlignment.MiddleRight;
            description.SetBounds(550, 40, 120, 24);
            Controls.Add(description);
            var checkUpdate = new Button { Text = "检查更新", FlatStyle = FlatStyle.Flat };
            checkUpdate.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 219);
            checkUpdate.SetBounds(300, 35, 106, 34);
            checkUpdate.Click += delegate { OpenUpdateDialog(); };
            Controls.Add(checkUpdate);
            var more = new Button();
            more.Text = "更多设置";
            more.FlatStyle = FlatStyle.Flat;
            more.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 219);
            more.SetBounds(420, 35, 116, 34);
            more.Click += delegate { OpenSettings(); };
            Controls.Add(more);
            var separator = new Label();
            separator.BackColor = Color.FromArgb(224, 229, 231);
            separator.SetBounds(48, 102, 624, 1);
            Controls.Add(separator);

            var modeLabel = new Label();
            modeLabel.Text = "系统场景";
            modeLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            modeLabel.ForeColor = Color.FromArgb(48, 48, 48);
            modeLabel.AutoSize = false;
            modeLabel.TextAlign = ContentAlignment.MiddleLeft;
            modeLabel.SetBounds(48, 116, 180, 28);
            Controls.Add(modeLabel);

            win11 = new SceneChoice("win11");
            win10 = new SceneChoice("win10");
            win11.SetBounds(48, 150, 306, 92);
            win10.SetBounds(366, 150, 306, 92);
            Controls.Add(win11);
            Controls.Add(win10);
            win11.Checked = preferences.Scene == "win11";
            win10.Checked = preferences.Scene == "win10";
            win11.CheckedChanged += delegate { if (win11.Checked) SelectScene("win11"); };
            win10.CheckedChanged += delegate { if (win10.Checked) SelectScene("win10"); };
            StyleScenes();

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

            var delayLabel = new Label { Text = "开始倒计时（秒）", TextAlign = ContentAlignment.MiddleLeft };
            delayLabel.SetBounds(48, 270, 280, 30);
            Controls.Add(delayLabel);
            delaySeconds = new NumericUpDown { Minimum = 1, Maximum = 60, Value = preferences.DelaySeconds };
            delaySeconds.SetBounds(510, 270, 162, 30);
            delaySeconds.ValueChanged += delegate { CancelLaunch(); SavePreferences(); };
            Controls.Add(delaySeconds);

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

            desktopStatus = new Label { ForeColor = Color.FromArgb(72, 72, 72), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            desktopStatus.SetBounds(48, 358, 430, 30);
            Controls.Add(desktopStatus);

            speedChoice = new ComboBox();
            speedChoice.DropDownStyle = ComboBoxStyle.DropDownList;
            speedChoice.Items.AddRange(new object[] { "动画 · 慢速", "动画 · 标准", "动画 · 快速" });
            speedChoice.SelectedIndex = preferences.Speed;
            speedChoice.SetBounds(510, 358, 162, 30);
            speedChoice.SelectedIndexChanged += delegate { SavePreferences(); };
            Controls.Add(speedChoice);

            startButton = new Button();
            if (startButton != null) startButton.Text = "开始摸鱼";
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
            foreach (Control c in Controls)
                if (c != win11 && c != win10 && c.Top >= 160 && c != startButton && c != sessionSummary)
                    c.Top += 90;
        }

        private void ShowPreview()
        {
            CancelLaunch();
            using (var preview = new UpdateForm(null, selectedMode.ForOptions(preferences), SpeedFactor, preferences))
            {
                preview.SetEcoMode(preferences.EcoMode);
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
                Rectangle area = Screen.FromControl(this).WorkingArea;
                preview.Size = new Size(Math.Min(preview.Width, area.Width), Math.Min(preview.Height, area.Height));
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

            var localized = selectedMode.ForOptions(preferences);
            string preview = localized.Line1;
            if (!string.IsNullOrEmpty(localized.Line2))
            {
                preview += "  " + localized.Line2;
            }

            selectedModeLabel.Text = preview;
            selectedModeLabel.AutoEllipsis = true;
            foreach (var choice in new[] { win11 as SceneChoice, win10 as SceneChoice })
                if (choice != null) choice.SetPreview(selectedMode, preferences);
        }

        private void StartButtonClick(object sender, EventArgs e)
        {
            if (launchTimer != null) { CancelLaunch(); return; }
            StartLaunch(false);
        }

        internal void StartLaunch(bool showPrompt)
        {
            if (fishMode != null) { fishMode.ActivatePrimary(); return; }
            if (previewWindow != null || launchTimer != null || settingsWindow != null || updateWindow != null) return;
            launchClock = Stopwatch.StartNew();
            startButton.Text = "取消开始 · " + preferences.DelaySeconds;
            launchTimer = new Timer();
            launchTimer.Interval = 100;
            launchTimer.Tick += delegate
            {
                int remaining = Math.Max(0, preferences.DelaySeconds - (int)launchClock.Elapsed.TotalSeconds);
                startButton.Text = "取消开始 · " + remaining;
                if (launchPrompt != null) launchPrompt.SetRemaining(remaining);
                if (remaining == 0) { CancelLaunch(); BeginSession(); }
            };
            launchTimer.Start();
            if (showPrompt)
            {
                launchPrompt = new LaunchPrompt(preferences.Scene, selectedMode.DisplayName, preferences.DelaySeconds, preferences.ExitPattern);
                launchPrompt.CancelRequested += delegate { CancelLaunch(); };
                launchPrompt.StartRequested += delegate { CancelLaunch(); BeginSession(); };
                launchPrompt.Show();
                NativeDesktop.Foreground(launchPrompt.Handle);
            }
            UpdateTrayState();
        }

        private void CancelLaunch()
        {
            if (launchTimer != null) { launchTimer.Stop(); launchTimer.Dispose(); launchTimer = null; }
            if (launchPrompt != null) { var prompt = launchPrompt; launchPrompt = null; prompt.Dispose(); }
            startButton.Text = "开始摸鱼";
            UpdateTrayState();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && launchTimer != null) { CancelLaunch(); return true; }
            if (HandleShortcut(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        internal bool HandleShortcut(Keys keys)
        {
            if (fishMode != null || previewWindow != null) return false;
            if (keys == (Keys.Control | Keys.P)) { ShowPreview(); return true; }
            if (keys == (Keys.Control | Keys.Enter)) { StartButtonClick(this, EventArgs.Empty); return true; }
            if (keys == (Keys.Control | Keys.Oemcomma)) { OpenSettings(); return true; }
            int index = keys == (Keys.Control | Keys.D1) ? 0 : keys == (Keys.Control | Keys.D2) ? 1 : keys == (Keys.Control | Keys.D3) ? 2 : -1;
            if (index < 0) return false;
            string id = UpdateScreenMode.All()[index].Id;
            foreach (Control c in ContentControls)
            {
                var b = c as Button;
                var state = b == null ? null : b.Tag as UpdateScreenMode;
                if (state != null && state.Id == id) { b.PerformClick(); return true; }
            }
            return false;
        }

        private void OpenSettings()
        {
            CancelLaunch();
            using (var dialog = new SettingsDialog(preferences))
            {
                settingsWindow = dialog;
                try { if (dialog.ShowDialog(this) == DialogResult.OK) ApplySettings(dialog.Result); }
                finally { settingsWindow = null; }
            }
        }

        internal void ApplySettings(Preferences settings)
        {
            CancelLaunch();
            if (desktopEnabled && !ApplyStartupSetting(settings)) return;
            preferences = settings.Clone();
            ready = false;
            try
            {
                foreach (Control c in ContentControls)
                {
                    var button = c as Button;
                    var state = button == null ? null : button.Tag as UpdateScreenMode;
                    if (state != null && state.Id == preferences.Mode) SelectMode(state, button);
                }
                win11.Checked = preferences.Scene == "win11";
                win10.Checked = preferences.Scene == "win10";
                blackoutSecondary.Checked = preferences.BlackoutSecondary;
                speedChoice.SelectedIndex = preferences.Speed;
                delaySeconds.Value = preferences.DelaySeconds;
                UpdateSelectedModeLabel();
            }
            finally { ready = true; }
            SavePreferences();
            ConfigureHotkey();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CancelLaunch();
            DisposeDesktop();
            base.OnFormClosed(e);
        }

        private void BeginSession()
        {
            returnToTray = !Visible;
            fishMode = new FishMode(this, selectedMode.ForOptions(preferences), blackoutSecondary.Checked, SpeedFactor, preferences);
            sessionClock.Restart();
            Hide();
            try
            {
                fishMode.Start();
                UpdateTrayState();
            }
            catch (Exception error)
            {
                fishMode.Stop();
                MessageBox.Show(this, error.Message, "无法进入摸鱼状态", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectScene(string scene)
        {
            preferences.Scene = scene;
            StyleScenes();
            if (!ready) return;
            CancelLaunch();
            UpdateSelectedModeLabel();
            SavePreferences();
        }

        private void StyleScenes()
        {
            foreach (var button in new[] { win11, win10 })
            {
                button.FlatAppearance.BorderColor = button.Checked ? Accent : Color.FromArgb(198, 198, 198);
                button.FlatAppearance.CheckedBackColor = Color.FromArgb(227, 244, 241);
                button.ForeColor = button.Checked ? Accent : Color.FromArgb(38, 38, 38);
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
            UpdateTrayState();
            if (!IsDisposed && !Disposing && !exiting)
            {
                if (returnToTray && desktopEnabled) HideToTray();
                else Reveal();
            }
        }
    }

    internal sealed class FishMode : IMessageFilter
    {
        private readonly IntroForm owner;
        private readonly UpdateScreenMode screenMode;
        private readonly List<Form> windows = new List<Form>();
        private bool closing;
        private StartMenuGuard startMenuGuard;
        private Stopwatch elapsed;
        private bool cursorHidden;
        private bool returned;
        private bool rebuilding;
        private bool displaySubscribed;
        private Timer displayTimer;
        private readonly bool blackoutSecondary;
        private readonly double speedFactor;
        private readonly Preferences displayOptions;
        private readonly ExitMatcher exitMatcher;
        private bool filterAttached;
        internal Screen TargetScreen { get { return MonitorSelection.Resolve(Screen.AllScreens, displayOptions.Monitor); } }
        internal double AnimationSeconds { get { return elapsed == null ? 0D : elapsed.Elapsed.TotalSeconds; } }

        public FishMode(IntroForm owner, UpdateScreenMode screenMode, bool blackoutSecondary, double speedFactor, Preferences options)
        {
            displayOptions = options.Clone();
            exitMatcher = new ExitMatcher(displayOptions.ExitPattern);
            this.owner = owner;
            this.screenMode = screenMode;
            this.blackoutSecondary = blackoutSecondary;
            this.speedFactor = speedFactor;
        }

        public void Start()
        {
            Cursor.Hide();
            cursorHidden = true;
            elapsed = Stopwatch.StartNew();
            Application.AddMessageFilter(this);
            filterAttached = true;

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
            if (displayOptions.DismissStartMenu)
            {
                startMenuGuard = new StartMenuGuard(displayOptions.StartMenuSeconds);
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
            exitMatcher.Reset();
            rebuilding = true;
            try
            {
            foreach (Form old in windows.ToArray()) old.Close();
            windows.Clear();

            Screen[] screens = Screen.AllScreens;
            Screen targetScreen = MonitorSelection.Resolve(screens, displayOptions.Monitor);
            foreach (Screen screen in screens)
            {
                bool target = screen.DeviceName == targetScreen.DeviceName;
                if (!target && !blackoutSecondary) continue;
                Form form = target ? (Form)new UpdateForm(this, screenMode, speedFactor, displayOptions) : new BlackForm(this);
                form.StartPosition = FormStartPosition.Manual;
                form.FormBorderStyle = FormBorderStyle.None;
                form.AutoScaleMode = AutoScaleMode.None;
                form.Bounds = screen.Bounds;
                form.TopMost = true;
                form.KeyPreview = true;
                form.ShowInTaskbar = target;
                form.KeyDown += FullscreenKeyDown;
                form.KeyUp += FullscreenKeyUp;
                form.Activated += delegate { exitMatcher.Reset(); };
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
                    form.Bounds = TargetScreen.Bounds;
                    form.Activate();
                    NativeDesktop.Foreground(form.Handle);
                }
        }

        public void Stop()
        {
            if (closing)
            {
                return;
            }

            closing = true;
            if (filterAttached) { Application.RemoveMessageFilter(this); filterAttached = false; }
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
            if (startMenuGuard != null)
            {
                startMenuGuard.Dispose();
                startMenuGuard = null;
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
            if (exitMatcher.KeyDown(e.KeyData, elapsed.Elapsed.TotalMilliseconds))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Stop();
            }
        }

        private void FullscreenKeyUp(object sender, KeyEventArgs e) { exitMatcher.KeyUp(e.KeyData); }

        public bool PreFilterMessage(ref Message message)
        {
            IntPtr window = message.HWnd;
            if (closing || !windows.Exists(w => w.IsHandleCreated && w.Handle == window)) return false;
            Keys key = (Keys)message.WParam.ToInt32() | Control.ModifierKeys;
            if (message.Msg == 0x0101 || message.Msg == 0x0105) { exitMatcher.KeyUp(key); return false; }
            if (message.Msg != 0x0100 && message.Msg != 0x0104) return false;
            if (exitMatcher.KeyDown(key, elapsed.Elapsed.TotalMilliseconds)) { Stop(); return true; }
            return false;
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
        private readonly AnimationTimer timer;
        private readonly UpdateScreenMode screenMode;
        private readonly FishMode mode;
        private readonly Stopwatch previewClock = Stopwatch.StartNew();
        private readonly double speedFactor;
        private Rectangle spinnerBounds;
        internal SceneLayout LastLayout;
        private readonly Font statusFont;
        private readonly Color ink;
        private readonly string scene;

        public UpdateForm(FishMode mode, UpdateScreenMode screenMode)
            : this(mode, screenMode, 1D) { }

        public UpdateForm(FishMode mode, UpdateScreenMode screenMode, double speedFactor)
            : this(mode, screenMode, speedFactor, new Preferences()) { }

        public UpdateForm(FishMode mode, UpdateScreenMode screenMode, double speedFactor, Preferences options)
        {
            statusFont = new Font("Microsoft YaHei UI", new[] { 12F, 15F, 18F }[options.TextSize], FontStyle.Regular, GraphicsUnit.Point);
            this.speedFactor = speedFactor;
            this.screenMode = screenMode;
            this.mode = mode;
            scene = options.Scene;
            BackColor = SceneRenderer.Background(options);
            ink = options.Palette == "light" ? Color.FromArgb(30, 33, 38) : Color.White;
            DoubleBuffered = true;
            Icon = AppIcon.Load();
            ResizeRedraw = true;

            timer = new AnimationTimer(this, delegate { TimerTick(this, EventArgs.Empty); });
            timer.Interval = options.EcoMode ? 50 : 15;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(BackColor);
            double seconds = (mode == null ? previewClock.Elapsed.TotalSeconds : mode.AnimationSeconds) * speedFactor;
            if (LastLayout != null && e.ClipRectangle == spinnerBounds)
            {
                SceneRenderer.DrawSpinner(e.Graphics, LastLayout.Center.X, LastLayout.Center.Y, LastLayout.Scale, ink, seconds, scene);
                return;
            }
            LastLayout = SceneRenderer.Draw(e.Graphics, ClientSize, screenMode, statusFont, ink, seconds, scene);
            spinnerBounds = LastLayout.Spinner;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (timer != null) timer.Enabled = Visible && WindowState != FormWindowState.Minimized;
        }

        protected override void OnResize(EventArgs e)
        {
            LastLayout = null;
            spinnerBounds = Rectangle.Empty;
            base.OnResize(e);
            if (timer != null) timer.Enabled = Visible && WindowState != FormWindowState.Minimized;
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
            Update();
        }

        internal void SetEcoMode(bool enabled) { timer.Interval = enabled ? 50 : 15; }

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

        internal UpdateScreenMode Localize(string language)
        {
            if (language != "en") return this;
            if (Id == "restart") return new UpdateScreenMode(Id, DisplayName, "Restarting", "");
            if (Id == "prepare") return new UpdateScreenMode(Id, DisplayName, "Getting Windows ready", "Don't turn off your computer");
            return new UpdateScreenMode(Id, DisplayName, "Updates are underway.", "Please keep your computer on.");
        }

        internal UpdateScreenMode ForOptions(Preferences options)
        {
            if (options.Scene == "win10" && Id == "update")
                return options.Language == "en"
                    ? new UpdateScreenMode(Id, DisplayName, "Working on updates", "Don't turn off your computer")
                    : new UpdateScreenMode(Id, DisplayName, "正在处理更新", "请不要关闭电脑。");
            return Localize(options.Language);
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
