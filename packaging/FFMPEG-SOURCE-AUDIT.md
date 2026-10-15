# FFmpeg 对应源码审计（未来项目捆绑二进制之前）

**当前安装 EXE 与源码仓库均不附带 FFmpeg/ffprobe；首次使用告知上游来源及 GPLv3 许可，用户明确同意后由自己的电脑从固定上游地址直接下载并校验，或指定已有工具目录。** 当前安装包没有分发 GPL FFmpeg 二进制，不产生由该安装包分发该二进制引起的相应再分发义务；这不意味着 FFmpeg 的 GPLv3 许可完全不必关注。以下清单源于旧版私有 MSIX 测试，仅在未来由 SlimClip 项目**自行捆绑分发**这些第三方二进制时作为事先核查项目；它不构成法律意见或合规证明。SlimClip 自有代码采用 [MIT](../LICENSE)，第三方 FFmpeg 并非 MIT。

## 被审计的旧二进制

- [Gyan FFmpeg 8.1.2 essentials ZIP](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip)，[上游 SHA-256](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip.sha256) 为 `db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec`，包括 ffmpeg.exe/ffprobe.exe。[Gyan 构建说明](https://www.gyan.dev/ffmpeg/builds/)称其是 GPLv3 静态构建，可能链接 x265、x264 等外部库。
- 历史私有 MSIX 曾在 CI 内生成并上传到**私有 Actions 产物**；仓库转 public 时该旧产物曾短暂公开，已由[一次性 CI](https://github.com/32ethers/SlimClip/actions/runs/38697950751)删除，删除时公开产物列表为 0；之后的新 FFmpeg-free EXE 产物不在该旧产物中。不能撤回之前可能被下载的副本；当前分支已取消旧 MSIX 构建/上传，Git 历史保留构建方案并不等于当前 EXE 分发了二进制。

## 未来如果恢复捆绑分发，须先核实

1. 与该 ZIP 精确对应的 FFmpeg 源码版本、所有补丁/本地修改及编译配置。[FFmpeg 8.1.2 主源码](https://ffmpeg.org/releases/ffmpeg-8.1.2.tar.xz)只是一部分候选，不能单独证明对应性。
2. `ffmpeg.exe`、`ffprobe.exe` 实际静态依赖的准确版本、源码、补丁、适用的许可证和必要构建脚本（如 x265/x264），建立与发布二进制/哈希可追溯的记录。
3. 按[FFmpeg 官方许可说明](https://ffmpeg.org/legal.html)与[GPLv3 第 6 节](https://www.gnu.org/licenses/gpl-3.0.en.html)提供符合所选分发方式的完整对应源码获取路径并核验持续可用性；短期 GitHub Actions artifact 不应冒充持久源码提供方式。

若不能取得并核实这些资料，继续使用**不捆绑 FFmpeg 的 EXE + 用户从上游直接获取**，不要仅凭一份 LICENSE 或 FFmpeg 项目主页推断旧静态构建的依赖源码已完备。
