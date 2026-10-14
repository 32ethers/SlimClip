# FFmpeg 对应源码审计（公开 MSIX 分发前必须完成）

当前状态：**未完成；不要在公开仓库上传含本构建 FFmpeg/ffprobe 的 MSIX**。这份清单不构成法律意见或合规证明。SlimClip 自有源码使用 [MIT](../LICENSE)；独立运行的 FFmpeg 二进制按其自身条款处理。

## 被审计的成品

- [Gyan FFmpeg 8.1.2 essentials ZIP](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip)
- ZIP SHA-256：`db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec`，以[上游校验值](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip.sha256)和 CI 核对；包括 `ffmpeg.exe` 与 `ffprobe.exe`。
- [Gyan 构建说明](https://www.gyan.dev/ffmpeg/builds/)称所有构建均为 GPLv3 静态构建，essentials 包含 x265、x264 等外部库。现行 CI 会将发行版许可证和 README 放进 MSIX，但这**不是**完整对应源码。
- [FFmpeg 官方许可说明](https://ffmpeg.org/legal.html)建议分发方核对与二进制精确对应的源码、配置和所链接外部库，而不只是提供 FFmpeg 项目首页。

## 尚缺的可验证材料

1. 与指定 ZIP 精确对应的 FFmpeg 源码版本、所有本地改动/补丁及配置参数。[上游 8.1.2 源码](https://ffmpeg.org/releases/ffmpeg-8.1.2.tar.xz)只是候选，尚未核实能对应 Gyan 成品。
2. `ffmpeg.exe`、`ffprobe.exe` 实际静态链接库的**准确版本**、源码归档及修改/补丁（尤其是 x265、x264 及其依赖）；发行版 README 提供线索，但未经逐项核对。
3. 产生、安装这些二进制所需的构建脚本、配置、必要工具说明以及全部适用许可证文本；需将源码归档与二进制版本、SHA-256 建立可追溯关系。
4. 公开 MSIX 下载位置旁提供**免费、等效、持续可用**的完整对应源码下载方式（见 [GPLv3 第 6 节](https://www.gnu.org/licenses/gpl-3.0.en.html)）；对发布后的可用性进行实际下载验证。短期 Actions artifact 不能代替持久发行源。

## 下一步

优先向 Gyan 构建维护者询问该 ZIP 的完整对应源码、各静态依赖的版本/补丁及构建脚本；其[构建页](https://www.gyan.dev/ffmpeg/builds/)列有公开讨论与联系途径。如果无法取得并核实，则改用自己从固定源码构建、同时归档并发布全部必要对应源码/脚本的 FFmpeg，或重新设计为不分发 FFmpeg 二进制（后者需修改 `MediaTools.cs` 和完整离线 MSIX 的既定方案）。**不要凭一个主项目 Git 标签或一份 LICENSE 将上述条目标为完成。**
