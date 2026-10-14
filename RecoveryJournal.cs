using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SlimClip;

internal sealed record PendingReplacement(string Source, string Destination, string Backup, DateTime CreatedUtc);

internal static class RecoveryJournal
{
    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlimClip", "recovery");

    // Create the recovery record *before* moving or replacing any original file.
    public static string Add(string source, string destination, string backup)
    {
        Directory.CreateDirectory(Folder);
        string entry = Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".json");
        string staging = entry + ".tmp";
        try
        {
            File.WriteAllText(staging, JsonSerializer.Serialize(
                new PendingReplacement(source, destination, backup, DateTime.UtcNow)));
            File.Move(staging, entry);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
        return entry;
    }

    public static void Complete(string entry)
    {
        try { File.Delete(entry); }
        catch (IOException) { /* A leftover journal is safer than losing a recovery clue. */ }
        catch (UnauthorizedAccessException) { }
    }

    public static IReadOnlyList<PendingReplacement> FindBackups()
    {
        var pending = new List<PendingReplacement>();
        if (!Directory.Exists(Folder)) return pending;
        try
        {
            foreach (var path in Directory.EnumerateFiles(Folder, "*.json"))
            {
                try
                {
                    var item = JsonSerializer.Deserialize<PendingReplacement>(File.ReadAllText(path));
                    if (item is not null && ValidBackup(item) && File.Exists(item.Backup)) pending.Add(item);
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return pending;
    }

    private static bool ValidBackup(PendingReplacement item)
    {
        if (string.IsNullOrWhiteSpace(item.Source) || string.IsNullOrWhiteSpace(item.Destination) ||
            string.IsNullOrWhiteSpace(item.Backup)) return false;
        string source = Path.GetFullPath(item.Source), backup = Path.GetFullPath(item.Backup);
        string destination = Path.GetFullPath(item.Destination);
        string directory = Path.GetDirectoryName(source)!;
        if (!Path.IsPathFullyQualified(item.Source) || !Path.IsPathFullyQualified(item.Backup) ||
            !Path.IsPathFullyQualified(item.Destination) ||
            !string.Equals(Path.GetDirectoryName(backup), directory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(destination), directory, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, backup, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(destination, backup, StringComparison.OrdinalIgnoreCase)) return false;
        string expected = $".{Path.GetFileNameWithoutExtension(source)}.slimclip-backup-";
        string name = Path.GetFileName(backup);
        return Regex.IsMatch(name, "^" + Regex.Escape(expected) + "[0-9a-f]{32}" +
            Regex.Escape(Path.GetExtension(source)) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
