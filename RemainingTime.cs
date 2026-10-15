using System.Diagnostics;

namespace SlimClip;

/// <summary>Approximate time remaining for one running encode/estimate, not queue wait or download.</summary>
internal sealed class RemainingTime
{
    private readonly Func<double> _now;
    private double? _startSeconds;

    public RemainingTime() : this(() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency) { }
    internal RemainingTime(Func<double> now) => _now = now;

    public string Update(double fraction)
    {
        // Callers may spend a long time probing before any video is encoded. Start at
        // the first positive encode timestamp, never at task dequeue or stage setup.
        if (!_startSeconds.HasValue && double.IsFinite(fraction) && fraction > 0 && fraction < 1)
            _startSeconds = _now();
        return Format(fraction, _startSeconds.HasValue ? _now() - _startSeconds.Value : 0);
    }

    internal static string Format(double fraction, double elapsedSeconds)
    {
        if (!double.IsFinite(fraction) || !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0 ||
            fraction < 0 || fraction > 1) return "计算中…";
        if (fraction >= 1) return "已完成";
        // After encoding, verification/safe saving can take unknown time.
        if (fraction >= 0.99) return "收尾中…";
        // Early FFmpeg timestamps are noisy; a numerical estimate would be misleading.
        if (elapsedSeconds < 3 || fraction < 0.05) return "计算中…";
        double remaining = elapsedSeconds * (1 - fraction) / fraction;
        if (!double.IsFinite(remaining) || remaining < 0) return "计算中…";
        // Avoid an unbounded or nonsensical extrapolation on badly timed/slow inputs.
        if (remaining > 7 * 24 * 3600) return "超过 7 天";
        var duration = TimeSpan.FromSeconds(Math.Max(1, Math.Ceiling(remaining)));
        if (duration.TotalDays >= 1) return duration.Hours == 0 ? $"约 {(int)duration.TotalDays} 天" :
            $"约 {(int)duration.TotalDays} 天 {duration.Hours} 小时";
        if (duration.TotalHours >= 1) return duration.Minutes == 0 ? $"约 {(int)duration.TotalHours} 小时" :
            $"约 {(int)duration.TotalHours} 小时 {duration.Minutes} 分";
        if (duration.TotalMinutes >= 1) return duration.Seconds == 0 ? $"约 {(int)duration.TotalMinutes} 分" :
            $"约 {(int)duration.TotalMinutes} 分 {duration.Seconds} 秒";
        return $"约 {duration.Seconds} 秒";
    }
}
