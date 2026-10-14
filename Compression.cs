using System.Globalization;
using System.IO;

namespace SlimClip;

internal sealed record CompressionRequest(string File, string Level);
internal sealed record CompressionResult(string Message, bool Success);

internal static class Compression
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".m4v", ".ts",
      ".mts", ".m2ts", ".mpg", ".mpeg", ".3gp" };

    public static bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path)) && File.Exists(path);

    public static async Task<CompressionResult> RunAsync(CompressionRequest request, Preset preset,
        Settings settings, bool allowExtraTrackOmission, Action<double> progress, CancellationToken cancellation)
    {
        if (!IsSupported(request.File)) throw new IOException("视频文件不存在或扩展名不受支持。");
        if (preset.Crf is < 0 or > 51 || preset.MaxHeight < 0 ||
            (preset.MaxHeight != 0 && preset.MaxHeight % 2 != 0)) throw new ArgumentException("压缩档位参数无效。");
        var media = await MediaTools.ProbeAsync(request.File, cancellation);
        if (media.SubtitleCount + media.AncillaryCount > 0)
        {
            if (settings.SaveMode == SaveMode.Replace)
                throw new IOException("源文件含字幕、额外视频轨或数据轨，首版无法保留；禁止替换原文件，请改为另存。");
            if (!allowExtraTrackOmission)
                throw new IOException("源文件包含未处理轨道，必须先确认另存结果将不含这些轨道。");
        }
        // Block cooperating Windows writers/renames while FFmpeg reads the source.
        using var sourceLease = new FileStream(request.File, FileMode.Open, FileAccess.Read, FileShare.Read);
        var original = new FileInfo(request.File);
        long originalLength = original.Length;
        DateTime originalWriteTime = original.LastWriteTimeUtc;
        string directory = Path.GetDirectoryName(request.File)!;
        string basename = Path.GetFileNameWithoutExtension(request.File);
        string destination = Path.Combine(directory, basename +
            (settings.SaveMode == SaveMode.Copy ? $"_slimclip_{request.Level}" : "") + ".mp4");
        if (settings.SaveMode == SaveMode.Replace && !SamePath(destination, request.File) && File.Exists(destination))
            throw new IOException("目标 MP4 已存在，不能覆盖其他文件：" + destination);
        string temp = Path.Combine(directory, $".slimclip-{Guid.NewGuid():N}.mp4");
        try
        {
            var args = new List<string> { "-hide_banner", "-nostdin", "-loglevel", "error",
                "-progress", "pipe:1", "-i", request.File };
            MediaTools.AddEncodingOptions(args, media, preset);
            args.AddRange(new[] { "-y", temp });
            using (var ffmpeg = MediaTools.Start("ffmpeg", args))
            {
                using var registration = MediaTools.KillOnCancel(ffmpeg, cancellation);
                var errorTask = ffmpeg.StandardError.ReadToEndAsync(cancellation);
                try
                {
                    string? line;
                    while ((line = await ffmpeg.StandardOutput.ReadLineAsync(cancellation)) is not null)
                    {
                        if (line.StartsWith("out_time=", StringComparison.Ordinal) && media.Duration > 0 &&
                            TimeSpan.TryParse(line.AsSpan(9), CultureInfo.InvariantCulture, out var elapsed))
                            progress(Math.Clamp(elapsed.TotalSeconds / media.Duration, 0, 0.99));
                    }
                    await ffmpeg.WaitForExitAsync(cancellation);
                    string error = await errorTask;
                    if (ffmpeg.ExitCode != 0) throw new IOException("FFmpeg 转换失败：" + error.Trim());
                }
                catch (OperationCanceledException)
                {
                    try { if (!ffmpeg.HasExited) ffmpeg.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { }
                    await ffmpeg.WaitForExitAsync(CancellationToken.None);
                    throw;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            if (!File.Exists(temp) || new FileInfo(temp).Length == 0) throw new IOException("FFmpeg 未生成有效结果。");
            var encoded = await MediaTools.ProbeAsync(temp, cancellation);
            if (encoded.VideoCodec != "hevc" || encoded.Audio.Count != media.Audio.Count ||
                encoded.Audio.Any(x => x.Codec != "aac") || encoded.SubtitleCount != 0 || encoded.AncillaryCount != 0 || encoded.Duration <= 0 ||
                (media.Duration > 0 && Math.Abs(encoded.Duration - media.Duration) > Math.Max(1, media.Duration * 0.03)))
                throw new IOException("输出轨道或时长校验失败，原片未改动。");
            sourceLease.Dispose(); // Release before replacing/renaming the source on Windows.
            var currentSource = new FileInfo(request.File);
            if (!currentSource.Exists || currentSource.Length != originalLength || currentSource.LastWriteTimeUtc != originalWriteTime)
                throw new IOException("源视频在压缩期间发生变化，结果未保存。");
            if (settings.SkipLargerResult && new FileInfo(temp).Length >= originalLength)
                return new CompressionResult("结果未缩小，原文件未改动。", false);
            // Commit begins here. Cancellation after this check cannot interrupt an atomic
            // replace or the paired rename/rollback steps without risking an orphaned source.
            cancellation.ThrowIfCancellationRequested();
            string? leftoverBackup = null;
            if (settings.SaveMode == SaveMode.Copy)
            {
                destination = UniquePath(destination);
                File.Move(temp, destination); // No overwrite; a concurrent collision fails safely.
            }
            else if (SamePath(destination, request.File))
            {
                string backup = BackupPath(request.File);
                string journal = RecoveryJournal.Add(request.File, destination, backup);
                File.Replace(temp, request.File, backup);
                if (!TryDelete(backup)) leftoverBackup = backup;
                else RecoveryJournal.Complete(journal);
            }
            else
            {
                if (File.Exists(destination)) throw new IOException("目标 MP4 已存在：" + destination);
                string backup = BackupPath(request.File);
                string journal = RecoveryJournal.Add(request.File, destination, backup);
                File.Move(request.File, backup);
                try { File.Move(temp, destination); }
                catch
                {
                    try { File.Move(backup, request.File); RecoveryJournal.Complete(journal); }
                    catch (Exception ex) { throw new IOException($"回滚失败！原片备份保存在 {backup}：{ex.Message}", ex); }
                    throw;
                }
                if (!TryDelete(backup)) leftoverBackup = backup;
                else RecoveryJournal.Complete(journal);
            }
            progress(1);
            return new CompressionResult(leftoverBackup is null ? "已保存：" + destination :
                $"已保存：{destination}；旧文件备份需手动清理：{leftoverBackup}", true);
        }
        finally { TryDelete(temp); }
    }

    private static string BackupPath(string source) => Path.Combine(Path.GetDirectoryName(source)!,
        $".{Path.GetFileNameWithoutExtension(source)}.slimclip-backup-{Guid.NewGuid():N}{Path.GetExtension(source)}");

    private static bool TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool SamePath(string first, string second) =>
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private static string UniquePath(string proposed)
    {
        string path = proposed;
        for (int number = 2; File.Exists(path); number++)
            path = Path.Combine(Path.GetDirectoryName(proposed)!,
                Path.GetFileNameWithoutExtension(proposed) + $"_{number}" + Path.GetExtension(proposed));
        return path;
    }
}
