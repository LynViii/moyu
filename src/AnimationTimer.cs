using System;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace WindowsFish
{
    internal sealed class AnimationTimer : IDisposable
    {
        private readonly Control owner;
        private readonly Action repaint;
        private readonly System.Threading.Timer timer;
        private FrameWindow window;
        private readonly MethodInvoker present;
        private volatile bool nativeDispatch;
        private int pending;
        private int interval = 15;
        private volatile bool enabled;
        private volatile bool disposed;

        internal AnimationTimer(Control owner, Action repaint)
        {
            this.owner = owner;
            this.repaint = repaint;
            present = Present;
            timer = new System.Threading.Timer(QueueFrame, null, Timeout.Infinite, Timeout.Infinite);
        }

        internal bool NativeDispatch
        {
            set
            {
                if (value && window == null) window = new FrameWindow(Present);
                nativeDispatch = value;
            }
        }

        public int Interval
        {
            get { return interval; }
            set { interval = value; if (enabled && !disposed) timer.Change(0, interval); }
        }

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (disposed || enabled == value) return;
                enabled = value;
                timer.Change(value ? 0 : Timeout.Infinite, value ? interval : Timeout.Infinite);
            }
        }

        private void QueueFrame(object state)
        {
            if (!enabled || disposed || Interlocked.CompareExchange(ref pending, 1, 0) != 0) return;
            if (nativeDispatch)
            {
                // A bounded native wakeup avoids Control.BeginInvoke's per-frame marshaling queue.
                if (!window.Post()) Interlocked.Exchange(ref pending, 0);
            }
            else
            {
                try { owner.BeginInvoke(present); }
                catch (InvalidOperationException) { Interlocked.Exchange(ref pending, 0); }
            }
        }

        private void Present()
        {
            Interlocked.Exchange(ref pending, 0);
            if (enabled && !disposed && !owner.IsDisposed) repaint();
        }

        internal void Stop() { Enabled = false; }

        public void Dispose()
        {
            enabled = false;
            disposed = true;
            timer.Dispose();
            if (window != null) window.Dispose();
        }

        private sealed class FrameWindow : NativeWindow, IDisposable
        {
            private const int FrameMessage = 0x8000 + 0x4D5;
            private readonly Action present;
            [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

            internal FrameWindow(Action present)
            {
                this.present = present;
                CreateHandle(new CreateParams { Caption = "Moyu.Animation", Parent = new IntPtr(-3) });
            }

            internal bool Post() { return Handle != IntPtr.Zero && PostMessage(Handle, FrameMessage, IntPtr.Zero, IntPtr.Zero); }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == FrameMessage) { present(); return; }
                base.WndProc(ref message);
            }

            public void Dispose() { if (Handle != IntPtr.Zero) DestroyHandle(); }
        }
    }
}
