#requires -Version 5.1
<#
.SYNOPSIS
    发布"带 FFmpeg 的完整包"：把 FFmpeg 嵌入 EXE 后再打包。

.DESCRIPTION
    对应设计文档 4.1/4.3 的"用户无需安装 FFmpeg"目标。
    流程：pack-ffmpeg.ps1 放入资源 → publish.ps1 单文件发布 → 可选清理资源目录。

    注意（设计文档 4.4）：FFmpeg 的许可证取决于具体构建。若使用 GPL 构建，
    分发时会带来 GPL 义务。本脚本只负责打包，不判断许可证是否合规。

.EXAMPLE
    pwsh build/publish-full.ps1 -Version 0.2.3 -FfmpegDirectory D:\ffmpeg\bin
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$FfmpegDirectory,

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$DotnetPath,

    # 保留 Resources/ffmpeg 下的二进制（默认用完即删，避免大文件留在工作区）
    [switch]$KeepResources
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$packScript = Join-Path $PSScriptRoot 'pack-ffmpeg.ps1'
$publishScript = Join-Path $PSScriptRoot 'publish.ps1'
$resourceDir = Join-Path $repoRoot "src/AudioCropper/Resources/ffmpeg/$Runtime"

if (-not (Test-Path $FfmpegDirectory)) {
    throw "找不到 FFmpeg 目录：$FfmpegDirectory"
}

Write-Host "`n=== 打包 FFmpeg（$Runtime）===" -ForegroundColor Cyan
& $packScript -SourceDirectory $FfmpegDirectory -Runtime $Runtime

try {
    Write-Host "`n=== 发布完整包 ===" -ForegroundColor Cyan
    $publishArgs = @{
        Version    = $Version
        Runtime    = @($Runtime)
        Suffix     = '-full'
    }
    if (-not [string]::IsNullOrWhiteSpace($DotnetPath)) {
        $publishArgs.DotnetPath = $DotnetPath
    }

    & $publishScript @publishArgs

    $artifact = Join-Path $repoRoot "artifacts/AudioCropper-v$Version-full/AudioCropper-$Runtime-full.exe"
    if (-not (Test-Path $artifact)) {
        throw "未找到完整包产物：$artifact"
    }

    $sizeMb = [math]::Round((Get-Item $artifact).Length / 1MB, 1)
    Write-Host "`n完整包：$artifact（$sizeMb MB）" -ForegroundColor Green
}
finally {
    if (-not $KeepResources -and (Test-Path $resourceDir)) {
        Remove-Item $resourceDir -Recurse -Force
        Write-Host "已清理临时 FFmpeg 资源目录（-KeepResources 可保留）" -ForegroundColor DarkGray
    }
}
