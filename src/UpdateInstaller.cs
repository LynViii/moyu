using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace WindowsFish
{
    internal sealed class UpdatePlan
    {
        internal string DirectoryPath, Target, NewHash, OldHash, Version, Token;
        internal int ProcessId;
        internal long ProcessTicks;
        internal bool Silent;
        internal string PlanPath { get { return Path.Combine(DirectoryPath, "plan.xml"); } }
        internal string Candidate { get { return Path.Combine(DirectoryPath, "moyu.exe"); } }
        internal string Helper { get { return Path.Combine(DirectoryPath, "Moyu.UpdateHelper.exe"); } }
        internal string Prepared { get { return Path.Combine(DirectoryPath, "prepared.flag"); } }
        internal string Started { get { return Path.Combine(DirectoryPath, "started.flag"); } }
        internal string Result { get { return Path.Combine(DirectoryPath, "result.xml"); } }
        internal string Canceled { get { return Path.Combine(DirectoryPath, "cancel.flag"); } }
        internal string Sibling { get { return Target + ".update-" + Token + ".tmp"; } }
        internal string Backup { get { return Target + ".rollback-" + Token; } }

        internal void Save()
        {
            new XDocument(new XElement("update", new XAttribute("target", Target), new XAttribute("newHash", NewHash),
                new XAttribute("oldHash", OldHash), new XAttribute("version", Version), new XAttribute("token", Token),
                new XAttribute("pid", ProcessId), new XAttribute("ticks", ProcessTicks), new XAttribute("silent", Silent))).Save(PlanPath);
        }

        internal static UpdatePlan Load(string path, string token)
        {
            var root = SettingsFiles.Read(path).Root;
            if (root == null || root.Name != "update" || Path.GetFileName(path) != "plan.xml") throw new InvalidDataException("更新任务无效。");
            Guid id;
            if (!Guid.TryParseExact(token, "N", out id) || (string)root.Attribute("token") != token) throw new InvalidDataException("更新任务标识无效。");
            var plan = new UpdatePlan { DirectoryPath = Path.GetDirectoryName(Path.GetFullPath(path)), Target = Path.GetFullPath((string)root.Attribute("target")),
                NewHash = (string)root.Attribute("newHash"), OldHash = (string)root.Attribute("oldHash"), Version = (string)root.Attribute("version"),
                Token = token, ProcessId = (int)root.Attribute("pid"), ProcessTicks = (long)root.Attribute("ticks"), Silent = (bool)root.Attribute("silent") };
            if (!Guid.TryParseExact(Path.GetFileName(plan.DirectoryPath), "N", out id) || !plan.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                plan.Target.StartsWith(plan.DirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                plan.OldHash == null || plan.NewHash == null || plan.OldHash.Length != 64 || plan.NewHash.Length != 64 ||
                !plan.OldHash.All(Uri.IsHexDigit) || !plan.NewHash.All(Uri.IsHexDigit) || plan.ProcessId <= 0)
                throw new InvalidDataException("更新任务路径或校验值无效。");
            UpdateService.ParseVersion(plan.Version);
            return plan;
        }
    }

    internal static class UpdateInstaller
    {
        internal static string StartupFailure;
        private static string readyPath;

        internal static UpdatePlan CreatePlan(string candidate, Version version, bool silent)
        {
            string target = Application.ExecutablePath;
            var plan = new UpdatePlan { DirectoryPath = Path.GetDirectoryName(Path.GetFullPath(candidate)), Target = target,
                OldHash = UpdateService.Hash(target), NewHash = UpdateService.Hash(candidate), Version = version.ToString(), Token = Guid.NewGuid().ToString("N"),
                ProcessId = Process.GetCurrentProcess().Id, ProcessTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks, Silent = silent };
            UpdateService.VerifyCandidate(candidate, plan.NewHash, version);
            File.Copy(target, plan.Helper, false);
            plan.Save();
            return plan;
        }

        internal static async Task PrepareAsync(UpdatePlan plan)
        {
            try
            {
            using (var helper = Process.Start(new ProcessStartInfo(plan.Helper, "--apply-update " + Quote(plan.PlanPath) + " " + plan.Token)
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = plan.DirectoryPath }))
            {
                var clock = Stopwatch.StartNew();
                while (clock.Elapsed.TotalSeconds < 10)
                {
                    if (File.Exists(plan.Prepared)) return;
                    if (File.Exists(plan.Result)) throw new IOException((string)SettingsFiles.Read(plan.Result).Root.Attribute("message"));
                    if (helper.HasExited) throw new IOException("更新助手未能启动，原程序仍可继续使用。");
                    await Task.Delay(100);
                }
                // The helper never replaces the executable while this process is alive.
                throw new IOException("准备安装超时，请重试。");
            }
            }
            catch { Cancel(plan); throw; }
        }

        internal static void Cancel(UpdatePlan plan)
        { if (plan != null) try { File.WriteAllText(plan.Canceled, plan.Token); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

        internal static int RunHelper(string path, string token)
        {
            UpdatePlan plan = null;
            Process child = null;
            bool replaced = false;
            bool parentReleased = false;
            try
            {
                plan = UpdatePlan.Load(path, token);
                if (!string.Equals(Path.GetFullPath(Application.ExecutablePath), plan.Helper, StringComparison.OrdinalIgnoreCase) ||
                    UpdateService.Hash(Application.ExecutablePath) != plan.OldHash || UpdateService.Hash(plan.Target) != plan.OldHash)
                    throw new InvalidDataException("原程序与更新任务不一致。");
                UpdateService.VerifyCandidate(plan.Candidate, plan.NewHash, UpdateService.ParseVersion(plan.Version));
                // Copy beside the target so File.Replace works even when the download is on another drive.
                File.Copy(plan.Candidate, plan.Sibling, false);
                if (UpdateService.Hash(plan.Sibling) != plan.NewHash) throw new InvalidDataException("安装前校验失败。");
                if (File.Exists(plan.Canceled)) throw new IOException("更新已取消。");
                File.WriteAllText(plan.Prepared, plan.Token);
                WaitForParent(plan);
                parentReleased = true;
                if (File.Exists(plan.Canceled)) throw new IOException("更新已取消。");
                if (UpdateService.Hash(plan.Target) != plan.OldHash) throw new InvalidDataException("原程序在更新期间发生变化。");
                var retry = Stopwatch.StartNew();
                while (true)
                {
                    try { File.Replace(plan.Sibling, plan.Target, plan.Backup); break; }
                    catch (IOException) { if (retry.Elapsed.TotalSeconds > 10) throw; Thread.Sleep(200); }
                }
                replaced = true;
                child = Process.Start(new ProcessStartInfo(plan.Target, "--update-ready " + Quote(plan.Started) + " --update-token " + plan.Token + (plan.Silent ? " --silent" : ""))
                    { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(plan.Target) });
                var startup = Stopwatch.StartNew();
                while (!File.Exists(plan.Started))
                {
                    if (child.HasExited || startup.Elapsed.TotalSeconds > 12) throw new IOException("新版本未完成启动，已恢复原版本。");
                    Thread.Sleep(100);
                }
                if (File.ReadAllText(plan.Started) != plan.Token) throw new InvalidDataException("新版本启动确认无效。");
                WriteResult(plan, "success", "更新完成。");
                TryDelete(plan.Backup);
                return 0;
            }
            catch (Exception error)
            {
                if (plan != null)
                {
                    string message = error is UnauthorizedAccessException ? "安装目录不可写，请将程序放到可写文件夹后重试。" : error.Message;
                    if (replaced && File.Exists(plan.Backup))
                    {
                        try
                        {
                            if (child != null && !child.HasExited) { child.Kill(); child.WaitForExit(5000); }
                            File.Replace(plan.Backup, plan.Target, null);
                            WriteResult(plan, "rolledBack", message);
                            Process.Start(new ProcessStartInfo(plan.Target, "--update-failed " + Quote(plan.Result) + " --update-token " + plan.Token)
                                { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(plan.Target) });
                        }
                        catch (Exception rollbackError) { WriteResult(plan, "error", message + " 恢复失败：" + rollbackError.Message + " 备份位置：" + plan.Backup); }
                    }
                    else
                    {
                        WriteResult(plan, "error", message);
                        if (parentReleased && !File.Exists(plan.Canceled) && File.Exists(plan.Target) && UpdateService.Hash(plan.Target) == plan.OldHash)
                        {
                            try { Process.Start(new ProcessStartInfo(plan.Target, "--update-failed " + Quote(plan.Result) + " --update-token " + plan.Token)
                                { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(plan.Target) }); }
                            catch (Exception restartError) { WriteResult(plan, "error", message + " 请手动重新打开程序：" + restartError.Message); }
                        }
                    }
                    TryDelete(plan.Sibling);
                }
                return 1;
            }
            finally { if (child != null) child.Dispose(); }
        }

        private static void WaitForParent(UpdatePlan plan)
        {
            Process parent;
            try { parent = Process.GetProcessById(plan.ProcessId); }
            catch (ArgumentException) { return; }
            using (parent)
            {
                if (parent.HasExited) return;
                if (parent.StartTime.ToUniversalTime().Ticks != plan.ProcessTicks ||
                    !string.Equals(Path.GetFullPath(parent.MainModule.FileName), plan.Target, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("更新进程身份发生变化。");
                var wait = Stopwatch.StartNew();
                while (!parent.WaitForExit(100))
                {
                    if (File.Exists(plan.Canceled)) throw new IOException("更新已取消。");
                    if (wait.Elapsed.TotalSeconds > 30) throw new IOException("原程序尚未关闭，请重试更新。");
                }
            }
        }

        internal static string[] ConsumeRestartArguments(string[] args)
        {
            int ready = Array.IndexOf(args, "--update-ready");
            int failed = Array.IndexOf(args, "--update-failed");
            if (ready < 0 && failed < 0) return args;
            int marker = ready >= 0 ? ready : failed;
            int token = Array.IndexOf(args, "--update-token");
            if (marker + 1 >= args.Length || token < 0 || token + 1 >= args.Length) throw new InvalidDataException("更新启动参数不完整。");
            string resultPath = Path.GetFullPath(args[marker + 1]);
            var plan = UpdatePlan.Load(Path.Combine(Path.GetDirectoryName(resultPath), "plan.xml"), args[token + 1]);
            string expected = ready >= 0 ? plan.Started : plan.Result;
            if (!string.Equals(resultPath, expected, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Application.ExecutablePath, plan.Target, StringComparison.OrdinalIgnoreCase) ||
                UpdateService.Hash(Application.ExecutablePath) != (ready >= 0 ? plan.NewHash : plan.OldHash)) throw new InvalidDataException("更新启动任务不匹配。");
            if (ready >= 0) readyPath = resultPath;
            else StartupFailure = "更新未完成，仍在使用原版本。";
            return args.Where((value, index) => index != marker && index != marker + 1 && index != token && index != token + 1).ToArray();
        }

        internal static void MarkReady()
        {
            if (readyPath == null) return;
            var plan = SettingsFiles.Read(Path.Combine(Path.GetDirectoryName(readyPath), "plan.xml"));
            string temporary = readyPath + ".tmp";
            File.WriteAllText(temporary, (string)plan.Root.Attribute("token"));
            File.Move(temporary, readyPath);
            readyPath = null;
        }

        private static void WriteResult(UpdatePlan plan, string status, string message)
        {
            try
            {
                string temporary = plan.Result + ".tmp";
                new XDocument(new XElement("result", new XAttribute("status", status), new XAttribute("message", message))).Save(temporary);
                if (File.Exists(plan.Result)) File.Replace(temporary, plan.Result, null); else File.Move(temporary, plan.Result);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        private static string Quote(string text) { return "\"" + text + "\""; }
    }
}
