using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class ResourceProbe
{
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Assembly assembly = Assembly.LoadFrom(args[0]);
            Type preferences = assembly.GetType("WindowsFish.Preferences");
            object options = Activator.CreateInstance(preferences, true);
            preferences.GetField("Scene", Fields).SetValue(options, args[2]);
            preferences.GetField("EcoMode", Fields).SetValue(options, args[3] == "eco");
            object mode = ((Array)assembly.GetType("WindowsFish.UpdateScreenMode").GetMethod("All").Invoke(null, null)).GetValue(0);
            Type type = assembly.GetType("WindowsFish.UpdateForm");
            var constructor = type.GetConstructor(new[] { assembly.GetType("WindowsFish.FishMode"), mode.GetType(), typeof(double), preferences });
            using (Form form = (Form)constructor.Invoke(new object[] { null, mode, 1D, options }))
            using (Process process = Process.GetCurrentProcess())
            using (var timer = new Timer { Interval = 100 })
            {
                form.FormBorderStyle = FormBorderStyle.None;
                form.StartPosition = FormStartPosition.Manual;
                form.Bounds = Screen.PrimaryScreen.Bounds;
                form.TopMost = true;
                int paints = 0;
                double memory = 0, working = 0;
                TimeSpan cpu = TimeSpan.Zero;
                int gdi = 0, handles = 0, collections = 0;
                double start = 0;
                var clock = Stopwatch.StartNew();
                bool measuring = false;
                form.Paint += delegate { if (measuring) paints++; };
                timer.Tick += delegate {
                    double now = clock.Elapsed.TotalSeconds;
                    if (!measuring)
                    {
                        if (now < 1.5) return;
                        if (args[3] == "hidden") form.Hide();
                        process.Refresh();
                        gdi = GetGuiResources(process.Handle, 0);
                        handles = process.HandleCount;
                        memory = process.PrivateMemorySize64;
                        working = process.WorkingSet64;
                        collections = GC.CollectionCount(0);
                        cpu = process.TotalProcessorTime;
                        start = clock.Elapsed.TotalSeconds;
                        measuring = true;
                        return;
                    }
                    if (now - start < 8) return;
                    double seconds = now - start;
                    double cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                    process.Refresh();
                    memory = (memory + process.PrivateMemorySize64) / 2;
                    working = (working + process.WorkingSet64) / 2;
                    string json = string.Format(CultureInfo.InvariantCulture,
                        "{{\"Scene\":\"{0}\",\"Mode\":\"{1}\",\"Width\":{2},\"Height\":{3},\"Seconds\":{4:F4},\"CpuMs\":{5:F3},\"CpuOneCorePercent\":{6:F4},\"AveragePrivateMiB\":{7:F4},\"AverageWorkingMiB\":{8:F4},\"Paints\":{9},\"PaintFps\":{10:F3},\"GdiStart\":{11},\"GdiEnd\":{12},\"HandlesStart\":{13},\"HandlesEnd\":{14},\"Gen0Collections\":{15}}}",
                        args[2], args[3], form.Width, form.Height, seconds, cpuMs, cpuMs / seconds / 10,
                        memory / 1048576, working / 1048576, paints, paints / seconds,
                        gdi, GetGuiResources(process.Handle, 0), handles, process.HandleCount, GC.CollectionCount(0) - collections);
                    File.WriteAllText(args[1], json);
                    timer.Stop();
                    Application.ExitThread();
                };
                form.Show();
                timer.Start();
                Application.Run();
            }
            return 0;
        }
        catch (Exception error) { File.WriteAllText(args[1] + ".error.txt", error.ToString()); return 1; }
    }
}
