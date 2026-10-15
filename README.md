# SlimClip（开发中）

Windows 11 文件资源管理器新式右键菜单的视频压缩入口。**11 项引擎测试及完整签名 MSIX 在 Windows CI 通过；隔离 runner 安装/卸载已验证，Windows 11 资源管理器实机验收尚未完成。**产品规格以 [DESIGN.md](DESIGN.md) 为准。

## 首版功能

- 视频右键「压缩视频」→「低 / 中 / 高 / 预估大小 / 设置」；多个文件加入同一个顺序队列，进度窗口关闭后留在系统托盘。
- 唯一输出格式为 **H.265 视频 + MP4 容器**。三个档位分别设置 CRF 和最大输出高度（默认 33/720p、28/1080p、23/原分辨率）；分辨率不会放大。
- 预估会逐档编码源视频中间最多 8 秒，显示正在评估的档位及进度条，得到的是**粗略**的预计大小，并非大小保证。
- AAC 音轨直接复制，非 AAC 音轨自动编码为 AAC，尽量保留全部音轨。首版不保留字幕及其他附加轨道：另存前需明确同意丢弃，替换原文件则直接拒绝。
- 默认同目录另存新文件；设置可改为成功后替换原片。结果不小于原片默认不保存；异扩展名替换时目标 MP4 已存在则报冲突。发生异常时优先保留原片或备份，启动时提示仍存在的原片备份。

## 安装与卸载

计划交付**完整 MSIX 安装包**（含主程序、Windows 11 原生菜单 DLL、FFmpeg/ffprobe），从 Windows「设置 → 应用 → 已安装的应用」卸载并移除右键菜单。源码不等于安装包；**仅私有仓库手动运行 CI 或使用专用 `[private-test-msix]` 提交标记才上传测试包及公钥证书**，公开仓库禁止上传内置 FFmpeg 的包。测试版 MSIX 需显式信任公钥测试证书，不能把签名私钥公开或静默安装证书。签名、安装及卸载细节参见 [packaging/INSTALL.md](packaging/INSTALL.md)；实机检查条目见 [Windows 11 验收清单](tests/windows11-acceptance.md)。

采用 WPF/.NET 8 主程序与原生 C++ `IExplorerCommand` 菜单 DLL，GitHub Actions 在 Windows runner 上构建；**用户电脑不需要 Visual Studio**。请勿用传统注册表菜单冒充 Windows 11 新式菜单。

## 本地可运行的有限测试

使用 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/ffmpeg-smoke.ps1` 已验证 H.264/AAC → H.265 MP4、AAC 音轨直接复制、Opus → AAC、混合多音轨、语言标记、`-progress` 输出和取样编码。独立的 `tests/SlimClip.Tests.csproj` 含 11 个实际引擎集成测试，均已在 Windows CI 通过，由 CI 执行（需要设置 `SLIMCLIP_FFMPEG_DIR` 指向工具目录，再运行 `dotnet run --project tests/SlimClip.Tests.csproj --configuration Release`）。**[GitHub Actions 第十一轮](https://github.com/32ethers/SlimClip/actions/runs/38607273909)已通过 11 项引擎测试、FFmpeg 烟测、原生菜单 DLL 编译及直接调用的 COM 菜单自测、完整 MSIX 测试签名及 Windows Server 2025 一次性 runner 的安装/卸载与证书清理。CI 不公开上传包含 FFmpeg 的包；Windows 11 新式右键菜单、双击安装及从系统设置卸载仍未实机验证。**

## 开源许可

SlimClip 自有源码采用 [MIT 许可证](LICENSE)（版权所有 © 2026 32ethers）。随包使用的第三方 FFmpeg/ffprobe **不是 MIT**：所选 [Gyan 构建](https://www.gyan.dev/ffmpeg/builds/)标注 GPLv3；其许可证与已核实的来源信息另存于包内 `licenses/FFmpeg/`。公开提供包含该静态二进制的 MSIX 前，仍须核实并提供完整对应源码、依赖和构建脚本；仅开源 SlimClip 自身不能替代这项义务。详见 [对应源码审计清单](packaging/FFMPEG-SOURCE-AUDIT.md)与[安装说明](packaging/INSTALL.md)。

设计依据：[微软的新式文件菜单](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer)和[MSIX 签名说明](https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide)。
