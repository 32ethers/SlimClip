# SlimClip

![SlimClip：胶片、播放按钮与向内的压缩箭头](assets/SlimClip.png)

**在 Windows 资源管理器里，右键就能压缩视频。**

手机、相机或录屏生成的视频太大，上传、分享和存放都不方便；为了压缩它们，又不想研究 FFmpeg 命令行，或冒险覆盖原片。SlimClip 把常用操作做成三个画质档位：选视频、选档位，等待生成 H.265/MP4 文件。它可以先预估大小、批量排队，并在处理时显示进度和大致剩余时间。

适用 **Windows 10/11 x64**。Windows 11 的入口在右键菜单的 **「显示更多选项」** 里，而不是第一层新式菜单。程序和安装包目前是**未签名预览版**；Windows 11 实机验收已获用户反馈通过。

## 下载和安装

1. 从 [v1.1.0-preview.1 发布页](https://github.com/32ethers/SlimClip/releases/tag/v1.1.0-preview.1)下载 `SlimClip-Setup.exe`；可同时下载 `SHA256SUMS.txt` [核对文件哈希](packaging/INSTALL.md)。安装器未签名，Windows 可能提示 SmartScreen；请先确认下载地址，不要为了安装而关闭系统防护。
2. 双击安装器，选择简体中文或 English，按向导安装。它只为当前用户安装，不需要安装 .NET SDK、MSIX 或测试证书。
3. **不需要事先下载 FFmpeg 才能安装。**如果电脑已有同一目录中的 `ffmpeg.exe` 和 `ffprobe.exe`，SlimClip 可以使用它们；否则在第一次压缩或预估时，它会先征求同意，再由你的电脑从上游下载并校验。安装器和源码仓库都**不包含 FFmpeg 二进制**。拒绝下载时也不会改动原视频。

## 怎么用

1. 在资源管理器里选中一个视频，右键选择 **「压缩视频」**；Windows 11 需先点 **「显示更多选项」**。想先看看结果大约有多大，选 **「预估大小」**；这个功能一次只针对一个视频。
2. 选择压缩档位：

   | 档位 | 适合什么时候选 | 默认最高高度 |
   | --- | --- | --- |
   | **低** | 更在意文件大小，可以接受画质下降 | 720p |
   | **中** | 想在体积和画质间折中；不确定时先试这个 | 1080p |
   | **高** | 更在意画质，文件不一定会明显变小 | 保持原分辨率 |

   程序不会把低分辨率视频放大。预估值和剩余时间只是参考，具体结果取决于源视频。输出固定为 **H.265 视频、MP4 文件**；播放设备也需要支持 H.265。
3. 首次处理若提示缺少 FFmpeg，可以选择同意从上游下载，或打开 **「设置」** 指定已有工具目录。电脑的 `PATH` 中若已有同一绝对目录下成对的 `ffmpeg.exe`、`ffprobe.exe`，程序也会自动找到它们；刚修改 `PATH` 后请完全退出程序并重启，必要时重新登录 Windows。
4. 在窗口里查看每项进度、状态和大致剩余时间。选中多个视频后选择压缩档位，它们会依次排队；关闭窗口只会收起到系统托盘，**托盘菜单中的「退出」**才会结束程序。需要中止当前任务，可点窗口中的 **「取消当前任务」**。

默认在原视频所在文件夹**另存新文件**，保留原片；如果结果不比原片小，默认不保存这个结果。你可以在 **「设置」** 调整三个档位、改为成功后替换原片、指定 FFmpeg 文件夹，或切换 English / 简体中文。更改界面语言后，需要从托盘完全退出并重新启动；右键菜单的语言则跟随 Windows 的显示语言。

## 使用前要知道

- 音频会随视频保留：AAC 音轨直接复制，其他音轨转换为 AAC；尽量保留多音轨和语言标签。**字幕等额外轨道目前不能保留**：另存前会提示，替换原片时则拒绝这样做。重要视频请先用副本试用并检查播放效果。
- 预览版通过了 [Windows CI：14 项测试、安装与卸载检查](https://github.com/32ethers/SlimClip/actions/runs/38724166612)；用户另反馈 Windows 11 实机验收通过。自动测试与实机体验是两类不同的验证，复测步骤见[实机验收清单](tests/windows11-acceptance.md)。
- 安装包不附带 FFmpeg。SlimClip 自有代码采用 [MIT](LICENSE)；可选择下载的第三方 Gyan FFmpeg 构建采用 GPLv3，自己提供的版本则按其实际许可使用。详见[安装、卸载与工具说明](packaging/INSTALL.md)。

## English

**SlimClip compresses videos from the Windows 10/11 Explorer context menu** without requiring FFmpeg commands. It is an **unsigned preview**. Download `SlimClip-Setup.exe` from the [release](https://github.com/32ethers/SlimClip/releases/tag/v1.1.0-preview.1), install it for your user account, then right-click a video. On Windows 11 choose **Show more options → Compress Video**; on Windows 10 use the classic context menu. Choose **Low** (smaller), **Medium** (balanced), **High** (quality), or **Estimate size** for a single file. The default saves a new H.265/MP4 file beside the original; selecting several videos queues them.

The installer contains **no FFmpeg**. SlimClip can use an existing folder or a complete `ffmpeg.exe`/`ffprobe.exe` pair on `PATH`; otherwise it asks before downloading a pinned build directly from upstream. Use **Settings → Language** to choose English or Simplified Chinese, then fully quit and restart. See the [installation guide](packaging/INSTALL.md) and [manual acceptance checklist](tests/windows11-acceptance.md). The user reports that Windows 11 hands-on acceptance passed; the installer remains unsigned.
