using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace SlimClip;

// FFmpeg is third-party GPLv3 software, not part of the MIT-licensed SlimClip installer.
// Only an explicit user action may fetch it; the authenticated ZIP is never bundled.
internal static class FfmpegSetup
{
    internal const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip";
    internal const string ExpectedSha256 = "db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec";
    private const long MaxZipBytes = 512L * 1024 * 1024;
    private const long MaxToolBytes = 512L * 1024 * 1024;
    internal static string ToolsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlimClip", "tools");

    internal static bool HasTools(string folder) =>
        Path.IsPathFullyQualified(folder) && File.Exists(Path.Combine(folder, "ffmpeg.exe")) &&
        File.Exists(Path.Combine(folder, "ffprobe.exe"));

    internal static async Task InstallAsync(IProgress<string> progress, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (HasTools(ToolsDirectory)) return;
        string parent = Path.GetDirectoryName(ToolsDirectory)!;
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, ".tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            string archive = Path.Combine(stage, "download.zip");
            progress.Report("正在从 Gyan 官方网站下载 FFmpeg…");
            using (var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 3 }) { Timeout = TimeSpan.FromMinutes(30) })
            using (var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation))
            {
                if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                    throw new IOException("官网下载最终地址不是 HTTPS，已拒绝安装。");
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new IOException($"官网下载失败（HTTP {(int)response.StatusCode}）；不会修改源视频。");
                if (response.Content.Headers.ContentLength > MaxZipBytes)
                    throw new IOException("下载文件超过安全大小限制。");
                await using var input = await response.Content.ReadAsStreamAsync(cancellation);
                await using var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[81920];
                long bytes = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellation)) != 0)
                {
                    bytes += count;
                    if (bytes > MaxZipBytes) throw new IOException("下载文件超过安全大小限制。");
                    sha.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
                    progress.Report($"正在下载 FFmpeg：{bytes / 1048576.0:F1} MiB" +
                        (response.Content.Headers.ContentLength is long total && total > 0 ? $" / {total / 1048576.0:F1} MiB" : ""));
                }
                if (!string.Equals(Convert.ToHexString(sha.GetHashAndReset()), ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("FFmpeg 下载包 SHA-256 与固定校验值不符，已拒绝安装。请检查网络后重试。");
            }
            progress.Report("校验通过，正在安全提取 ffmpeg.exe 和 ffprobe.exe…");
            using (var zip = ZipFile.OpenRead(archive))
            {
                var selected = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in zip.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    string[] segments = name.TrimEnd('/').Split('/');
                    if (name.StartsWith('/') || segments.Any(s => s is "" or "." or ".." || s.Contains(':')))
                        throw new IOException("下载包含不安全的 ZIP 路径，已拒绝安装。");
                    if (segments.Length != 3 || segments[0] != "ffmpeg-8.1.2-essentials_build" ||
                        segments[1] != "bin" || !segments[2].Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
                        !segments[2].Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (entry.Length <= 0 || entry.Length > MaxToolBytes || !selected.TryAdd(segments[2], entry))
                        throw new IOException("下载包中的 FFmpeg 可执行文件无效或重复。");
                }
                foreach (string file in new[] { "ffmpeg.exe", "ffprobe.exe" })
                {
                    if (!selected.TryGetValue(file, out var entry)) throw new IOException("下载包缺少必要的 FFmpeg 工具。");
                    await using var source = entry.Open();
                    await using var destination = new FileStream(Path.Combine(stage, file), FileMode.CreateNew,
                        FileAccess.Write, FileShare.None, 81920, true);
                    byte[] buffer = new byte[81920];
                    long copied = 0;
                    int count;
                    while ((count = await source.ReadAsync(buffer, cancellation)) != 0)
                    {
                        copied += count;
                        if (copied > MaxToolBytes) throw new IOException("可执行文件超过安全大小限制。");
                        await destination.WriteAsync(buffer.AsMemory(0, count), cancellation);
                    }
                    if (copied != entry.Length) throw new IOException("解压文件长度校验失败。");
                }
            }
            File.Delete(archive);
            cancellation.ThrowIfCancellationRequested();
            // Same-volume directory rename commits the pair together; never overwrite existing tools.
            if (Directory.Exists(ToolsDirectory))
            {
                if (!HasTools(ToolsDirectory)) throw new IOException("工具目录已存在但不完整；请手动检查后重试：" + ToolsDirectory);
                return; // Another SlimClip instance finished while we were downloading.
            }
            Directory.Move(stage, ToolsDirectory);
            progress.Report("FFmpeg 已安装到当前用户目录。");
        }
        finally
        {
            // stage is a fresh, private GUID directory. Never follow a substituted junction.
            try
            {
                if (Directory.Exists(stage) && (File.GetAttributes(stage) & FileAttributes.ReparsePoint) == 0)
                    Directory.Delete(stage, true);
            }
            catch (IOException) { /* Preserve the original setup error. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
