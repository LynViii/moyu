using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace WindowsFish
{
    internal static class ExitBinding
    {
        internal static readonly string[] Presets = { "27", "27;27", "38;38;40;40;37;39;37;39", "393297" };

        internal static bool TryParse(string text, out Keys[] keys)
        {
            keys = null;
            if (string.IsNullOrEmpty(text) || text.Length > 160) return false;
            var result = new List<Keys>();
            foreach (string part in text.Split(';'))
            {
                int number;
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out number)) return false;
                Keys key = (Keys)number;
                if (!Allowed(key)) return false;
                result.Add(key);
            }
            if (result.Count == 0 || result.Count > 12) return false;
            if (result.Count == 1 && result[0] != Keys.Escape && (result[0] & (Keys.Control | Keys.Alt)) == 0) return false;
            keys = result.ToArray();
            return true;
        }

        internal static bool Allowed(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            if (((int)key & ~((int)Keys.KeyCode | (int)Keys.Modifiers)) != 0 || (key & Keys.Modifiers & ~(Keys.Control | Keys.Alt | Keys.Shift)) != 0) return false;
            bool basic = code == Keys.Escape || code == Keys.Enter || code == Keys.Space || code == Keys.Back || code == Keys.Tab ||
                (code >= Keys.PageUp && code <= Keys.Down) || (code >= Keys.Insert && code <= Keys.Delete) ||
                (code >= Keys.D0 && code <= Keys.Z) || (code >= Keys.NumPad0 && code <= Keys.F12);
            if (!basic) return false;
            if ((key & Keys.Alt) != 0 && (code == Keys.F4 || code == Keys.Tab || code == Keys.Escape || code == Keys.Space)) return false;
            if ((key & Keys.Control) != 0 && code == Keys.Escape) return false;
            return true;
        }

        internal static string Format(string text)
        {
            Keys[] keys;
            if (!TryParse(text, out keys)) return "Esc";
            return string.Join("  ", keys.Select(FormatKey).ToArray());
        }

        internal static string FormatKey(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            string label = code == Keys.Up ? "↑" : code == Keys.Down ? "↓" : code == Keys.Left ? "←" : code == Keys.Right ? "→" : code == Keys.Escape ? "Esc" : code.ToString();
            return ((key & Keys.Control) != 0 ? "Ctrl+" : "") + ((key & Keys.Alt) != 0 ? "Alt+" : "") + ((key & Keys.Shift) != 0 ? "Shift+" : "") + label;
        }

        internal static Keys LaunchKey(int selection)
        { return selection == 1 ? Keys.Alt | Keys.L : selection == 2 ? Keys.Control | Keys.Alt | Keys.L : selection == 3 ? Keys.Control | Keys.Shift | Keys.L : Keys.None; }
    }

    internal sealed class ExitMatcher
    {
        private readonly Keys[] sequence;
        private readonly int[] prefix;
        private readonly HashSet<Keys> pressed = new HashSet<Keys>();
        private int position;
        private double last;

        internal ExitMatcher(string binding)
        {
            if (!ExitBinding.TryParse(binding, out sequence)) sequence = new[] { Keys.Escape };
            prefix = new int[sequence.Length];
            for (int i = 1, j = 0; i < sequence.Length; i++)
            {
                while (j > 0 && sequence[i] != sequence[j]) j = prefix[j - 1];
                if (sequence[i] == sequence[j]) j++;
                prefix[i] = j;
            }
        }

        internal bool KeyDown(Keys key, double milliseconds)
        {
            if (!ExitBinding.Allowed(key) || !pressed.Add(key & Keys.KeyCode)) return false;
            if (milliseconds - last > 1500) position = 0;
            last = milliseconds;
            while (position > 0 && sequence[position] != key) position = prefix[position - 1];
            if (sequence[position] == key) position++;
            if (position != sequence.Length) return false;
            position = 0;
            return true;
        }

        internal void KeyUp(Keys key) { pressed.Remove(key & Keys.KeyCode); }
        internal void Reset() { position = 0; pressed.Clear(); }
    }

    internal sealed class ExitRecorder : TextBox
    {
        private readonly List<Keys> keys = new List<Keys>();
        private readonly HashSet<Keys> pressed = new HashSet<Keys>();
        internal bool Recording;
        internal string Binding { get { return string.Join(";", keys.Select(k => ((int)k).ToString(CultureInfo.InvariantCulture)).ToArray()); } }
        internal ExitRecorder() { ReadOnly = true; Dock = DockStyle.Fill; }
        internal void Begin() { keys.Clear(); pressed.Clear(); Text = ""; Recording = true; Focus(); }
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (!Recording) return base.ProcessCmdKey(ref message, keyData);
            CaptureKey(keyData);
            return true;
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Recording) { base.OnKeyDown(e); return; }
            CaptureKey(e.KeyData); e.SuppressKeyPress = true;
        }
        private void CaptureKey(Keys key)
        {
            if (!ExitBinding.Allowed(key) || !pressed.Add(key & Keys.KeyCode) || keys.Count >= 12) return;
            keys.Add(key); Text = string.Join("  ", keys.Select(ExitBinding.FormatKey).ToArray());
        }
        protected override void OnKeyUp(KeyEventArgs e) { pressed.Remove(e.KeyCode); base.OnKeyUp(e); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0101 || message.Msg == 0x0105) pressed.Remove((Keys)message.WParam.ToInt32() & Keys.KeyCode);
            base.WndProc(ref message);
        }
    }
}
