# SlimClip Windows 11 安装、卸载与签名

> **状态：[Windows CI 第六轮](https://github.com/32ethers/SlimClip/actions/runs/38571220801)已成功生成完整无签名 MSIX；尚未签名、真实安装或验证资源管理器。** 本仓库的 `build.ps1` 会生成完整 MSIX，包含自包含的 WPF 应用、x64 `IExplorerCommand` DLL、`ffmpeg.exe` 和 `ffprobe.exe`；它不安装应用，也不会把测试证书加入信任列表。当前本地机器缺少 .NET 8 SDK、C++ 编译器和 Windows SDK，实测构建在 GitHub Windows runner 上完成。

## GitHub Actions 生成测试包

1. 推送 `main` 会自动运行源码构建/测试，**不会公开上传包含 FFmpeg 的 MSIX**。需要测试安装包时，仓库所有者可在 Actions 手动运行 `Windows 11 MSIX test build`（workflow_dispatch）。仓库工作流固定使用上游版本化的 FFmpeg Essentials ZIP URL 和公开 SHA-256；升级 FFmpeg 需审查来源并修改工作流提交。ZIP 必须包含 `ffmpeg.exe`/`ffprobe.exe` 和许可文本，CI 解压前核对哈希；找不到许可文本即失败。
2. Workflow 在 Windows runner 安装 .NET 8 SDK，使用 runner 的 Visual Studio C++/Windows SDK 构建；`build.ps1` 把所有运行文件和视觉资产放进同一个完整 MSIX，而非依赖外部程序目录。每次 Actions run number 作为 MSIX 的构建版本编号；测试证书每次运行都重新生成，测试另一版本前需重新明确核验、信任新公钥，是否可原位升级仍需实机验证（必要时先卸载旧测试版）。正式发行应使用稳定的受信任发布者身份。
3. **仅手动触发**并构建成功时会上传两组**明确区分**的产物：`SlimClip-UNSIGNED-NOT-INSTALLABLE` 仅作构建中间产物，**无法安装**；`SlimClip-TEST-SIGNED-TRUST-PUBLIC-CERT-FIRST` 包含 `SlimClip.test.msix` 和公开的 `SlimClip.test.cer`。若签名失败，workflow 失败，至多留下明确标记的 unsigned 包，不把 unsigned 叫作安装包。

当前固定 URL/校验值参考 [Gyan 官方版本页](https://www.gyan.dev/ffmpeg/builds/)与[该版本 SHA-256](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip.sha256)；版本号和校验值要成对更新。该上游明确称其 FFmpeg 二进制为 **GPLv3 静态构建**。**本仓库公开；手动 workflow_dispatch 上传的测试 MSIX 也会公开可下载。**随包携带原发行版许可证和 URL/SHA-256 **不自动满足** GPLv3 对应源码、构建脚本及相关库的全部分发义务；完成许可与相应源码提供方案核查前，不要公开上传测试包。若仅在个人 Windows 11 测试机验收，可先使用私有构建仓库或本地受控打包；勿将私钥提交到任何仓库。

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
