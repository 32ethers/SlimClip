using System.Globalization;
using System.IO;

namespace SlimClip;

internal sealed record Estimate(string Level, long Bytes, double SampleSeconds);

internal static class Estimator
{
    // Each preset is applied to an eight-second middle sample. The result is deliberately
    // labelled approximate: a single scene does not represent the complexity of a whole movie.
    public static async Task<List<Estimate>> RunAsync(string file, Settings settings,
        Action<string, double?> status, CancellationToken cancellation)
    {
        if (!Compression.IsSupported(file)) throw new IOException("请选择支持的视频文件。");
        status("探测视频轨道…", null);
        var media = await MediaTools.ProbeAsync(file, cancellation);
        if (media.Duration <= 0) throw new IOException("无法获取视频时长，不能估算。");
        double sample = Math.Min(8, media.Duration);
        double start = Math.Max(0, (media.Duration - sample) / 2);
        string folder = Path.Combine(Path.GetTempPath(), "SlimClip-estimate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var results = new List<Estimate>();
        var levels = new[] { "low", "medium", "high" };
        var labels = new[] { "低", "中", "高" };
        try
        {
            for (int index = 0; index < levels.Length; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                string label = $"评估 {labels[index]}（{index + 1}/3）";
                status(label, index / 3.0);
                string output = Path.Combine(folder, levels[index] + ".mp4");
                var args = new List<string> { "-hide_banner", "-nostdin", "-loglevel", "error",
                    "-progress", "pipe:1", "-ss", start.ToString("F3", CultureInfo.InvariantCulture),
                    "-i", file, "-t", sample.ToString("F3", CultureInfo.InvariantCulture) };
                MediaTools.AddEncodingOptions(args, media, settings.Presets[levels[index]]);
                args.AddRange(new[] { "-y", output });
                using (var process = MediaTools.Start("ffmpeg", args))
                {
                    using var registration = MediaTools.KillOnCancel(process, cancellation);
                    var errorTask = process.StandardError.ReadToEndAsync(cancellation);
                    try
                    {
                        string? line;
                        while ((line = await process.StandardOutput.ReadLineAsync(cancellation)) is not null)
                        {
                            if (line.StartsWith("out_time=", StringComparison.Ordinal) &&
                                TimeSpan.TryParse(line.AsSpan(9), CultureInfo.InvariantCulture, out var elapsed))
                                status(label, Math.Clamp((index + Math.Clamp(elapsed.TotalSeconds / sample, 0, 1)) / 3.0, 0, 0.99));
                        }
                        await process.WaitForExitAsync(cancellation);
                        string error = await errorTask;
                        if (process.ExitCode != 0) throw new IOException("预估编码失败：" + error.Trim());
                    }
                    catch (OperationCanceledException)
                    {
                        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                        catch (InvalidOperationException) { }
                        await process.WaitForExitAsync(CancellationToken.None);
                        throw;
                    }
                }
                if (!File.Exists(output) || new FileInfo(output).Length == 0)
                    throw new IOException("评估片段未生成。");
                double projected = new FileInfo(output).Length * media.Duration / sample;
                if (!double.IsFinite(projected) || projected < 0 || projected > long.MaxValue)
                    throw new IOException("视频过长或文件过大，无法可靠估算。");
                results.Add(new Estimate(levels[index], (long)projected, sample));
                status(label, (index + 1) / 3.0);
            }
            return results;
        }
        finally
        {
            try
            {
                // Never recurse through a substituted junction/symlink in a shared temp folder.
                if ((new DirectoryInfo(folder).Attributes & FileAttributes.ReparsePoint) == 0)
                    Directory.Delete(folder, recursive: true);
            }
            catch (IOException) { /* Incomplete sample may be locked while cancellation settles. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
