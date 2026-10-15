using System.Globalization;

namespace SlimClip;

// Keys are stable internal identifiers; neither protocol values nor file names are localized.
internal static class Loc
{
    private static readonly Dictionary<string, (string English, string Chinese)> Text = new(StringComparer.Ordinal)
    {
        ["queued"] = ("Queued", "排队中"),
        ["ready"] = ("Ready. Right-click a video file to choose a compression preset or estimate its size.", "就绪。右键视频文件，选择压缩档位或预估。"),
        ["main.title"] = ("SlimClip · Video compression", "SlimClip · 视频压缩"),
        ["tray.open"] = ("Open SlimClip", "打开 SlimClip"),
        ["exit"] = ("Exit", "退出"),
        ["settings"] = ("Settings", "设置"),
        ["cancel.current"] = ("Cancel current task", "取消当前任务"),
        ["file.column"] = ("Video file", "视频文件"),
        ["preset.column"] = ("Preset", "档位"),
        ["progress.column"] = ("Progress", "进度"),
        ["remaining.column"] = ("Time remaining", "预计剩余"),
        ["status.column"] = ("Status / result", "状态 / 结果"),
        ["backup.found"] = ("An original-file backup remains after compression. Check the files, then handle this backup manually:\n{0}", "检测到压缩后仍保留的原片备份，请确认文件无误后手动处理：\n{0}"),
        ["backup.pending"] = ("Backup needs attention", "备份待处理"),
        ["backup.restore"] = ("The original file is missing, but a backup exists:\n{0}\n\nRestore it to:\n{1}\n\nThe converted file will not be deleted.", "原文件不见了，但发现备份：\n{0}\n\n是否将它恢复到：\n{1}\n\n已有转换结果不会被删除。"),
        ["backup.restore.title"] = ("Restore original file", "恢复原文件"),
        ["backup.restore.error"] = ("{0}\nBackup remains at: {1}", "{0}\n备份仍在：{1}"),
        ["backup.restore.failed"] = ("Restore failed", "恢复失败"),
        ["exit.confirm"] = ("Exiting cancels the current task and discards queued tasks. Exit anyway?", "退出会取消当前任务，并丢弃未开始的队列。确定退出吗？"),
        ["exit.title"] = ("Exit SlimClip", "退出 SlimClip"),
        ["exit.cleaning"] = ("Cancelling tasks and cleaning temporary files…", "正在取消任务并清理临时文件…"),
        ["exit.error"] = ("Could not clean up tasks on exit. Check the original file and backup:\n{0}", "退出时清理任务失败，请检查原片和备份：\n{0}"),
        ["file.invalid"] = ("Invalid file path: {0}", "文件路径无效：{0}"),
        ["file.unsupported"] = ("Unsupported video file: {0}", "不是支持的视频文件：{0}"),
        ["preparing"] = ("Preparing", "准备中"),
        ["probe.estimate"] = ("Probing video; preparing estimate…", "探测视频，准备评估…"),
        ["probe.encode"] = ("Probing video; preparing conversion…", "探测视频，准备转换…"),
        ["download.consent"] = ("SlimClip does not include FFmpeg. Video processing requires the third-party Gyan FFmpeg (GPLv3, separate from SlimClip's MIT license).\n\nDownload it directly from Gyan's official HTTPS URL now?\n{0}\n\nAfter download, SlimClip verifies a fixed SHA-256 hash and installs only ffmpeg.exe / ffprobe.exe in the current user's %LOCALAPPDATA%\\SlimClip\\tools. Declining or a failed download does not change the source video. You can also select an existing FFmpeg folder in Settings.", "SlimClip 安装包不包含 FFmpeg。视频处理需要第三方 Gyan FFmpeg（GPLv3，与 SlimClip 的 MIT 许可不同）。\n\n是否现在从 Gyan 官方 HTTPS 地址直接下载到这台电脑？\n{0}\n\n下载后将核对固定 SHA-256，并仅安装 ffmpeg.exe / ffprobe.exe 到当前用户的 %LOCALAPPDATA%\\SlimClip\\tools。不同意或下载失败不会改动源视频。也可以在设置中选择已有 FFmpeg 目录。"),
        ["download.title"] = ("First use: authorize FFmpeg download", "首次使用：授权下载 FFmpeg"),
        ["download.declined"] = ("FFmpeg download not authorized; original video unchanged. Specify an existing tools folder in Settings.", "未授权下载 FFmpeg；原视频未改动。可在设置中指定已有工具目录。"),
        ["estimate.size"] = ("{0}: approximately {1:F1} MiB", "{0}：约 {1:F1} MiB"),
        ["estimate.done"] = ("Estimate complete: {0}", "预估已完成：{0}"),
        ["estimate.result"] = ("Source file: {0:F1} MiB\n{1}\n\nProjected from a sample in the middle; scene changes can cause substantial error. This is not a size guarantee.", "源文件：{0:F1} MiB\n{1}\n\n根据中间片段取样推算，场景变化可能使误差很大；不是大小保证。"),
        ["estimate.title"] = ("Estimated video size", "视频大小预估"),
        ["tracks.consent"] = ("{0} contains {1} subtitle track(s) and {2} additional video/data track(s). This version keeps only the main video and all audio tracks; the original is retained. Continue and save a copy?", "{0} 包含 {1} 条字幕和 {2} 条额外视频/数据轨。首版输出只保留主视频与全部音轨；原文件将保留。是否继续另存？"),
        ["tracks.title"] = ("Track warning", "轨道提示"),
        ["tracks.declined"] = ("Cancelled: output would omit additional tracks", "用户取消：输出会省略额外轨道"),
        ["encoding"] = ("Converting", "转换中"),
        ["finished"] = ("Finished", "已结束"),
        ["cancelled"] = ("Cancelled", "已取消"),
        ["failed"] = ("Failed: {0}", "失败：{0}"),
        ["level.low"] = ("Low", "低"),
        ["level.medium"] = ("Medium", "中"),
        ["level.high"] = ("High", "高"),
        ["settings.title"] = ("SlimClip · Compression settings", "SlimClip · 压缩设置"),
        ["settings.format"] = ("Output format is always H.265 video in an MP4 container.", "输出格式固定为 H.265 视频 + MP4 容器。"),
        ["settings.output"] = ("After compression:", "压缩后文件："),
        ["settings.copy"] = ("Create a new file in the same folder (recommended)", "同目录生成新文件（推荐）"),
        ["settings.replace"] = ("Replace original after success (not allowed with subtitles)", "成功后替换原文件（有字幕时禁止）"),
        ["settings.skip"] = ("Do not save a result that is not smaller than the original", "结果不小于原文件时，不保存压缩结果"),
        ["settings.crf.help"] = ("Higher CRF usually means a smaller file and lower quality; height 0 preserves original resolution. Never upscale.", "CRF 越大文件通常越小、画质越低；高度 0 表示保持原分辨率。不放大原片。"),
        ["settings.height"] = ("Maximum height (pixels)", "最大高度 (像素)"),
        ["settings.tools"] = ("FFmpeg tools folder (contains ffmpeg.exe and ffprobe.exe):", "FFmpeg 工具目录（已有 ffmpeg.exe 和 ffprobe.exe）："),
        ["settings.browse"] = ("Browse…", "浏览…"),
        ["settings.tools.tip"] = ("A configured folder takes priority. Leave blank to use downloaded user-local tools, then a complete ffmpeg.exe + ffprobe.exe pair from an absolute PATH entry; otherwise download requires consent.", "指定的目录优先；留空则先使用用户目录已下载工具，再查绝对 PATH 条目中的 ffmpeg.exe + ffprobe.exe 完整组合；仍未找到时须授权下载。"),
        ["settings.folder.select"] = ("Select the folder containing ffmpeg.exe and ffprobe.exe", "选择包含 ffmpeg.exe 和 ffprobe.exe 的目录"),
        ["settings.tools.help"] = ("Order: configured folder, downloaded user-local tools, a complete pair on absolute PATH, then consent to download. No automatic download. SlimClip does not bundle GPLv3 FFmpeg.", "顺序：指定目录、用户目录已下载工具、绝对 PATH 中完整工具组合，最后征得同意后下载。不会自动下载。SlimClip 不附带 GPLv3 FFmpeg。"),
        ["settings.language"] = ("Language (takes effect after restart):", "语言（重启后生效）："),
        ["language.auto"] = ("Auto", "自动"),
        ["language.english"] = ("English", "English"),
        ["language.chinese"] = ("简体中文", "简体中文"),
        ["settings.save"] = ("Save settings", "保存设置"),
        ["settings.invalid.preset"] = ("Invalid {0} preset: CRF must be 0–51; height must be 0 or an even number from 2–16384.", "{0} 档参数无效：CRF 应为 0–51，高度为 0 或 2–16384 的偶数。"),
        ["settings.invalid.tools"] = ("Select an absolute folder containing both ffmpeg.exe and ffprobe.exe, or leave it blank to use the user-local tools or PATH.", "请选择同时包含 ffmpeg.exe 和 ffprobe.exe 的绝对目录；留空则使用用户目录中的工具或 PATH。"),
        ["settings.save.failed"] = ("Could not save settings", "无法保存设置"),
        ["settings.restart"] = ("Settings saved. Language changes take effect after restarting SlimClip. If SlimClip is already running (including in the notification area), exit that instance and restart it; commands sent to it via IPC retain its current language.", "设置已保存。语言更改需重启 SlimClip 才生效。若 SlimClip 已在运行（包括通知区域中的实例），请退出该实例并重新启动；通过 IPC 发送给现有实例的命令仍使用其当前语言。"),
        ["instance.connect.failed"] = ("Could not connect to the existing SlimClip window. Please try again later.", "无法连接现有 SlimClip 窗口，请稍后重试。"),
        ["compression.unsupported"] = ("Video file does not exist or its extension is unsupported.", "视频文件不存在或扩展名不受支持。"),
        ["compression.invalid.preset"] = ("Invalid compression preset parameters.", "压缩档位参数无效。"),
        ["compression.replace.tracks"] = ("Source contains subtitles, additional video tracks or data tracks that cannot be preserved in this version. Do not replace the original; save a copy instead.", "源文件含字幕、额外视频轨或数据轨，首版无法保留；禁止替换原文件，请改为另存。"),
        ["compression.confirm.tracks"] = ("Source contains unhandled tracks. Confirm that the saved copy will omit these tracks first.", "源文件包含未处理轨道，必须先确认另存结果将不含这些轨道。"),
        ["compression.target.exists"] = ("Target MP4 already exists; cannot overwrite another file: {0}", "目标 MP4 已存在，不能覆盖其他文件：{0}"),
        ["compression.ffmpeg.failed"] = ("FFmpeg conversion failed: {0}", "FFmpeg 转换失败：{0}"),
        ["compression.no.output"] = ("FFmpeg did not produce a valid result.", "FFmpeg 未生成有效结果。"),
        ["compression.verify.failed"] = ("Output track or duration validation failed; original unchanged.", "输出轨道或时长校验失败，原片未改动。"),
        ["compression.source.changed"] = ("Source video changed during compression; result was not saved.", "源视频在压缩期间发生变化，结果未保存。"),
        ["compression.not.smaller"] = ("Result is not smaller; original unchanged.", "结果未缩小，原文件未改动。"),
        ["compression.target.exists.short"] = ("Target MP4 already exists: {0}", "目标 MP4 已存在：{0}"),
        ["compression.rollback.failed"] = ("Rollback failed! Original backup remains at {0}: {1}", "回滚失败！原片备份保存在 {0}：{1}"),
        ["compression.saved"] = ("Saved: {0}", "已保存：{0}"),
        ["compression.saved.backup"] = ("Saved: {0}; manually remove old-file backup: {1}", "已保存：{0}；旧文件备份需手动清理：{1}"),
        ["estimator.unsupported"] = ("Select a supported video file.", "请选择支持的视频文件。"),
        ["estimator.probing"] = ("Probing video tracks…", "探测视频轨道…"),
        ["estimator.no.duration"] = ("Cannot determine video duration for estimation.", "无法获取视频时长，不能估算。"),
        ["estimator.stage"] = ("Estimating {0} ({1}/3)", "评估 {0}（{1}/3）"),
        ["estimator.encode.failed"] = ("Estimation encoding failed: {0}", "预估编码失败：{0}"),
        ["estimator.no.output"] = ("Estimate sample was not produced.", "评估片段未生成。"),
        ["estimator.too.large"] = ("Video is too long or large for a reliable estimate.", "视频过长或文件过大，无法可靠估算。"),
        ["eta.calculating"] = ("Calculating…", "计算中…"),
        ["eta.done"] = ("Completed", "已完成"),
        ["eta.finishing"] = ("Finishing…", "收尾中…"),
        ["eta.over.week"] = ("Over 7 days", "超过 7 天"),
        ["eta.days"] = ("About {0} day(s)", "约 {0} 天"),
        ["eta.days.hours"] = ("About {0} day(s) {1} hour(s)", "约 {0} 天 {1} 小时"),
        ["eta.hours"] = ("About {0} hour(s)", "约 {0} 小时"),
        ["eta.hours.minutes"] = ("About {0} hour(s) {1} minute(s)", "约 {0} 小时 {1} 分"),
        ["eta.minutes"] = ("About {0} minute(s)", "约 {0} 分"),
        ["eta.minutes.seconds"] = ("About {0} minute(s) {1} second(s)", "约 {0} 分 {1} 秒"),
        ["eta.seconds"] = ("About {0} second(s)", "约 {0} 秒"),
        ["media.unknown.tool"] = ("Unknown media tool.", "未知媒体工具。"),
        ["media.configured.missing"] = ("The configured FFmpeg folder is missing ffmpeg.exe or ffprobe.exe; choose another folder in Settings.", "设置的 FFmpeg 目录缺少 ffmpeg.exe 或 ffprobe.exe；请在设置中重新选择目录。"),
        ["media.not.found"] = ("FFmpeg was not found. Authorize the first-use download, select a tools folder in Settings, or add a folder containing ffmpeg.exe and ffprobe.exe to PATH.", "尚未找到 FFmpeg。请同意首次下载、在设置中选择工具目录，或将包含 ffmpeg.exe 和 ffprobe.exe 的目录加入 PATH。"),
        ["media.start.null"] = ("Could not start {0}.", "无法启动 {0}。"),
        ["media.start.failed"] = ("Could not start {0}.exe: {1}. Check the FFmpeg folder in Settings or download it again.", "无法启动 {0}.exe：{1}。请在设置中检查 FFmpeg 目录或重新下载。"),
        ["media.tool.failed"] = ("{0} failed: {1}", "{0} 失败：{1}"),
        ["media.tracks.unreadable"] = ("Could not read media tracks.", "无法读取媒体轨道。"),
        ["media.video.missing"] = ("No processable video track was found.", "未找到可处理的视频轨道。"),
        ["setup.downloading"] = ("Downloading FFmpeg from Gyan's official website…", "正在从 Gyan 官方网站下载 FFmpeg…"),
        ["setup.non.https"] = ("The final official download URL was not HTTPS; installation refused.", "官网下载最终地址不是 HTTPS，已拒绝安装。"),
        ["setup.http.failed"] = ("Official download failed (HTTP {0}); source video unchanged.", "官网下载失败（HTTP {0}）；不会修改源视频。"),
        ["setup.zip.too.large"] = ("Download exceeds the safety size limit.", "下载文件超过安全大小限制。"),
        ["setup.download.progress"] = ("Downloading FFmpeg: {0:F1} MiB", "正在下载 FFmpeg：{0:F1} MiB"),
        ["setup.download.total"] = (" / {0:F1} MiB", " / {0:F1} MiB"),
        ["setup.hash.failed"] = ("FFmpeg download SHA-256 does not match the pinned hash. Installation refused; check your connection and retry.", "FFmpeg 下载包 SHA-256 与固定校验值不符，已拒绝安装。请检查网络后重试。"),
        ["setup.extracting"] = ("Hash verified; safely extracting ffmpeg.exe and ffprobe.exe…", "校验通过，正在安全提取 ffmpeg.exe 和 ffprobe.exe…"),
        ["setup.zip.unsafe.path"] = ("Download contains an unsafe ZIP path; installation refused.", "下载包含不安全的 ZIP 路径，已拒绝安装。"),
        ["setup.invalid.tools"] = ("Downloaded FFmpeg executable is invalid or duplicated.", "下载包中的 FFmpeg 可执行文件无效或重复。"),
        ["setup.missing.tools"] = ("Download is missing a required FFmpeg tool.", "下载包缺少必要的 FFmpeg 工具。"),
        ["setup.tool.too.large"] = ("Executable exceeds the safety size limit.", "可执行文件超过安全大小限制。"),
        ["setup.length.failed"] = ("Extracted file length validation failed.", "解压文件长度校验失败。"),
        ["setup.incomplete"] = ("Existing tools folder is incomplete; inspect it manually and retry: {0}", "工具目录已存在但不完整；请手动检查后重试：{0}"),
        ["setup.installed"] = ("FFmpeg installed in the current user's folder.", "FFmpeg 已安装到当前用户目录。")
    };

    private static bool _chinese;
    public static IReadOnlyCollection<string> Keys => Text.Keys;

    public static void Configure(string? language, CultureInfo? systemCulture = null)
    {
        _chinese = language switch
        {
            "zh-Hans" => true,
            "en" => false,
            _ => IsSimplified(systemCulture ?? CultureInfo.CurrentUICulture)
        };
    }

    private static bool IsSimplified(CultureInfo culture)
    {
        string name = culture.Name;
        return name.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("zh-Hans-", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("zh-SG", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("zh-MY", StringComparison.OrdinalIgnoreCase);
    }

    public static string T(string key)
    {
        if (!Text.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value.English))
            throw new KeyNotFoundException("Missing English localization key: " + key);
        if (_chinese && string.IsNullOrWhiteSpace(value.Chinese))
            throw new KeyNotFoundException("Missing Simplified Chinese localization key: " + key);
        return _chinese ? value.Chinese : value.English;
    }

    public static string F(string key, params object?[] args) => string.Format(
        _chinese ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.GetCultureInfo("en-US"), T(key), args);
}
