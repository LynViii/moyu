using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

public sealed class FrameReport
{
    public int Frames;
    public double Seconds;
    public double Fps;
    public double MedianMs;
    public double P95Ms;
    public double MaximumMs;
    public int Over50Ms;
    public int Over100Ms;
}

public static class FrameProbe
{
    public static FrameReport Measure(Form form, int milliseconds)
    {
        form.Show();
        var clock = Stopwatch.StartNew();
        var intervals = new List<double>();
        double previous = -1;
        PaintEventHandler handler = delegate {
            double now = clock.Elapsed.TotalMilliseconds;
            if (now < 700) return;
            if (previous >= 0) intervals.Add(now - previous);
            previous = now;
        };
        form.Paint += handler;
        using (var context = new ApplicationContext())
        using (var stop = new System.Windows.Forms.Timer { Interval = milliseconds + 700 })
        try
        {
            stop.Tick += delegate { context.ExitThread(); };
            stop.Start();
            Application.Run(context);
        }
        finally { form.Paint -= handler; }
        clock.Stop();
        if (intervals.Count < 5) throw new InvalidOperationException("Too few rendered frames");
        double[] sorted = intervals.OrderBy(x => x).ToArray();
        double duration = clock.Elapsed.TotalSeconds - 0.7;
        return new FrameReport { Frames = intervals.Count, Seconds = duration,
            Fps = intervals.Count / duration,
            MedianMs = sorted[sorted.Length / 2], P95Ms = sorted[(int)((sorted.Length - 1) * 0.95)],
            MaximumMs = sorted[sorted.Length - 1], Over50Ms = intervals.Count(x => x > 50),
            Over100Ms = intervals.Count(x => x > 100) };
    }

}
