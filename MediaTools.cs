using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace SlimClip;

internal sealed record AudioTrack(string Codec, int Channels);
internal sealed record MediaInfo(int VideoStreamIndex, string VideoCodec, int Height,
    double Duration, IReadOnlyList<AudioTrack> Audio, int SubtitleCount, int AncillaryCount);

internal static class MediaTools
{
    internal static string Executable(string tool)
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, tool + ".exe");
        if (!File.Exists(bundled)) throw new IOException($"安装不完整：缺少 {tool}.exe。请重新安装 SlimClip。");
        return bundled;
    }

    internal static Process Start(string tool, IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo(Executable(tool))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        try { return Process.Start(psi) ?? throw new IOException($"无法启动 {tool}。"); }
        catch (System.ComponentModel.Win32Exception ex)
        { throw new IOException($"无法启动已安装的 {tool}.exe，请重新安装 SlimClip。", ex); }
    }

    internal static CancellationTokenRegistration KillOnCancel(Process process, CancellationToken cancellation) =>
        cancellation.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });

    internal static async Task<string> CaptureAsync(string tool, IEnumerable<string> arguments, CancellationToken cancellation)
    {
        using var process = Start(tool, arguments);
        using var registration = KillOnCancel(process, cancellation);
        var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
        var stderr = process.StandardError.ReadToEndAsync(cancellation);
        try
        {
            await process.WaitForExitAsync(cancellation);
            var error = await stderr;
            if (process.ExitCode != 0) throw new IOException($"{tool} 失败：{error.Trim()}");
            return await stdout;
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
    }

    internal static async Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellation)
    {
        var json = await CaptureAsync("ffprobe", new[] { "-v", "error", "-show_streams", "-show_format", "-of", "json", path }, cancellation);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("streams", out var streams)) throw new IOException("无法读取媒体轨道。");
        int videoIndex = -1, height = 0, subtitles = 0, ancillary = 0;
        string codec = "";
        var audio = new List<AudioTrack>();
        foreach (var stream in streams.EnumerateArray())
        {
            var type = stream.TryGetProperty("codec_type", out var streamType) ? streamType.GetString() : null;
            if (type == "video")
            {
                bool attachedPicture = stream.TryGetProperty("disposition", out var disposition) &&
                    disposition.TryGetProperty("attached_pic", out var picture) && picture.GetInt32() != 0;
                if (videoIndex == -1 && !attachedPicture)
                {
                    videoIndex = stream.GetProperty("index").GetInt32();
                    height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                    codec = stream.TryGetProperty("codec_name", out var videoCodec) ? videoCodec.GetString() ?? "" : "";
                }
                else ancillary++;
            }
            else if (type == "audio")
            {
                audio.Add(new AudioTrack(stream.TryGetProperty("codec_name", out var c) ? c.GetString() ?? "" : "",
                    stream.TryGetProperty("channels", out var channels) ? channels.GetInt32() : 2));
            }
            else if (type == "subtitle") subtitles++;
            else ancillary++;
        }
        if (videoIndex < 0 || height < 1) throw new IOException("未找到可处理的视频轨道。");
        double duration = 0;
        if (root.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var d))
            double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
        if (!double.IsFinite(duration) || duration < 0) duration = 0;
        return new MediaInfo(videoIndex, codec, height, duration, audio, subtitles, ancillary);
    }

    internal static void AddEncodingOptions(List<string> args, MediaInfo media, Preset preset)
    {
        args.AddRange(new[] { "-map", $"0:{media.VideoStreamIndex}", "-map", "0:a?",
            "-c:v", "libx265", "-crf", preset.Crf.ToString(CultureInfo.InvariantCulture) });
        if (preset.MaxHeight > 0 && media.Height > preset.MaxHeight)
            args.AddRange(new[] { "-vf", $"scale=-2:{preset.MaxHeight}" });
        args.AddRange(new[] { "-c:a", "copy" });
        for (int index = 0; index < media.Audio.Count; index++)
        {
            if (string.Equals(media.Audio[index].Codec, "aac", StringComparison.OrdinalIgnoreCase)) continue;
            args.AddRange(new[] { $"-c:a:{index}", "aac", $"-b:a:{index}",
                media.Audio[index].Channels > 2 ? "384k" : "160k" });
        }
        args.AddRange(new[] { "-map_metadata", "0", "-tag:v", "hvc1", "-movflags", "+faststart" });
    }
}
