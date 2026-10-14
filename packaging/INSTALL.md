# SlimClip Windows 11 安装、卸载与签名

> **状态：[Windows CI 第十轮](https://github.com/32ethers/SlimClip/actions/runs/38589585372)已通过完整测试签名 MSIX 构建及 Windows Server 2025 一次性 runner 的安装、核验、卸载与证书清理；Windows 11 菜单和双击安装仍未实机验收。** 本仓库的 `build.ps1` 会生成完整 MSIX，包含自包含的 WPF 应用、x64 `IExplorerCommand` DLL、`ffmpeg.exe` 和 `ffprobe.exe`；它不安装应用，也不会把测试证书加入信任列表。当前本地机器缺少 .NET 8 SDK、C++ 编译器和 Windows SDK，实测构建在 GitHub Windows runner 上完成。

## GitHub Actions 构建与隔离安装测试

1. 推送 `main` 或手动触发都会测试源码、生成完整 MSIX，并用临时自签证书签名；**push 不上传包；仅私有仓库由所有者手动触发且成功后才上传测试 MSIX 和公开证书**。公开仓库即使手动触发也不会上传包含 FFmpeg 的包。工作流固定上游版本化的 FFmpeg Essentials ZIP URL 与 SHA-256，解压前校验哈希，并要求保留许可证/README。升级需审查来源与提交校验值。
2. Windows 2025 CI runner 的独立步骤会**显式**在一次性机器上导入本次测试证书，尝试安装签名包，核对包身份，然后卸载并移除所导入的精确证书。这不是在用户电脑导入证书，也不能代替 Windows 11 真实资源管理器菜单检查；只有该步骤实际通过才可称 CI 安装/卸载通过。正式发行还需稳定的受信任签名身份。
3. 每次 run number 作为包构建编号；证书每次重新生成。在私有仓库 Actions 页面手动运行本工作流，成功后下载名为 `SlimClip-PRIVATE-TEST-SIGNED-TRUST-CERT-FIRST` 的压缩产物（保存 3 天）；里面的 `SlimClip.test.msix` 与 `SlimClip.test.cer` 必须来自**同一次**运行。**发布为 public 前先从所有运行中删除这些测试产物；变更可见性无法收回之前已下载的副本。**私有试装不是面向公众的正式分发，也不能拿未签名包当安装包。

固定 URL/校验值参考 [Gyan 官方版本页](https://www.gyan.dev/ffmpeg/builds/)和[该版本 SHA-256](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip.sha256)；该二进制是 **GPLv3 静态构建**。原始 ZIP/许可证/FFmpeg 主项目源码链接**不自动等于**该成品所有静态依赖、补丁与构建脚本的完整对应源码。[FFmpeg 官方合规说明](https://ffmpeg.org/legal.html)和[GPLv3 第 6 节](https://www.gnu.org/licenses/gpl-3.0.en.html)要求核查随公开二进制提供的相应源码。完成[对应源码审计清单](FFMPEG-SOURCE-AUDIT.md)及等效源码获取方式前，不公开上传此完整 MSIX；独立进程运行 FFmpeg 不会消除对所分发 FFmpeg 自身的义务。

## 安装测试包（仅在知情测试机上）

- **先验证来源和证书指纹**：从同一个可信 CI 运行记录获取 MSIX 和 `.cer`，检查证书主题为 `CN=SlimClip Development`。这个证书是 CI 每次运行临时生成的**自签公钥证书**；信任它意味着该用户可能信任用这个证书签名的其他代码，切勿对陌生文件这样做。仓库和 Actions 产物不包含 PFX/私钥。
- 使用 Windows 证书管理界面**明确选择**将该 `.cer` 导入**本地计算机**的「受信任人」存储（通常需要管理员/UAC 确认）；或由测试者在有管理员权限的 PowerShell 中知情执行 `Import-Certificate -FilePath .\SlimClip.test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`。这是信任该签名者的系统级改动，切勿在陌生机器上随意操作。某些系统只导入当前用户存储会导致 App Installer 仍报证书不可信；不会由应用或构建脚本静默导入证书。
- 双击 `SlimClip.test.msix`，让 Windows App Installer 安装；必要时用 `Add-AppxPackage -Path .\SlimClip.test.msix` 查看诊断错误。实际菜单需要在 Windows 11 资源管理器**直接右键视频文件**验证；菜单初次未显示时可退出后重新登录再试。预估仅单文件可用；五个子项分别是低、中、高、预估大小、设置。
- **签名测试范围**：有测试签名且用户手动信任证书，才能作为安装测试包使用；它不是面向大众可直接安装的正式版。正式版需 Microsoft Store 签名或其他受 Windows 信任的代码签名。生产证书 Subject 改变时必须同时更新 `shell/AppxManifest.xml` 的 `Identity Publisher` 和 `app.manifest` 的 `msix publisher`，两者须逐字一致；不能仅替换证书。

## 卸载与数据

在 Windows「设置 → 应用 → 已安装的应用 → SlimClip → 卸载」卸载完整 MSIX，Windows 会取消包注册和右键菜单扩展。**先在程序托盘选择退出**，并确认没有正在压缩的任务。卸载之后重新打开资源管理器确认菜单消失。必要时可用 `Get-AppxPackage -Name SlimClip.Menu` 核对是否还安装了测试包。用户视频及压缩输出永远不属于安装包，卸载不会自动清除它们；应用设置和残留的临时/备份文件需另外制定/核验清理策略，勿盲目递归删除用户视频目录。

自签测试证书不是包的一部分，卸载应用**不会自动撤销对证书的信任**。确认其他软件不依赖它之后，测试者可在本地计算机证书管理器的「受信任人」中，按先前核对的指纹单独移除测试证书（可能需管理员权限）。切勿因卸载 SlimClip 而批量删除其他受信任证书。

## 仍需实机验证

当前 shell DLL 的 EXE 启动路径、完整 MSIX 的 COM 注册与初次启动、升级/卸载后 Explorer 的菜单刷新、签名测试机安装、FFmpeg 分发许可、视频文件访问权限均需在 CI 或独立 Windows 11 测试机验证；**静态 XML/脚本检查不能替代安装验收**。
