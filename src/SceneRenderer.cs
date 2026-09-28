using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WindowsFish
{
    internal sealed class SceneLayout
    {
        internal RectangleF FirstText;
        internal RectangleF SecondText;
        internal Rectangle Spinner;
        internal float FontSize;
        internal float Scale;
        internal PointF Center;
    }

    internal static class SceneRenderer
    {
        internal static Color Background(Preferences options)
        {
            string palette = options.Palette == "auto" ? (options.Scene == "win10" ? "blue" : "black") : options.Palette;
            return palette == "blue" ? Color.FromArgb(0, 120, 215) : palette == "light" ? Color.FromArgb(245, 246, 248) : Color.Black;
        }

        internal static SceneLayout Draw(Graphics g, Size size, UpdateScreenMode mode, Font requested, Color ink, double seconds)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float scale = Math.Max(0.1F, Math.Min(g.DpiY / 96F, Math.Min(size.Width, size.Height) / 160F));
            float margin = 16 * scale;
            float width = Math.Max(1, size.Width - margin * 2);
            float gap = 6 * scale;
            float spinnerSize = 44 * scale;
            float separation = 24 * scale;
            bool second = !string.IsNullOrEmpty(mode.Line2);
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
            {
                float points = requested.SizeInPoints;
                Font font = null;
                float firstHeight = 0, secondHeight = 0, total = 0;
                try
                {
                    // Measure wrapped text first; shrink only when the complete group cannot fit vertically.
                    for (int i = 0; i < 30; i++)
                    {
                        if (font != null) font.Dispose();
                        font = new Font(requested.FontFamily, points, FontStyle.Regular, GraphicsUnit.Point);
                        firstHeight = (float)Math.Ceiling(g.MeasureString(mode.Line1, font, (int)width, format).Height);
                        secondHeight = second ? (float)Math.Ceiling(g.MeasureString(mode.Line2, font, (int)width, format).Height) : 0;
                        total = spinnerSize + separation + firstHeight + (second ? gap + secondHeight : 0);
                        if (total <= size.Height - margin * 2 || points <= 4) break;
                        points = Math.Max(4, points * 0.9F);
                    }
                    float top = Math.Max(0, (size.Height - total) / 2);
                    var layout = new SceneLayout { FontSize = points, Scale = scale, Center = new PointF(size.Width / 2F, top + spinnerSize / 2),
                        Spinner = Rectangle.Ceiling(new RectangleF((size.Width - spinnerSize) / 2, top, spinnerSize, spinnerSize)),
                        FirstText = new RectangleF(margin, top + spinnerSize + separation, width, firstHeight) };
                    layout.SecondText = new RectangleF(margin, layout.FirstText.Bottom + gap, width, secondHeight);
                    DrawDots(g, size.Width / 2F, top + spinnerSize / 2, scale, ink, seconds);
                    using (var brush = new SolidBrush(ink))
                    {
                        g.DrawString(mode.Line1, font, brush, layout.FirstText, format);
                        if (second) g.DrawString(mode.Line2, font, brush, layout.SecondText, format);
                    }
                    return layout;
                }
                finally { if (font != null) font.Dispose(); }
            }
        }

        internal static void DrawDots(Graphics g, float cx, float cy, float scale, Color ink, double seconds)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < 5; i++)
            {
                double local = (seconds + 4.8 - i * 0.12) % 4.8;
                if (local > 4) continue;
                double turn = local / 2;
                double fraction = turn - Math.Floor(turn);
                double eased = fraction - 0.72 * Math.Sin(fraction * Math.PI * 2) / (Math.PI * 2);
                double angle = (Math.Floor(turn) + eased) * Math.PI * 2 - Math.PI / 2;
                double fade = Math.Min(1, Math.Min(local / 0.12, (4 - local) / 0.12));
                float x = cx + (float)Math.Cos(angle) * 18 * scale;
                float y = cy + (float)Math.Sin(angle) * 18 * scale;
                float dot = 2 * scale;
                using (var brush = new SolidBrush(Color.FromArgb((int)(255 * Math.Max(0, fade)), ink)))
                    g.FillEllipse(brush, x - dot, y - dot, dot * 2, dot * 2);
            }
        }
    }

    internal sealed class SceneChoice : RadioButton
    {
        private readonly string scene;
        private Bitmap thumbnail;
        internal SceneChoice(string scene)
        {
            this.scene = scene;
            Text = scene == "win10" ? "Windows 10" : "Windows 11";
            Appearance = Appearance.Button;
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal void SetPreview(UpdateScreenMode mode, Preferences preferences)
        {
            var options = preferences.Clone();
            options.Scene = scene;
            var bitmap = new Bitmap(520, 300);
            bitmap.SetResolution(96, 96);
            using (var g = Graphics.FromImage(bitmap))
            using (var font = new Font("Microsoft YaHei UI", new[] { 12F, 15F, 18F }[options.TextSize]))
            {
                g.Clear(SceneRenderer.Background(options));
                SceneRenderer.Draw(g, bitmap.Size, mode.ForOptions(options), font,
                    options.Palette == "light" ? Color.FromArgb(30, 33, 38) : Color.White, 1.1);
            }
            var old = thumbnail;
            thumbnail = bitmap;
            if (old != null) old.Dispose();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Checked ? Color.FromArgb(227, 244, 241) : Color.White);
            int margin = Math.Max(4, (int)(8 * e.Graphics.DpiX / 96));
            int height = Math.Max(1, Height - margin * 2);
            int width = Math.Min(Width / 2, height * 520 / 300);
            if (thumbnail != null) e.Graphics.DrawImage(thumbnail, new Rectangle(margin, margin, width, height));
            var text = new Rectangle(width + margin * 2, 0, Math.Max(1, Width - width - margin * 3), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, text, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            using (var pen = new Pen(Checked ? Color.FromArgb(0, 111, 116) : Color.FromArgb(198, 198, 198), Checked ? 2 : 1))
                e.Graphics.DrawRectangle(pen, 1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -margin, -margin));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && thumbnail != null) { thumbnail.Dispose(); thumbnail = null; }
            base.Dispose(disposing);
        }
    }
}
