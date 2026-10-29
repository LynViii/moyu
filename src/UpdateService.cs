using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFish
{
    [DataContract] internal sealed class ReleaseAsset
    {
        [DataMember(Name = "name")] internal string Name { get; set; }
        [DataMember(Name = "browser_download_url")] internal string Url { get; set; }
        [DataMember(Name = "size")] internal long Size { get; set; }
        [DataMember(Name = "digest")] internal string Digest { get; set; }
    }

    [DataContract] internal sealed class ReleaseInfo
    {
        [DataMember(Name = "tag_name")] internal string Tag { get; set; }
        [DataMember(Name = "body")] internal string Notes { get; set; }
        [DataMember(Name = "draft")] internal bool Draft { get; set; }
        [DataMember(Name = "prerelease")] internal bool Prerelease { get; set; }
        [DataMember(Name = "assets")] internal ReleaseAsset[] Assets;
        internal Version Version;
        internal ReleaseAsset Executable;
        internal ReleaseAsset Checksums;
    }

    internal static class UpdateService
    {
        internal const string LatestUrl = "https://api.github.com/repos/LynViii/moyu/releases/latest";
        internal const string ReleasesUrl = "https://github.com/LynViii/moyu/releases";
        internal static string StagingRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Moyu", "updates"); } }

        internal static void CleanupStaging()
        {
            try
            {
                if (!Directory.Exists(StagingRoot)) return;
                foreach (string directory in Directory.GetDirectories(StagingRoot))
                {
                    Guid id;
                    if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out id) ||
                        (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 || Directory.GetLastWriteTimeUtc(directory) > DateTime.UtcNow.AddDays(-7)) continue;
                    foreach (string name in new[] { "moyu.exe", "Moyu.UpdateHelper.exe", "plan.xml", "prepared.flag", "started.flag", "started.flag.tmp", "result.xml", "result.xml.tmp", "cancel.flag" })
                        File.Delete(Path.Combine(directory, name));
                    if (Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory);
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        internal static Version ParseVersion(string text)
        {
            Version version;
            if (text != null && text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text.Substring(1);
            if (!Version.TryParse(text, out version)) throw new InvalidDataException("版本号格式不正确。");
            return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
        }

        internal static ReleaseInfo ParseRelease(string json)
        {
            ReleaseInfo release;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                release = (ReleaseInfo)new DataContractJsonSerializer(typeof(ReleaseInfo)).ReadObject(stream);
            if (release == null || release.Draft || release.Prerelease) throw new InvalidDataException("没有可用的正式版本。");
            release.Version = ParseVersion(release.Tag);
            release.Assets = release.Assets ?? new ReleaseAsset[0];
            release.Executable = release.Assets.SingleOrDefault(a => a.Name == "moyu.exe");
            release.Checksums = release.Assets.SingleOrDefault(a => a.Name == "SHA256SUMS.txt");
            if (release.Executable == null || release.Checksums == null) throw new InvalidDataException("此版本缺少安装文件或校验清单，请稍后重试。");
            ValidateAssetUrl(release.Executable.Url);
            ValidateAssetUrl(release.Checksums.Url);
            if (release.Executable.Size < 1024 || release.Executable.Size > 64 * 1024 * 1024) throw new InvalidDataException("安装文件大小异常。");
            return release;
        }

        internal static void ValidateAssetUrl(string text)
        {
            Uri url;
            if (!Uri.TryCreate(text, UriKind.Absolute, out url) || url.Scheme != "https" || url.Host != "github.com" ||
                !url.AbsolutePath.StartsWith("/LynViii/moyu/releases/download/", StringComparison.Ordinal) || !string.IsNullOrEmpty(url.UserInfo))
                throw new InvalidDataException("更新文件来源不正确。");
        }

        internal static async Task<ReleaseInfo> CheckAsync(CancellationToken token)
        {
            byte[] json = await FetchAsync(new Uri(LatestUrl), 1024 * 1024, 0, null, null, token).ConfigureAwait(false);
            return ParseRelease(Encoding.UTF8.GetString(json));
        }

        internal static string ReadChecksum(string text, string name)
        {
            string hash = null;
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2 || parts[1].TrimStart('*') != name) continue;
                    if (hash != null || parts[0].Length != 64 || !parts[0].All(Uri.IsHexDigit)) throw new InvalidDataException("校验清单格式不正确。");
                    hash = parts[0].ToLowerInvariant();
                }
            }
            if (hash == null) throw new InvalidDataException("校验清单中没有安装文件。");
            return hash;
        }

        internal static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var file = File.OpenRead(path)) return BitConverter.ToString(algorithm.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }

        internal static void VerifyCandidate(string path, string expectedHash, Version expectedVersion)
        {
            if (!string.Equals(Hash(path), expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("下载文件校验失败，原程序未改动。");
            using (var file = new BinaryReader(File.OpenRead(path)))
            {
                if (file.ReadUInt16() != 0x5A4D) throw new InvalidDataException("下载的不是 Windows 应用。");
                file.BaseStream.Position = 0x3C;
                int offset = file.ReadInt32();
                if (offset < 64 || offset > file.BaseStream.Length - 6) throw new InvalidDataException("安装文件头无效。");
                file.BaseStream.Position = offset;
                if (file.ReadUInt32() != 0x00004550 || file.ReadUInt16() != 0x8664) throw new InvalidDataException("安装文件架构不匹配。");
            }
            var info = FileVersionInfo.GetVersionInfo(path);
            if (info.ProductName != "摸鱼" || ParseVersion(info.ProductVersion) != expectedVersion) throw new InvalidDataException("安装文件产品或版本不匹配。");
        }

        internal static async Task<string> DownloadAsync(ReleaseInfo release, IProgress<int> progress, CancellationToken token)
        {
            ValidateAssetUrl(release.Executable.Url); ValidateAssetUrl(release.Checksums.Url);
            string directory = Path.Combine(StagingRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "moyu.exe");
            try
            {
                byte[] manifest = await FetchAsync(new Uri(release.Checksums.Url), 65536, 0, null, null, token).ConfigureAwait(false);
                string hash = ReadChecksum(Encoding.UTF8.GetString(manifest), "moyu.exe");
                if (!string.IsNullOrEmpty(release.Executable.Digest) && !string.Equals(release.Executable.Digest, "sha256:" + hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("发布附件与校验清单不一致。");
                await FetchAsync(new Uri(release.Executable.Url), 64 * 1024 * 1024, release.Executable.Size, file, progress, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                VerifyCandidate(file, hash, release.Version);
                if (progress != null) progress.Report(100);
                return file;
            }
            catch
            {
                try { File.Delete(file); Directory.Delete(directory); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        private static async Task<byte[]> FetchAsync(Uri url, long limit, long expected, string path, IProgress<int> progress, CancellationToken token)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "Moyu/" + AppVersion.Value;
            request.Accept = "application/vnd.github+json";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = request.ReadWriteTimeout = 20000;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(path == null ? 20000 : 120000);
                using (timeout.Token.Register(request.Abort))
                try
                {
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        string host = response.ResponseUri.Host;
                        if (response.ResponseUri.Scheme != "https" || !(host == "github.com" || host == "api.github.com" || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                            throw new InvalidDataException("更新下载重定向来源不正确。");
                        if (response.ContentLength > limit) throw new InvalidDataException("服务器返回的文件过大。");
                        using (var input = response.GetResponseStream())
                        using (Stream output = path == null ? (Stream)new MemoryStream() : new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[32768];
                            long total = 0;
                            int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                            {
                                total += count;
                                if (total > limit) throw new InvalidDataException("下载大小超出限制。");
                                await output.WriteAsync(buffer, 0, count, timeout.Token).ConfigureAwait(false);
                                if (progress != null && expected > 0) progress.Report((int)Math.Min(99, total * 100 / expected));
                            }
                            if (expected > 0 && total != expected) throw new InvalidDataException("安装文件未完整下载。");
                            return path == null ? ((MemoryStream)output).ToArray() : new byte[0];
                        }
                    }
                }
                catch (WebException error)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                    var response = error.Response as HttpWebResponse;
                    bool limited = response != null && (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429);
                    if (response != null) response.Dispose();
                    throw new IOException(limited ? "更新服务暂时限流，请稍后重试。" : "无法连接更新服务，请检查网络后重试。", error);
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested) throw;
                    throw new IOException("检查或下载更新超时，请重试。");
                }
            }
        }
    }
}
