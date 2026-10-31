#requires -Version 5.1
<#
.SYNOPSIS
    把指定架构的 FFmpeg 二进制放入工程资源目录，并生成 SHA-256 manifest。

.DESCRIPTION
    设计文档 4.3/4.4：FFmpeg 必须按架构分别打包，且固定版本与构建来源。
    本脚本只负责把已经取得的二进制放进工程：
        src/AudioCropper/Resources/ffmpeg/<rid>/ffmpeg.exe
        src/AudioCropper/Resources/ffmpeg/<rid>/ffprobe.exe
        src/AudioCropper/Resources/ffmpeg/<rid>/manifest.txt
    构建时这些文件会被嵌入主 EXE（见 AudioCropper.csproj）。

    不负责下载。请从可信来源取得对应架构的构建，并记录版本、配置与许可证。

.EXAMPLE
    pwsh build/pack-ffmpeg.ps1 -SourceDirectory D:\ffmpeg-7.1-win64 -Runtime win-x64
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $repoRoot "src/AudioCropper/Resources/ffmpeg/$Runtime"

if (-not (Test-Path $SourceDirectory)) {
    throw "源目录不存在：$SourceDirectory"
}

$expected = @('ffmpeg.exe', 'ffprobe.exe')
foreach ($name in $expected) {
    if (-not (Test-Path (Join-Path $SourceDirectory $name))) {
        throw "源目录缺少 $name。单文件发布需要 ffmpeg.exe；ffprobe.exe 可缺失，但建议一并打包。"
    }
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$manifestLines = @()
foreach ($name in $expected) {
    $source = Join-Path $SourceDirectory $name
    if (-not (Test-Path $source)) { continue }

    # 架构校验：确认二进制的 PE 机器类型与目标架构一致。
    $bytes = [System.IO.File]::ReadAllBytes($source)
    if ($bytes.Length -lt 0x40) { throw "$name 不是有效的 PE 文件。" }
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    $machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
    $actualArch = switch ($machine) {
        0x8664 { 'win-x64' }
        0xAA64 { 'win-arm64' }
        0x014C { 'win-x86' }
        default { "unknown(0x{0:X4})" -f $machine }
    }
    if ($actualArch -ne $Runtime) {
        throw "$name 的架构为 $actualArch，与目标 $Runtime 不一致。"
    }

    $target = Join-Path $targetDir $name
    Copy-Item $source $target -Force
    $hash = (Get-FileHash $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestLines += "$name $hash"
    Write-Host "已复制 $name（$([math]::Round((Get-Item $target).Length / 1MB, 2)) MB）" -ForegroundColor Green
}

$manifestPath = Join-Path $targetDir 'manifest.txt'
$manifestLines | Set-Content -Path $manifestPath -Encoding ascii
Write-Host "已写入 manifest：$manifestPath" -ForegroundColor Green

Write-Host "`n下一步：pwsh build/publish.ps1 -Version <版本> -Runtime $Runtime"
