using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WindowsFish
{
    internal static class NativeDesktop
    {
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)] private struct Input { internal uint Type; internal InputUnion Data; }
        [StructLayout(LayoutKind.Explicit)] private struct InputUnion
        {
            [FieldOffset(0)] internal KeyboardInput Keyboard;
            [FieldOffset(0)] internal MouseInput Mouse;
        }
        [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput
        { internal ushort Key, Scan; internal uint Flags, Time; internal UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct MouseInput
        { internal int X, Y; internal uint Data, Flags, Time; internal UIntPtr Extra; }

        internal static void Foreground(IntPtr window) { if (window != IntPtr.Zero) SetForegroundWindow(window); }

        internal static IntPtr StartMenuForeground()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                uint id;
                GetWindowThreadProcessId(window, out id);
                using (var process = Process.GetProcessById((int)id))
                {
                    if (process.ProcessName.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase)) return window;
                    if (process.ProcessName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
                    {
                        var caption = new StringBuilder(128);
                        GetWindowText(window, caption, caption.Capacity);
                        if (caption.ToString() == "Start" || caption.ToString() == "开始") return window;
                    }
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            return IntPtr.Zero;
        }

        internal static bool DismissStartMenu(IntPtr expected)
        {
            if (expected == IntPtr.Zero || StartMenuForeground() != expected) return false;
            foreach (int key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
                if ((GetAsyncKeyState(key) & 0x8000) != 0) return false;
            if (GetForegroundWindow() != expected) return false;
            var inputs = new Input[2];
            inputs[0].Type = inputs[1].Type = 1;
            inputs[0].Data.Keyboard.Key = inputs[1].Data.Keyboard.Key = 0x1B;
            inputs[1].Data.Keyboard.Flags = 2;
            return SendInput(2, inputs, Marshal.SizeOf(typeof(Input))) == 2;
        }
    }

    internal sealed class GlobalHotkey : NativeWindow, IDisposable
    {
        private const int Id = 0x4D59;
        private readonly Action action;
        internal bool Registered { get; private set; }
        internal int ErrorCode { get; private set; }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);

        internal GlobalHotkey(int selection, Action action)
        {
            this.action = action;
            CreateHandle(new CreateParams { Caption = "Moyu.Hotkey", Parent = new IntPtr(-3) });
            uint modifiers = selection == 1 ? 1U : selection == 2 ? 3U : 6U;
            uint key = 0x4CU;
            Registered = RegisterHotKey(Handle, Id, modifiers | 0x4000U, key);
            ErrorCode = Registered ? 0 : Marshal.GetLastWin32Error();
        }

        internal static string Label(int selection)
        { return new[] { "关闭", "Alt+L", "Ctrl+Alt+L", "Ctrl+Shift+L" }[selection]; }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312 && message.WParam.ToInt32() == Id) { action(); return; }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (Registered) { UnregisterHotKey(Handle, Id); Registered = false; }
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }

    internal sealed class StartMenuGuard : IDisposable
    {
        private readonly Timer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly int seconds;
        private IntPtr observed;
        private double openedAt;

        internal StartMenuGuard(int seconds)
        {
            this.seconds = seconds;
            timer = new Timer { Interval = 200 };
            timer.Tick += delegate {
                IntPtr menu = NativeDesktop.StartMenuForeground();
                if (ShouldDismiss(menu, clock.Elapsed.TotalSeconds) && NativeDesktop.DismissStartMenu(menu)) observed = IntPtr.Zero;
            };
            timer.Start();
        }

        internal bool ShouldDismiss(IntPtr menu, double now)
        {
            if (menu == IntPtr.Zero) { observed = IntPtr.Zero; return false; }
            if (menu != observed) { observed = menu; openedAt = now; return false; }
            return now - openedAt >= seconds;
        }

        public void Dispose() { timer.Stop(); timer.Dispose(); clock.Stop(); }
    }
}
