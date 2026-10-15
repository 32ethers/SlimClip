# SlimClip（开发中）

Windows 10/11 视频压缩工具：**传统资源管理器右键子菜单**、H.265 MP4、队列/托盘与安全保存。Windows 11 菜单预计位于**「显示更多选项」**，不承诺第一层新式菜单。[DESIGN.md](DESIGN.md) 是当前需求；新版 EXE 安装及实机菜单**尚待验证**。

## 功能

- 视频右键「压缩视频」→「低 / 中 / 高 / 预估大小 / 设置」；多选文件排入同一顺序队列，窗口关闭后继续在系统托盘处理。
- 唯一输出格式为 **H.265 视频 + MP4**。默认 CRF 33/720p、28/1080p、23/原分辨率，按原比例缩放，不放大。
- 预估分别编码中间最多 8 秒并显示实际进度，最终值是**粗略估计**，非保证大小。
- AAC 音轨直接复制、其他音轨转 AAC；尽量保留全部音轨及语言标签。字幕/额外轨道不能在首版保留，另存前明确征求同意，替换原片时拒绝丢弃。
- 默认同目录另存，结果不缩小则跳过；可在设置选择有备份恢复保护的替换。不会随安装/卸载清理用户视频。

## 安装与 FFmpeg

计划交付**每用户 EXE 安装包**，免 MSIX 和测试证书；卸载通过 Windows「设置 → 应用」。目前打包及 Windows 11「显示更多选项」中的实际菜单仍在验收中，**不能把旧版 MSIX 测试结果算作新版 EXE 通过**。未签名 EXE 仍可能出现 Windows SmartScreen 提示；不要从不可信来源下载安装器。详见[安装与卸载](packaging/INSTALL.md)和[实机验收清单](tests/windows11-acceptance.md)。用户电脑不需要 Visual Studio 或 .NET 8 SDK。

**公开安装包不含 FFmpeg/ffprobe**。首次处理视频时程序说明第三方 Gyan FFmpeg 的 GPLv3 许可，征得明确同意后让用户电脑直接从[上游](https://www.gyan.dev/ffmpeg/builds/)下载固定版本、核对 SHA-256 并保存到当前用户目录；也能在设置中选择已有工具目录。拒绝下载或校验失败不会动源视频。CI 虽使用上游 FFmpeg 进行测试，但不会上传它的二进制。

## 测试状态

此前 [CI 第十一轮](https://github.com/32ethers/SlimClip/actions/runs/38607273909)验证了 11 项引擎集成测试、FFmpeg 烟测、COM 子菜单自测及**旧 MSIX** 在一次性 Windows runner 上安装卸载。新版 EXE 的 CI、真实 Windows 11 经典菜单、多选/托盘交互及人工音画同步检查**尚未完成**，需要单独报告结果。运行 FFmpeg 烟测：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/ffmpeg-smoke.ps1`；独立 .NET 测试需要 `SLIMCLIP_FFMPEG_DIR` 指向 FFmpeg/ffprobe 目录，执行 `dotnet run --project tests/SlimClip.Tests.csproj --configuration Release`。

## 许可与公开仓库

SlimClip 自有源码采用 [MIT 许可证](LICENSE)（版权所有 © 2026 32ethers）。FFmpeg **不是 MIT**，是用户从上游直接获取的独立 GPLv3 软件；将来如果打算捆绑公开分发二进制，仍须核查[完整对应源码与分发义务](packaging/FFMPEG-SOURCE-AUDIT.md)。在把旧私有测试仓库改回 public **之前**，必须删除临时 `ci-diagnostics` 分支及从 Actions 中删除旧版含 FFmpeg 的 MSIX 测试产物；即使改回 private 也无法收回已下载的文件。

经典菜单设计依据：[微软创建快捷菜单处理程序](https://learn.microsoft.com/en-us/windows/win32/shell/context-menu-handlers)。
