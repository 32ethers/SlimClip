# SlimClip EXE 安装、卸载与第三方 FFmpeg

> **状态：[Windows CI 第十四轮](https://github.com/32ethers/SlimClip/actions/runs/38718474279)已通过 14 项引擎/双语/PATH/ETA 测试、首次向上游下载、哈希验证、工具提取与启动、双语 EXE 构建、COM 自测、按两种语言参数每用户安装／经典注册核对／卸载及 FFmpeg-free 安装器上传；真实 Windows 11 Explorer 菜单和首次授权 UI 尚待实机验收。** `packaging/build-exe.ps1` 构建自包含 .NET 8 程序、原生经典菜单 DLL 和 Inno Setup 安装器；不包含 FFmpeg、不使用 MSIX，也不会生成、导入或信任测试证书。

## 安装（预览版）

1. 发布后可从 [Releases](https://github.com/32ethers/SlimClip/releases) 下载对应版本的 `SlimClip-Setup.exe` 与 `SHA256SUMS.txt`，在两个文件所在目录运行 PowerShell `Get-FileHash .\SlimClip-Setup.exe -Algorithm SHA256`，核对输出与 `SHA256SUMS.txt` 中的 64 位哈希相同；或者从[Windows EXE 工作流](https://github.com/32ethers/SlimClip/actions/workflows/windows-exe.yml)选择最新的绿色成功运行，取得名为 `SlimClip-EXE-Installer-NO-FFMPEG` 的产物（内含 `SlimClip-Setup.exe`）；Actions 产物仅保留 7 天；若全部过期，仓库维护者可用工作流页的「Run workflow」重新构建；务必核对来源及构建编号。未签名 EXE 可能触发 SmartScreen 警告；不要关闭系统保护或执行不明来源 EXE。用户不必安装 MSIX 测试证书。
2. 双击安装器，安装向导提供 English / 简体中文两种语言；默认在当前用户 `%LOCALAPPDATA%\Programs\SlimClip` 安装，每用户注册 COM 经典子菜单和 Windows 卸载项，不改全局证书存储。Windows 11 对视频文件右键→**显示更多选项**→「压缩视频」；Windows 10 使用经典右键菜单。观察五个子项、单文件预估、多选排队；资源管理器可能需要重新登录后刷新菜单。CI 只检查 COM 注册/逻辑，不代替这一步实机观察。
3. 工具优先级：设置中手动指定的绝对目录 > 用户曾明确授权下载的 `%LOCALAPPDATA%\SlimClip\tools` > `PATH` 中首个同时含 `ffmpeg.exe` + `ffprobe.exe` 的绝对目录。只有都没有时，首次压缩/预估才会显示第三方 Gyan FFmpeg 的来源及 GPLv3 许可，须用户主动同意才从固定上游 HTTPS 地址下载并校验 SHA-256。请自行确认 PATH 中已有工具的来源与许可；如刚修改了系统或用户 PATH，请完全退出 SlimClip，必要时重启 Explorer 或重新登录，让新进程继承更新后的环境。界面语言在「设置」中选择跟随系统 / English / 简体中文，改变后从托盘完全退出并重启；右键菜单按 Windows 用户 UI 语言显示，可能需重启 Explorer 更新。EXE 安装包和源码仓库均不附带这两个程序；直接从上游下载失败、离线或拒绝授权时不会改动源片。
4. 若曾安装旧私有 MSIX 试用版，先从「设置 → 应用」卸载旧版 `SlimClip.Menu`；先前手动信任的自签证书不会由新 EXE 自动移除。确认其指纹、其他应用并不依赖后，可在证书管理器按**精确指纹**单独删除；不要批量清理证书。

## 卸载与保留的数据

在 SlimClip 托盘选择「退出」并等待正在执行的任务清理后，到 Windows「设置 → 应用 → 已安装的应用 → SlimClip → 卸载」。安装器应只删除自己的 EXE/DLL/快捷方式及 HKCU 经典右键菜单注册项。**不会删除用户视频、压缩输出、残留原片备份、设置或经用户授权下载的 FFmpeg 工具**；后两者位于 `%LOCALAPPDATA%\SlimClip`，如确实不再使用，再由用户逐项核对后自行删除。重新打开 Explorer 验证菜单消失；首次安装/升级及卸载均需真实机器检查。

## GitHub Actions 与重新公开

CI 的 FFmpeg 仅作为隔离 runner 的测试输入：从固定的 [Gyan ZIP](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip)下载、验证 [SHA-256](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip.sha256)，运行音轨/引擎测试；随后**只上传不含 FFmpeg 的 EXE 安装器**。构建脚本拒绝将名为 `ffmpeg.exe`、`ffprobe.exe` 的文件放进安装载荷，隔离安装测试再次检查。SlimClip 自有代码采用 [MIT](../LICENSE)，程序提供下载的 Gyan FFmpeg 构建是独立的 GPLv3 软件（用户自行选择的其他构建以其实际许可为准），仍应告知来源及许可，不能将第三方工具视为 SlimClip 的 MIT 代码。当前安装包没有分发 FFmpeg 二进制，因此不产生由该安装包分发 GPL 二进制引起的相应再分发义务；若未来决定自行捆绑上游二进制，先完成[对应源码与许可审计](FFMPEG-SOURCE-AUDIT.md)。Inno Setup 安装引擎来自[其官方项目](https://jrsoftware.org/)，其[许可](https://jrsoftware.org/files/is/license.txt)允许用于软件分发。简体中文安装向导使用 [Zhenghan Yang (Kira) 的翻译](https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation)，按其 [MIT 许可证](translations/LICENSE.txt)原样保留译者通知，安装后可在 `INNO-CHINESE-TRANSLATION-LICENSE.txt` 查看；CI runner 未预装该语言文件，因此源码仓库附带文本翻译，不附带 FFmpeg 二进制。仓库已重新变为 public，CI 状态与步骤结果现在可以通过公开 Actions API 查看；私有期设计的 `ci-diagnostics` 分支未产生，目前不需要上传额外报告。若将来再次转 private，诊断任务只记录步骤状态与限定错误编号，不能把任意构建日志或 FFmpeg 二进制写进 Git 历史。

仓库变为 public 后发现一个未过期的旧测试产物 `SlimClip-PRIVATE-TEST-SIGNED-TRUST-CERT-FIRST`（ID `11945350355`），曾短暂公开；通过[只针对该产物的清理 CI](https://github.com/32ethers/SlimClip/actions/runs/38697950751)核对原运行、名称、大小后删除。删除后的公开 API 查询该 ID 返回 404、当时的产物列表为 0；这不代表之后上传的 FFmpeg-free EXE 产物列表仍为空。**无法证明无人在公开期间下载，也无法撤回外部副本。**一次性清理工作流已不在当前主分支；新版公开 EXE 不捆绑 FFmpeg，不能用旧 MSIX 的测试成绩冒充 EXE 实机验收。
