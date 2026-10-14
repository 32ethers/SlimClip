// Standalone integration-test entry point (no test runner or third-party NuGet packages).
// CI (Windows, .NET 8 SDK, FFmpeg with libx265):
//   $env:SLIMCLIP_FFMPEG_DIR = "C:\\path\\to\\ffmpeg\\bin"
//   dotnet run --project tests/SlimClip.Tests.csproj --configuration Release
// Alternatively: dotnet run --project tests/SlimClip.Tests.csproj -- --ffmpeg-dir "C:\\path\\to\\ffmpeg\\bin"
// ffmpeg.exe and ffprobe.exe are copied into this test executable's bin directory because
// production MediaTools resolves both exclusively against AppContext.BaseDirectory.
// All media fixtures and outputs live in a uniquely named temporary directory, never in a user's video directory.
using System.Diagnostics;
using System.Security.Cryptography;
using SlimClip;

internal static class Program
{
    private static readonly Preset Fast = new() { Crf = 36, MaxHeight = 0 };
    private static string _root = "";
    private static int _failures;

    private static async Task<int> Main(string[] args)
    {
        try
        {
            string? toolDirectory = args.Length == 0 ? Environment.GetEnvironmentVariable("SLIMCLIP_FFMPEG_DIR") :
                args.Length == 2 && args[0] == "--ffmpeg-dir" ? args[1] : null;
            if (string.IsNullOrWhiteSpace(toolDirectory) || !Path.IsPathFullyQualified(toolDirectory))
                throw new ArgumentException("Set SLIMCLIP_FFMPEG_DIR or pass --ffmpeg-dir <absolute directory containing ffmpeg.exe and ffprobe.exe>.");
            StageTools(toolDirectory);
            _root = Path.Combine(Path.GetTempPath(), "SlimClip.Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            await Run("AAC stream-copy and copy-mode preservation", AacCopyAsync);
            await Run("Non-AAC audio is converted", NonAacAsync);
            await Run("Copy mode does not overwrite existing output", CopyCollisionAsync);
            await Run("Replace mode preserves input until valid output", ReplaceAsync);
            await Run("Replace mode refuses a distinct MP4 collision", ReplaceCollisionAsync);
            await Run("Subtitle requires consent and forbids replacement", SubtitleAsync);
            await Run("Extra video track requires consent and forbids replacement", ExtraVideoAsync);
            await Run("Estimator returns three levels and progress", EstimateAsync);
            await Run("Cancellation never commits or changes the source", CancellationAsync);
            Console.WriteLine($"Result: {9 - _failures}/9 passed, {_failures} failed.");
            return _failures == 0 ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Test setup failed: " + error);
            return 1;
        }
        finally
        {
            if (_root.Length != 0 && Directory.Exists(_root))
            {
                try { Directory.Delete(_root, recursive: true); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { Console.Error.WriteLine("Could not remove test temporary directory: " + _root + ": " + error.Message); }
            }
        }
    }

    private static void StageTools(string sourceDirectory)
    {
        foreach (string name in new[] { "ffmpeg.exe", "ffprobe.exe" })
        {
            string source = Path.GetFullPath(Path.Combine(sourceDirectory, name));
            string destination = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, name));
            Check(File.Exists(source), "FFmpeg executable not found: " + source);
            // Never replace a pre-existing binary in bin, even if the caller supplies a different directory.
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(destination))
            {
                Check(SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(destination))),
                    "Different tool already exists in test bin; clean test output directory first: " + destination);
            }
            else File.Copy(source, destination, overwrite: false);
        }
        Check(File.Exists(MediaTools.Executable("ffmpeg")) && File.Exists(MediaTools.Executable("ffprobe")),
            "Test tools must be accessible from AppContext.BaseDirectory.");
    }

    private static async Task Run(string name, Func<Task> test)
    {
        try { await test(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { _failures++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
    }

    private static string Case(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static Settings Options(SaveMode mode) => new() { SaveMode = mode, SkipLargerResult = false };

    private static async Task<string> Fixture(string folder, string name, string audio = "aac")
    {
        string path = Path.Combine(folder, name);
        var args = new List<string> { "-hide_banner", "-nostdin", "-loglevel", "error", "-f", "lavfi", "-i",
            "color=c=blue:s=128x72:r=10:d=1.2", "-f", "lavfi", "-i",
            "sine=frequency=660:sample_rate=44100:duration=1.2", "-map", "0:v:0", "-map", "1:a:0",
            "-c:v", "mpeg4", "-q:v", "12", "-c:a", audio, "-threads", "1", "-y", path };
        await Tool("ffmpeg", args.ToArray());
        Check(File.Exists(path) && new FileInfo(path).Length > 0, "Fixture is missing: " + path);
        return path;
    }

    private static async Task AacCopyAsync()
    {
        string folder = Case("aac-copy"), source = await Fixture(folder, "aac.mp4");
        string before = Digest(source);
        string packets = await AudioPacketHashes(source);
        var values = new List<double>();
        var result = await Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Copy), false,
            values.Add, CancellationToken.None);
        string output = Path.Combine(folder, "aac_slimclip_low.mp4");
        Check(result.Success && File.Exists(output), "Copy-mode output is missing.");
        Check(Digest(source) == before, "Copy mode changed the source.");
        var probe = await MediaTools.ProbeAsync(output, CancellationToken.None);
        Check(probe.VideoCodec == "hevc" && probe.Audio.Count == 1 && probe.Audio[0].Codec == "aac", "Wrong output codecs.");
        Check(packets.Length > 0 && packets == await AudioPacketHashes(output), "AAC packets were transcoded instead of copied.");
        Check(values.Count > 0 && values[^1] == 1, "Compression did not report completion.");
    }

    private static async Task NonAacAsync()
    {
        string folder = Case("non-aac"), source = await Fixture(folder, "mp3.mkv", "libmp3lame");
        string before = Digest(source);
        Check((await MediaTools.ProbeAsync(source, CancellationToken.None)).Audio[0].Codec == "mp3", "Fixture is not MP3.");
        var result = await Compression.RunAsync(new(source, "medium"), Fast, Options(SaveMode.Copy), false,
            _ => { }, CancellationToken.None);
        string output = Path.Combine(folder, "mp3_slimclip_medium.mp4");
        Check(result.Success && File.Exists(output), "Converted output is missing.");
        Check((await MediaTools.ProbeAsync(output, CancellationToken.None)).Audio.Single().Codec == "aac", "MP3 was not converted to AAC.");
        Check(Digest(source) == before, "Original non-AAC file changed.");
    }

    private static async Task CopyCollisionAsync()
    {
        string folder = Case("copy-collision"), source = await Fixture(folder, "clip.mp4");
        string occupied = Path.Combine(folder, "clip_slimclip_low.mp4");
        byte[] marker = { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(occupied, marker);
        var result = await Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Copy), false,
            _ => { }, CancellationToken.None);
        Check(result.Success && File.ReadAllBytes(occupied).SequenceEqual(marker), "Existing copy destination was overwritten.");
        Check(File.Exists(Path.Combine(folder, "clip_slimclip_low_2.mp4")), "Unique copy output was not created.");
    }

    private static async Task ReplaceAsync()
    {
        string folder = Case("replace"), source = await Fixture(folder, "replace.mp4");
        string before = Digest(source);
        var result = await Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Replace), false,
            _ => { }, CancellationToken.None);
        Check(result.Success && File.Exists(source) && Digest(source) != before, "Replace did not commit new bytes.");
        Check((await MediaTools.ProbeAsync(source, CancellationToken.None)).VideoCodec == "hevc", "Replacement is not HEVC.");
        Check(!Directory.EnumerateFiles(folder, ".replace.slimclip-backup-*").Any(), "Old backup unexpectedly remains.");
    }

    private static async Task ReplaceCollisionAsync()
    {
        string folder = Case("replace-collision"), source = await Fixture(folder, "collision.mkv", "libmp3lame");
        string occupied = await Fixture(folder, "collision.mp4");
        string original = Digest(source), existing = Digest(occupied);
        await ThrowsIOException(() => Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Replace), false,
            _ => { }, CancellationToken.None), "目标 MP4 已存在");
        Check(Digest(source) == original && Digest(occupied) == existing, "Replace collision modified existing media.");
    }

    private static async Task SubtitleAsync()
    {
        string folder = Case("subtitle"), original = await Fixture(folder, "plain.mp4");
        string subtitle = Path.Combine(folder, "sub.srt"), source = Path.Combine(folder, "subtitled.mp4");
        await File.WriteAllTextAsync(subtitle, "1\n00:00:00,000 --> 00:00:01,000\nHello\n");
        await Tool("ffmpeg", "-hide_banner", "-nostdin", "-loglevel", "error", "-i", original,
            "-f", "srt", "-i", subtitle, "-map", "0:v", "-map", "0:a", "-map", "1:0",
            "-c", "copy", "-c:s", "mov_text", "-y", source);
        Check((await MediaTools.ProbeAsync(source, CancellationToken.None)).SubtitleCount == 1, "Subtitle fixture has no subtitle.");
        string before = Digest(source);
        await ThrowsIOException(() => Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Replace), true,
            _ => { }, CancellationToken.None), "禁止替换");
        await ThrowsIOException(() => Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Copy), false,
            _ => { }, CancellationToken.None), "必须先确认");
        Check(Digest(source) == before && !File.Exists(Path.Combine(folder, "subtitled_slimclip_low.mp4")),
            "Forbidden subtitle operation wrote output or changed input.");
        var result = await Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Copy), true,
            _ => { }, CancellationToken.None);
        Check(result.Success && Digest(source) == before, "Consent-based copy damaged subtitle source.");
        Check((await MediaTools.ProbeAsync(Path.Combine(folder, "subtitled_slimclip_low.mp4"), CancellationToken.None)).SubtitleCount == 0,
            "Consent-based copy retained subtitle unexpectedly.");
    }

    private static async Task ExtraVideoAsync()
    {
        string folder = Case("extra-video"), original = await Fixture(folder, "plain.mp4");
        string source = Path.Combine(folder, "extra.mp4");
        await Tool("ffmpeg", "-hide_banner", "-nostdin", "-loglevel", "error", "-i", original,
            "-map", "0:v", "-map", "0:v", "-map", "0:a", "-c", "copy", "-y", source);
        Check((await MediaTools.ProbeAsync(source, CancellationToken.None)).AncillaryCount == 1, "Extra-track fixture has no extra video.");
        string before = Digest(source);
        await ThrowsIOException(() => Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Replace), true,
            _ => { }, CancellationToken.None), "禁止替换");
        await ThrowsIOException(() => Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Copy), false,
            _ => { }, CancellationToken.None), "必须先确认");
        Check(Digest(source) == before, "Forbidden extra-track operation changed input.");
        var result = await Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Copy), true,
            _ => { }, CancellationToken.None);
        Check(result.Success && (await MediaTools.ProbeAsync(Path.Combine(folder, "extra_slimclip_low.mp4"),
            CancellationToken.None)).AncillaryCount == 0, "Extra video track was not omitted by consent.");
    }

    private static async Task EstimateAsync()
    {
        string source = await Fixture(Case("estimate"), "estimate.mp4");
        var statuses = new List<(string Label, double? Progress)>();
        var result = await Estimator.RunAsync(source, new Settings(),
            (label, progress) => statuses.Add((label, progress)), CancellationToken.None);
        Check(result.Select(x => x.Level).SequenceEqual(new[] { "low", "medium", "high" }), "Estimator levels are missing or unordered.");
        Check(result.All(x => x.Bytes > 0 && x.SampleSeconds > 0 && x.SampleSeconds <= 8), "Invalid size or sample duration.");
        string[] labels = { "评估 低（1/3）", "评估 中（2/3）", "评估 高（3/3）" };
        for (int index = 0; index < 3; index++)
        {
            string label = labels[index];
            Check(statuses.Any(x => x.Label == label && x.Progress == index / 3.0), "Missing stage start: " + label);
            Check(statuses.Any(x => x.Label == label && x.Progress == (index + 1) / 3.0), "Missing stage completion: " + label);
        }
        Check(statuses.First().Progress is null && statuses.First().Label.Contains("探测"), "Probe status is missing.");
        Check(statuses.Where(x => x.Progress.HasValue).All(x => x.Progress >= 0 && x.Progress <= 1), "Invalid estimate progress.");
    }

    private static async Task CancellationAsync()
    {
        string folder = Case("cancel"), source = await Fixture(folder, "cancel.mp4");
        string before = Digest(source);
        using var cancellation = new CancellationTokenSource();
        bool reported = false;
        try
        {
            await Compression.RunAsync(new(source, "low"), Fast, Options(SaveMode.Replace), false,
                _ => { reported = true; cancellation.Cancel(); }, cancellation.Token);
            throw new Exception("A cancelled conversion returned successfully.");
        }
        catch (OperationCanceledException) { }
        Check(reported, "No compression progress was reported before cancellation.");
        Check(Digest(source) == before, "Cancellation modified the source video.");
        Check(!Directory.EnumerateFiles(folder, ".slimclip-*.mp4").Any(), "Cancellation left a temporary MP4.");
    }

    private static async Task ThrowsIOException(Func<Task> action, string expectedMessage)
    {
        try { await action(); }
        catch (IOException error) when (error.Message.Contains(expectedMessage, StringComparison.Ordinal)) { return; }
        throw new Exception("Expected IOException containing: " + expectedMessage);
    }

    private static string Digest(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static async Task<string> AudioPacketHashes(string file) => (await Tool("ffprobe", "-v", "error", "-select_streams", "a:0",
        "-show_packets", "-show_data_hash", "sha256", "-show_entries", "packet=data_hash", "-of", "csv=p=0", file)).Trim();

    private static async Task<string> Tool(string name, params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(MediaTools.Executable(name))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        } };
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var registration = deadline.Token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(deadline.Token);
        string output = await stdout, error = await stderr;
        Check(process.ExitCode == 0, name + " failed: " + error);
        return output;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
