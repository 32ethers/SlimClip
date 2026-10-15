# SlimClip EXE 安装、卸载与第三方 FFmpeg

> **状态：旧版 MSIX 在隔离 CI 安装/卸载成功，不等于新版 EXE 通过。** 新版 `packaging/build-exe.ps1` 在 Windows x64 构建自包含 .NET 8 程序、原生经典菜单 DLL 和 Inno Setup 安装器；不包含 FFmpeg、不使用 MSIX，也不会生成、导入或信任测试证书。当前开发机无 .NET 8 SDK/MSVC/Inno Setup，需使用 Windows CI 验证 EXE 实际构建及安装。

## 安装（开发测试与未来发布）

1. 从**可信的项目 Actions 运行**取得名为 `SlimClip-EXE-Installer-NO-FFMPEG` 的产物（内含 `SlimClip-Setup.exe`）；只有新版构建、测试和隔离安装均通过后才以它测试。未签名 EXE 可能触发 SmartScreen 警告；不要关闭系统保护或执行不明来源 EXE。用户不必安装 MSIX 测试证书。
2. 双击安装器，默认在当前用户 `%LOCALAPPDATA%\Programs\SlimClip` 安装，每用户注册 COM 经典子菜单和 Windows 卸载项，不改全局证书存储。Windows 11 对视频文件右键→**显示更多选项**→「压缩视频」；Windows 10 使用经典右键菜单。观察五个子项、单文件预估、多选排队；资源管理器可能需要重新登录后刷新菜单。CI 只检查 COM 注册/逻辑，不代替这一步实机观察。
3. 首次压缩/预估时，如果未指定工具，SlimClip 会先显示 Gyan FFmpeg GPLv3 及下载来源，必须用户主动同意才从固定 HTTPS 地址下载并校验 SHA-256。用户也可在「设置」中指定已存在的 `ffmpeg.exe` + `ffprobe.exe` 目录。EXE 安装包和仓库不分发这两个程序；直接从上游下载失败、离线或拒绝授权时不会改动源片。
4. 若曾安装旧私有 MSIX 试用版，先从「设置 → 应用」卸载旧版 `SlimClip.Menu`；先前手动信任的自签证书不会由新 EXE 自动移除。确认其指纹、其他应用并不依赖后，可在证书管理器按**精确指纹**单独删除；不要批量清理证书。

## 卸载与保留的数据

在 SlimClip 托盘选择「退出」并等待正在执行的任务清理后，到 Windows「设置 → 应用 → 已安装的应用 → SlimClip → 卸载」。安装器应只删除自己的 EXE/DLL/快捷方式及 HKCU 经典右键菜单注册项。**不会删除用户视频、压缩输出、残留原片备份、设置或经用户授权下载的 FFmpeg 工具**；后两者位于 `%LOCALAPPDATA%\SlimClip`，如确实不再使用，再由用户逐项核对后自行删除。重新打开 Explorer 验证菜单消失；首次安装/升级及卸载均需真实机器检查。

## GitHub Actions 与重新公开

CI 的 FFmpeg 仅作为隔离 runner 的测试输入：从固定的 [Gyan ZIP](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip)下载、验证 [SHA-256](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip.sha256)，运行音轨/引擎测试；随后**只上传不含 FFmpeg 的 EXE 安装器**。构建脚本拒绝将名为 `ffmpeg.exe`、`ffprobe.exe` 的文件放进安装载荷，隔离安装测试再次检查。SlimClip 自有代码采用 [MIT](../LICENSE)，上游 FFmpeg GPLv3 独立遵守其条款；若未来决定捆绑上游二进制，先完成[对应源码审计](FFMPEG-SOURCE-AUDIT.md)。Inno Setup 安装引擎来自[其官方项目](https://jrsoftware.org/)，其[许可](https://jrsoftware.org/files/is/license.txt)允许用于软件分发。

**仓库所有者把 private 改回 public 前**，必须先在 Actions 页面删除此前私有运行中名为 `SlimClip-PRIVATE-TEST-SIGNED-TRUST-CERT-FIRST`、包含 GPLv3 静态 FFmpeg 的旧 MSIX 产物（包括所有历史运行）；单改仓库可见性不会自动删除旧产物或撤回外部已下载的副本。旧 MSIX 构建脚本/工作流已经从当前分支移除，但历史下载仍须处理。公开新版 EXE 是另一步，不能用旧版 MSIX 测试成绩冒充新版验收。
