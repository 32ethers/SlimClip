# SlimClip（预览版 / Preview）

![胶片与向内压缩箭头的 SlimClip 标志](assets/SlimClip.png)

Windows 10/11 视频压缩工具：**传统资源管理器右键子菜单**、H.265 MP4、队列/托盘与安全保存。Windows 11 菜单预计位于**「显示更多选项」**，不承诺第一层新式菜单。[DESIGN.md](DESIGN.md) 是当前需求；新版 EXE 在隔离 CI 安装／卸载已通过，Windows 11 实机菜单**尚待验证**。

## 功能

- 胶片、播放与向内箭头的原创图标用于窗口、托盘、EXE、安装器和右键入口；视频右键「压缩视频」→「低 / 中 / 高 / 预估大小 / 设置」；多选文件排入同一顺序队列，窗口关闭后继续在系统托盘处理。
- **English / 简体中文**：界面在「设置 → 语言」选择跟随系统、English 或简体中文，切换后退出托盘中的现有进程并重启生效。右键菜单按 Windows 用户显示语言独立呈现英语/简体中文；繁体中文及其他未支持语言回退英语。安装向导提供两种语言。
- 唯一输出格式为 **H.265 视频 + MP4**。默认 CRF 33/720p、28/1080p、23/原分辨率，按原比例缩放，不放大。
- 压缩与三档预估显示进度条及按当前编码速度推算的**预计剩余时间**；刚开始显示「计算中」，不同档位速度会变，时间仅为粗略推算，收尾校验不承诺准确倒计时。预估分别编码中间最多 8 秒，最终大小仅是**粗略估计**。
- AAC 音轨直接复制、其他音轨转 AAC；尽量保留全部音轨及语言标签。字幕/额外轨道不能在首版保留，另存前明确征求同意，替换原片时拒绝丢弃。
- 默认同目录另存，结果不缩小则跳过；可在设置选择有备份恢复保护的替换。不会随安装/卸载清理用户视频。

## 安装与 FFmpeg

交付**每用户 EXE 安装包**，无需 MSIX 或测试证书；卸载通过 Windows「设置 → 应用」。发布后优先在 [Releases](https://github.com/32ethers/SlimClip/releases) 获取对应预览版安装器及 SHA-256；也可从[Windows EXE 工作流](https://github.com/32ethers/SlimClip/actions/workflows/windows-exe.yml)最新成功运行的 Artifacts 下载 `SlimClip-EXE-Installer-NO-FFMPEG`（内含 `SlimClip-Setup.exe`）；产物保留 7 天，过期可由维护者重新运行工作流。未签名 EXE 可能触发 SmartScreen，请核对下载来源。无需 Visual Studio 或 .NET 8 SDK。详见[安装与卸载](packaging/INSTALL.md)及[实机验收清单](tests/windows11-acceptance.md)。

**安装包与源码仓库均不附带 FFmpeg/ffprobe**。首次压缩或预估前，程序告知第三方 [Gyan FFmpeg](https://www.gyan.dev/ffmpeg/builds/) 的来源及 GPLv3 许可；用户明确同意后，由其电脑直接从固定上游地址下载固定版本、校验 SHA-256 并保存到当前用户目录；也可在设置中指定已有工具目录。未指定时优先使用已授权下载到用户目录的工具，之后按 `PATH` 顺序寻找**同一绝对目录**下成对的 `ffmpeg.exe` 与 `ffprobe.exe`；如果仍找不到，才询问是否下载。不会从工作目录解析相对程序名，也不通过 `cmd.exe` 执行。PATH 中已有工具由用户负责确认来源与对应许可；刚修改 PATH 时须重启 SlimClip，必要时重新登录使 Explorer 继承新环境。拒绝或校验失败不会改动源视频。

## English quick start

Download the unsigned `SlimClip-Setup.exe` from [Releases](https://github.com/32ethers/SlimClip/releases) (or a successful [Windows build](https://github.com/32ethers/SlimClip/actions/workflows/windows-exe.yml)). On Windows 11, use **right-click → Show more options → Compress Video**; select a preset or estimate. In **Settings → Language**, choose Automatic, English, or 简体中文, then fully quit and restart SlimClip to apply a change. SlimClip first uses a selected FFmpeg folder, then already authorized user-local tools, then a complete `ffmpeg.exe`/`ffprobe.exe` pair found in one absolute `PATH` entry. Only if none exists will it ask for consent to download FFmpeg directly from upstream; the installer contains no FFmpeg. See the [bilingual preview notes](packaging/RELEASE_NOTES.md) and [manual checklist](tests/windows11-acceptance.md); Windows 11 visual/audio acceptance is still pending.

## 测试状态

[Windows CI 第十四轮](https://github.com/32ethers/SlimClip/actions/runs/38718474279)已通过 14 项测试（引擎、双语资源、PATH 工具优先级、剩余时间及慢探测）、FFmpeg 下载校验、COM 子菜单与图标自测、无 FFmpeg 的 EXE 构建，以及两种安装语言参数的安装／注册／卸载冒烟。**Windows 11「显示更多选项」中的实际菜单、首次授权界面、多选/托盘交互及音画同步尚未获人工实机确认**；CI 注册检查不等于视觉验收。测试步骤见[安装说明](packaging/INSTALL.md)。

## 许可与公开仓库

SlimClip 自有代码采用 [MIT 许可证](LICENSE)（© 2026 32ethers）；程序提供下载的 Gyan FFmpeg 构建是独立的 GPLv3 软件，用户自行指定的其他构建则以其实际许可为准，**不能把 FFmpeg 误称为 MIT 或无需关注其许可**。当前安装包不再分发 FFmpeg 二进制，因此没有因该安装包分发 GPL 二进制而产生的相应再分发义务；首次使用仍告知来源与许可。未来如自行捆绑分发，须先核查[对应源码和许可义务](packaging/FFMPEG-SOURCE-AUDIT.md)。旧版含 FFmpeg 的 MSIX 测试产物曾短暂公开，已[删除](https://github.com/32ethers/SlimClip/actions/runs/38697950751)，但无法撤回此前可能被下载的副本。

经典菜单设计依据：[微软创建快捷菜单处理程序](https://learn.microsoft.com/en-us/windows/win32/shell/context-menu-handlers)。
