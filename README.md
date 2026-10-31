# AudioCropper

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

面向 Windows 的轻量级离线音频分割工具：选择本地音频（支持多选、拖放、整个文件夹递归导入）→ 设置分段时长或片段数量 → 每个源文件的结果保存到其同目录下的同名文件夹。

发布架构的权威说明见 [AudioCropper-release-architecture.md](docs/AudioCropper-release-architecture.md)（原始设计文档）。批量导入、WebM 支持是实现层对设计文档的扩展。

> 当前状态：**可构建、可运行的 0.2.3 骨架**。切割、批量、安装/卸载/升级逻辑已实现并通过端到端测试，
> 但正式发布包尚未打入 FFmpeg 二进制、没有代码签名、没有 ARM64 实机验证。详见[已知缺口](#已知缺口)。

## 功能

- 输入：mp3 / m4a / aac / wav / flac / ogg / opus / wma / aiff / ape / **webm**，以及带音频轨的 mp4 / mkv / mov / ts 等容器
- 输出：MP3、M4A(AAC)、WAV、FLAC、OGG(Vorbis)、Opus、**WebM(Opus)**
- 分段：按时长，或按片段数量均分
- 批量：一次导入多个文件或整个目录树，逐个处理，单个失败不中断；可查看每个文件的状态
- 导入方式：添加文件（多选）、添加文件夹（可含多级子目录）、**把文件或文件夹拖到窗口任意位置**、把文件夹拖到 EXE 图标上或命令行带路径直接导入
- 安全：源文件只读、临时目录提交、覆盖前备份并可回滚

## 技术栈

| 层 | 选择 |
| --- | --- |
| 语言 / 界面 | C# (.NET 10) + WinForms |
| 发布方式 | 自包含单文件 EXE，按架构分别发布 |
| 音频处理 | FFmpeg（内嵌进 EXE，运行时释放到缓存目录） |
| 权限 | 全程当前用户，不需要管理员 |

## 目录结构

```text
audiocropper/
├── AudioCropper.sln
├── src/AudioCropper/
│   ├── Program.cs                 启动入口：解析运行模式
│   ├── app.manifest               asInvoker、高 DPI、长路径
│   ├── Bootstrap/                 运行模式判断、架构探测、PE 校验
│   ├── UI/                        MainForm（任务列表）、InstallForm（安装向导）
│   ├── Audio/                     媒体探测、FFmpeg 定位与执行、分段、批量、输出提交
│   ├── Install/                   安装、升级、卸载、快捷方式与注册表
│   └── Safety/                    路径约定、临时目录、文件提交回滚、日志
├── build/
│   ├── publish.ps1                按架构发布单文件 EXE + SHA256SUMS.txt
│   ├── verify-release.ps1         校验哈希、PE 架构、可执行性
│   └── pack-ffmpeg.ps1            把架构匹配的 FFmpeg 放入工程并生成 manifest
└── docs/                          设计文档
```

关键类：`AudioSplitService`（单文件切割）、`BatchSplitService`（批量调度）、
`AudioFileScanner`（多级目录扫描）、`FfmpegLocator`（内嵌组件定位与释放）、
`InstallerService` / `UninstallService` / `UpgradeService`。

模块划分直接对应设计文档第 3 章，界面不参与 FFmpeg 参数拼接和文件提交。

## 运行模式

同一个 EXE 承担全部入口：

```text
AudioCropper.exe              直接运行（便携，无需安装）
AudioCropper.exe --install    安装向导
AudioCropper.exe --uninstall  卸载（由“应用和功能”调用）
AudioCropper.exe --version    版本信息
```

## 构建

前置条件：.NET 10 SDK（或更新版本）。

> 本机 SDK 装在用户目录 `%LOCALAPPDATA%\Microsoft\dotnet`（未改系统 PATH）。
> `build/publish.ps1` 会自动回退到该路径；手动执行 `dotnet build` 时请用完整路径，
> 或把该目录加入当前会话的 PATH：
>
> ```powershell
> $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
> ```

```powershell
pwsh build/publish.ps1 -Version 0.2.3
# 产物：artifacts/AudioCropper-v0.2.3/
#   AudioCropper-win-x64.exe
#   AudioCropper-win-arm64.exe
#   SHA256SUMS.txt
```

只发布单个架构：

```powershell
pwsh build/publish.ps1 -Version 0.2.3 -Runtime win-x64
```

发布**带 FFmpeg 的完整包**（把 FFmpeg 嵌入 EXE，产物带 `-full` 后缀）：

```powershell
pwsh build/publish-full.ps1 -Version 0.2.3 `
    -FfmpegDirectory D:\ffmpeg\bin `
    -Runtime win-x64
# 产物：artifacts/AudioCropper-v0.2.3-full/AudioCropper-win-x64-full.exe
```

发布后校验（哈希、PE 架构、可执行性）：

```powershell
pwsh build/verify-release.ps1 -Version 0.2.3
```

日常开发调试：

```powershell
dotnet build src/AudioCropper/AudioCropper.csproj
dotnet run --project src/AudioCropper/AudioCropper.csproj
```

开发期如果没有内嵌 FFmpeg，可以用环境变量指到一个现成的 FFmpeg 目录（仅用于调试，发布版不依赖它）：

```powershell
$env:AUDIOCROPPER_FFMPEG_DIR = 'D:\ffmpeg\bin'
```

## 试用版本

`artifacts/` 下有普通版和完整版，**完整版才能直接切割**：

| 文件 | 大小 | 说明 |
| --- | --- | --- |
| `AudioCropper-v0.2.3-full/AudioCropper-win-x64-full.exe` | 172 MB | **完整包**：已内嵌 FFmpeg，双击即可用，无需任何额外文件 |
| `AudioCropper-v0.2.3/AudioCropper-win-x64.exe` | 46.9 MB | 普通发布包，**不含 FFmpeg**；开始处理前会提示缺少组件 |
| `AudioCropper-v0.2.3/AudioCropper-win-arm64.exe` | 45.7 MB | 同上，ARM64 架构，尚未在真机验证 |

完整包目前只构建了 x64；内嵌的 FFmpeg 来自本机 GPLv3 构建，正式分发前需完成许可证合规。

## 打包 FFmpeg

发布版要求“用户无需安装 FFmpeg”。做法是把对应架构的 `ffmpeg.exe` / `ffprobe.exe`
作为嵌入资源打进主 EXE，首次运行时校验 SHA-256 后释放到
`%LOCALAPPDATA%\AudioCropper\cache\native\<架构>-<版本>\`。

```powershell
pwsh build/pack-ffmpeg.ps1 -SourceDirectory D:\ffmpeg-7.1-win64 -Runtime win-x64
pwsh build/pack-ffmpeg.ps1 -SourceDirectory D:\ffmpeg-7.1-winarm64 -Runtime win-arm64
```

脚本会校验 PE 架构（防止把 x64 误标成 ARM64），并生成每行 `文件名 sha256` 的 `manifest.txt`。
工程文件只在 `Resources/ffmpeg/<rid>/` 存在时才嵌入资源，因此没有二进制时也能编译。

发布前还需要针对实际构建确认能力清单（`ffmpeg -encoders` / `-decoders` / `-formats`），
并裁剪界面中的输出格式列表 —— 见设计文档 4.2 与验收项 R-08。

## 关键实现约定

- **文件安全**：切割在输出目录旁的唯一临时目录中进行，校验数量、大小与**可解码性**后逐文件提交；
  覆盖同名文件前先备份，失败回滚；只触碰本次任务登记的文件，绝不递归清空目录。
- **尾段处理**：时长分段会产生极短尾段，MP3 等编码器在这种长度下可能写出"非空但无有效帧"的文件。
  每个片段提交前都会实际探测，只有**无法解码的末段**会被丢弃并记录日志，其余情况一律报错。
- **源文件只读**：程序从不修改、移动或删除源音频。
- **参数安全**：FFmpeg 通过 `ProcessStartInfo.ArgumentList` 传参，不拼接命令行字符串。
- **不依赖 PATH**：只使用内嵌/同目录的 FFmpeg，避免用户机器上恰好存在的版本改变行为。
- **批量隔离**：每个文件独立调用切割服务、独立输出目录；单文件失败不影响其余文件，
  取消只作用于当前文件与未开始的队列。
- **多级目录**：递归扫描时跳过重解析点（目录联接/符号链接），避免循环；单次导入上限 5000 个文件。
- **卸载边界**：只删除安装目录内的程序文件、自建快捷方式和 HKCU 卸载项；
  用户设置按用户选择保留；无法确认归属的文件跳过并列出。
- **升级回滚**：替换前把旧 EXE 改名为 `.old`，替换后用 `--version` 试启动，失败自动回滚。

## 验证状态

以下项目已在本机实际执行并通过（2026-10-31，Windows 10 x64，.NET SDK 10.0.403，v0.2.3）：

| 检查 | 结果 |
| --- | --- |
| 编译（`dotnet build`） | 0 警告 / 0 错误 |
| 双架构单文件发布 | `AudioCropper-win-x64.exe` 46.9 MB、`AudioCropper-win-arm64.exe` 45.7 MB |
| PE 架构真实性 | x64 产物为 0x8664，ARM64 产物为 0xAA64（非改名） |
| 发布哈希 | `SHA256SUMS.txt` 与实际文件逐项一致（`build/verify-release.ps1`） |
| 直接运行 | `AudioCropper.exe --version` 正常输出并以 0 退出 |
| 按时长分段 | 10 s 样例 / 3 s → 4 个片段，命名 `0001.mp3`…`0004.mp3` |
| 按数量分段 | 10 s 样例 / 3 段 → 恰好 3 个片段，各 3.33 s |
| 目标目录冲突 | 已存在时返回待确认状态；编号目录生成 `lesson (1)`；覆盖只替换本次文件，其他文件保留 |
| 源文件保护 | 处理前后源文件 SHA-256 不变 |
| 临时目录 | 任务结束后无残留 |
| 内嵌 FFmpeg 释放 | 从程序内嵌资源按 manifest 校验 SHA-256 后释放到 `cache\native\<架构>-<版本>`，再运行切割成功；二次运行复用缓存 |
| WebM 输出 | 时长/数量两种模式均生成 `codec=opus, format=webm` 的片段，可被 ffprobe 正常读取 |
| WebM 输入 | 带 VP9 视频轨 + Opus 音频轨的 6 s WebM → MP3 得 3 段、WebM 得 2 段，输出只含音频流 |
| 尾段校验 | 6.008 s / 2 s 产生的 8 ms 尾段被识别为不可解码并丢弃，不再产出损坏文件 |
| 多级目录扫描 | 3 层嵌套目录 + 损坏文件：递归找到 4 个文件，非递归 0 个，重复目录不重复添加 |
| 批量处理 | 4 个文件（含 1 个损坏）：成功 3、失败 1，失败不中断；SkipExisting 跳过 3、失败 1；OverwriteExisting 成功 3 |
| 批量取消 | 已取消的令牌立即返回 `Canceled=true`，不再处理新文件 |
| 拖放覆盖范围 | 主窗体构造后统计：27 个控件全部 `AllowDrop=true`（不只窗体和列表） |
| 命令行导入 | `AudioCropper.exe <文件夹>` 启动后日志显示"已添加 2 个文件"（含 1 个一级子目录） |
| 缺少 FFmpeg 的提示 | 错误信息列出三处查找位置、当前架构与 `pack-ffmpeg.ps1` / 环境变量两条解决路径 |
| 启动前预检 | 开始处理前先解析一次 FFmpeg；缺失时只弹一次说明并终止，不再逐文件刷错误 |
| 完整包 | `publish-full.ps1` 产出的 172 MB 单文件 EXE 通过哈希、架构与 `--version` 校验 |

尚未验证：GUI 内的人工点击流程（对话框、状态着色、真实拖放）、ARM64 实机、安装/卸载/升级的注册表与快捷方式实际效果。

## 已知缺口

| 项 | 状态 |
| --- | --- |
| FFmpeg 二进制 | 未入库，需执行 `pack-ffmpeg.ps1` 才能实际切割 |
| 格式能力清单 | 界面当前列出乐观集合，未按实测构建裁剪（R-08） |
| 代码签名 / SHA-256 发布 | 脚本生成哈希；签名需证书，未接入 |
| ARM64 实机验证 | 未验证（R-02） |
| 最低 Windows 版本 | 未定（设计文档 14.1） |
| 单元测试 | 尚无测试工程（端到端验证用临时 harness 完成，未入库） |
| 安装目录默认值 | 目前为 `%LOCALAPPDATA%\Programs\AudioCropper`，仍属待定项 |

## 许可证

本项目源码采用 [MIT License](LICENSE)。

**FFmpeg 是独立组件，不适用 MIT。** 它的许可证义务取决于具体构建版本与编译选项。
对外分发携带 FFmpeg 的产物前，必须固定版本与构建来源、保存完整构建配置、
确认 LGPL/GPL 适用条款并随包提供所需声明与源码获取方式（设计文档 4.4）。
仓库本身不包含 FFmpeg 二进制（见 `.gitignore`）。

---

[![](https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white)](https://github.com/deepseek-ai/deepseek-harness)
