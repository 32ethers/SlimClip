using System.IO;
using System.Text.Json;

namespace SlimClip;

internal enum SaveMode { Copy, Replace }

internal sealed class Preset
{
    public int Crf { get; set; } = 28;
    public int MaxHeight { get; set; } = 1080; // 0 = preserve resolution
}

internal sealed class Settings
{
    public SaveMode SaveMode { get; set; } = SaveMode.Copy;
    public bool SkipLargerResult { get; set; } = true;
    // Empty selects user-local tools first, then tools on PATH if none are installed locally.
    public string? FfmpegDirectory { get; set; }
    public string Language { get; set; } = "auto";
    public Dictionary<string, Preset> Presets { get; set; } = new()
    {
        ["low"] = new() { Crf = 33, MaxHeight = 720 },
        ["medium"] = new() { Crf = 28, MaxHeight = 1080 },
        ["high"] = new() { Crf = 23, MaxHeight = 0 }
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlimClip", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
                if (data?.Presets is not null && Enum.IsDefined(data.SaveMode) &&
                    new[] { "low", "medium", "high" }.All(key => data.Presets.TryGetValue(key, out var preset) &&
                        preset is not null && preset.Crf is >= 0 and <= 51 &&
                        preset.MaxHeight is >= 0 and <= 16384 && (preset.MaxHeight == 0 || preset.MaxHeight % 2 == 0)))
                {
                    data.Language = NormalizeLanguage(data.Language);
                    return data;
                }
            }
        }
        catch (Exception) { /* A broken or old settings file must not prevent launch. */ }
        return new Settings();
    }

    public static string NormalizeLanguage(string? value) => value is "auto" or "en" or "zh-Hans" ? value : "auto";

    public void Save()
    {
        Language = NormalizeLanguage(Language);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
