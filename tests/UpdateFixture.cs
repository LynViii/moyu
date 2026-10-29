using System;
using System.IO;
using System.Reflection;
using System.Threading;

[assembly: AssemblyTitle("摸鱼")]
[assembly: AssemblyProduct("摸鱼")]
#if FAIL_UPDATE
[assembly: AssemblyVersion("0.5.2.0")]
[assembly: AssemblyFileVersion("0.5.2.0")]
[assembly: AssemblyInformationalVersion("0.5.2")]
#else
[assembly: AssemblyVersion("0.5.1.0")]
[assembly: AssemblyFileVersion("0.5.1.0")]
[assembly: AssemblyInformationalVersion("0.5.1")]
#endif

public static class UpdateFixture
{
    [STAThread]
    public static int Main(string[] args)
    {
#if FAIL_UPDATE
        return 1;
#else
        int ready = Array.IndexOf(args, "--update-ready");
        int token = Array.IndexOf(args, "--update-token");
        if (ready < 0 || token < 0) return 2;
        File.WriteAllText(args[ready + 1] + ".tmp", args[token + 1]);
        File.Move(args[ready + 1] + ".tmp", args[ready + 1]);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture-started.txt"), Array.IndexOf(args, "--silent") >= 0 ? "silent" : "visible");
        Thread.Sleep(1500);
        return 0;
#endif
    }
}
