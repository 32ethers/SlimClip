# FFmpeg 对应源码审计（未来项目捆绑二进制之前）

**新版公开 EXE 不含 FFmpeg/ffprobe；用户知情同意后自己的电脑从上游直接下载，或指定已有工具目录。** 这份清单是旧版私有 MSIX 分发测试留下的待办，只有未来由 SlimClip 项目**自行公开捆绑分发**这些第三方二进制时才需先按其条款完成；它不构成法律意见或合规证明。SlimClip 自有代码采用 [MIT](../LICENSE)，第三方 FFmpeg 并非 MIT。

## 被审计的旧二进制

- [Gyan FFmpeg 8.1.2 essentials ZIP](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip)，[上游 SHA-256](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip.sha256) 为 `db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec`，包括 ffmpeg.exe/ffprobe.exe。[Gyan 构建说明](https://www.gyan.dev/ffmpeg/builds/)称其是 GPLv3 静态构建，可能链接 x265、x264 等外部库。
- 历史私有 MSIX 曾在 CI 内生成且可上传到**私有 Actions 产物**；从私有改 public 前必须删除全部这种含 FFmpeg 的旧产物。当前分支已取消其构建/上传；Git 历史保留构建方案并不等于当前 EXE 分发了二进制。

## 未来如果恢复捆绑分发，须先核实

1. 与该 ZIP 精确对应的 FFmpeg 源码版本、所有补丁/本地修改及编译配置。[FFmpeg 8.1.2 主源码](https://ffmpeg.org/releases/ffmpeg-8.1.2.tar.xz)只是一部分候选，不能单独证明对应性。
2. `ffmpeg.exe`、`ffprobe.exe` 实际静态依赖的准确版本、源码、补丁、适用的许可证和必要构建脚本（如 x265/x264），建立与发布二进制/哈希可追溯的记录。
3. 按[FFmpeg 官方许可说明](https://ffmpeg.org/legal.html)与[GPLv3 第 6 节](https://www.gnu.org/licenses/gpl-3.0.en.html)提供符合所选分发方式的完整对应源码获取路径并核验持续可用性；短期 GitHub Actions artifact 不应冒充持久源码提供方式。

若不能取得并核实这些资料，继续使用**不捆绑 FFmpeg 的 EXE + 用户从上游直接获取**，不要仅凭一份 LICENSE 或 FFmpeg 项目主页推断旧静态构建的依赖源码已完备。
