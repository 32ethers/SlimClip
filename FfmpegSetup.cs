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

    // Resolve actual absolute executables, never let ProcessStartInfo search the current
    // directory or a shell. Keep the pair together to avoid mixing unrelated builds.
    internal static string? FindOnPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        foreach (string part in path.Split(Path.PathSeparator))
        {
            string folder = part.Trim();
            if (folder.Length >= 2 && folder[0] == '"' && folder[^1] == '"')
                folder = folder[1..^1];
            try
            {
                folder = Environment.ExpandEnvironmentVariables(folder);
                if (HasTools(folder)) return Path.GetFullPath(folder);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
            { /* Ignore a malformed PATH entry rather than falling back to a relative path. */ }
        }
        return null;
    }

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
            progress.Report(Loc.T("setup.downloading"));
            using (var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 3 }) { Timeout = TimeSpan.FromMinutes(30) })
            using (var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation))
            {
                if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                    throw new IOException(Loc.T("setup.non.https"));
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new IOException(Loc.F("setup.http.failed", (int)response.StatusCode));
                if (response.Content.Headers.ContentLength > MaxZipBytes)
                    throw new IOException(Loc.T("setup.zip.too.large"));
                await using var input = await response.Content.ReadAsStreamAsync(cancellation);
                await using var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[81920];
                long bytes = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellation)) != 0)
                {
                    bytes += count;
                    if (bytes > MaxZipBytes) throw new IOException(Loc.T("setup.zip.too.large"));
                    sha.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
                    progress.Report(Loc.F("setup.download.progress", bytes / 1048576.0) +
                        (response.Content.Headers.ContentLength is long total && total > 0 ? Loc.F("setup.download.total", total / 1048576.0) : ""));
                }
                if (!string.Equals(Convert.ToHexString(sha.GetHashAndReset()), ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException(Loc.T("setup.hash.failed"));
            }
            progress.Report(Loc.T("setup.extracting"));
            using (var zip = ZipFile.OpenRead(archive))
            {
                var selected = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in zip.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    string[] segments = name.TrimEnd('/').Split('/');
                    if (name.StartsWith('/') || segments.Any(s => s is "" or "." or ".." || s.Contains(':')))
                        throw new IOException(Loc.T("setup.zip.unsafe.path"));
                    if (segments.Length != 3 || segments[0] != "ffmpeg-8.1.2-essentials_build" ||
                        segments[1] != "bin" || !segments[2].Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
                        !segments[2].Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (entry.Length <= 0 || entry.Length > MaxToolBytes || !selected.TryAdd(segments[2], entry))
                        throw new IOException(Loc.T("setup.invalid.tools"));
                }
                foreach (string file in new[] { "ffmpeg.exe", "ffprobe.exe" })
                {
                    if (!selected.TryGetValue(file, out var entry)) throw new IOException(Loc.T("setup.missing.tools"));
                    await using var source = entry.Open();
                    await using var destination = new FileStream(Path.Combine(stage, file), FileMode.CreateNew,
                        FileAccess.Write, FileShare.None, 81920, true);
                    byte[] buffer = new byte[81920];
                    long copied = 0;
                    int count;
                    while ((count = await source.ReadAsync(buffer, cancellation)) != 0)
                    {
                        copied += count;
                        if (copied > MaxToolBytes) throw new IOException(Loc.T("setup.tool.too.large"));
                        await destination.WriteAsync(buffer.AsMemory(0, count), cancellation);
                    }
                    if (copied != entry.Length) throw new IOException(Loc.T("setup.length.failed"));
                }
            }
            File.Delete(archive);
            cancellation.ThrowIfCancellationRequested();
            // Same-volume directory rename commits the pair together; never overwrite existing tools.
            if (Directory.Exists(ToolsDirectory))
            {
                if (!HasTools(ToolsDirectory)) throw new IOException(Loc.F("setup.incomplete", ToolsDirectory));
                return; // Another SlimClip instance finished while we were downloading.
            }
            Directory.Move(stage, ToolsDirectory);
            progress.Report(Loc.T("setup.installed"));
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
