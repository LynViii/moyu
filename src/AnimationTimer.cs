using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFish
{
    internal sealed class AnimationTimer : IDisposable
    {
        private readonly Control owner;
        private readonly Action repaint;
        private readonly System.Threading.Timer timer;
        private int pending;
        private int interval = 15;
        private volatile bool enabled;
        private volatile bool disposed;

        internal AnimationTimer(Control owner, Action repaint)
        {
            this.owner = owner;
            this.repaint = repaint;
            timer = new System.Threading.Timer(QueueFrame, null, Timeout.Infinite, Timeout.Infinite);
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
            try
            {
                // Keep at most one queued UI callback; drawing never runs on the timer thread.
                owner.BeginInvoke((MethodInvoker)delegate {
                    Interlocked.Exchange(ref pending, 0);
                    if (enabled && !disposed && !owner.IsDisposed) repaint();
                });
            }
            catch (InvalidOperationException) { Interlocked.Exchange(ref pending, 0); }
        }

        internal void Stop() { Enabled = false; }

        public void Dispose()
        {
            enabled = false;
            disposed = true;
            timer.Dispose();
        }
    }
}
