#requires -Version 5.1
<#
.SYNOPSIS
    按架构分别发布 AudioCropper 单文件 EXE，并生成 SHA256SUMS.txt。

.DESCRIPTION
    对应设计文档第 12 章构建流程的最后几步：单 EXE 打包 → 生成哈希。
    代码签名与安全扫描需要在 CI 中另行处理，本脚本不涉及。

.EXAMPLE
    pwsh build/publish.ps1 -Version 0.1.0
#>
[CmdletBinding()]
param(
    [string]$Version = '0.1.0',
    [string[]]$Runtime = @('win-x64', 'win-arm64'),
    [string]$DotnetPath,
    [switch]$SkipHash,
    # 产物目录与文件名后缀，例如 '-full' 表示带 FFmpeg 的完整包
    [string]$Suffix = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/AudioCropper/AudioCropper.csproj'
$artifactRoot = Join-Path $repoRoot "artifacts/AudioCropper-v$Version$Suffix"

if (-not (Test-Path $project)) {
    throw "找不到项目文件：$project"
}

# 优先使用显式指定的 dotnet；否则用 PATH 中的 dotnet。
if ([string]::IsNullOrWhiteSpace($DotnetPath)) {
    $DotnetPath = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if ([string]::IsNullOrWhiteSpace($DotnetPath)) {
        $candidate = Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe'
        if (Test-Path $candidate) { $DotnetPath = $candidate }
    }
}

if ([string]::IsNullOrWhiteSpace($DotnetPath) -or -not (Test-Path $DotnetPath)) {
    throw '未找到 dotnet。请安装 .NET SDK，或用 -DotnetPath 指定路径。'
}

Write-Host "dotnet: $DotnetPath"
& $DotnetPath --version

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

foreach ($rid in $Runtime) {
    $publishDir = Join-Path $repoRoot "artifacts/publish/$rid"
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

    Write-Host "`n=== 发布 $rid ===" -ForegroundColor Cyan
    & $DotnetPath publish $project `
        -c Release `
        -r $rid `
        -o $publishDir `
        -p:Version=$Version `
        -p:InformationalVersion=$Version `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "发布失败：$rid"
    }

    $produced = Join-Path $publishDir 'AudioCropper.exe'
    if (-not (Test-Path $produced)) {
        throw "未找到发布产物：$produced"
    }

    $target = Join-Path $artifactRoot "AudioCropper-$rid$Suffix.exe"
    Copy-Item $produced $target -Force
    $sizeMb = [math]::Round((Get-Item $target).Length / 1MB, 2)
    Write-Host "  生成 $target（$sizeMb MB）" -ForegroundColor Green
}

if (-not $SkipHash) {
    $hashFile = Join-Path $artifactRoot 'SHA256SUMS.txt'
    $lines = Get-ChildItem $artifactRoot -Filter '*.exe' | Sort-Object Name | ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
    $lines | Set-Content -Path $hashFile -Encoding ascii
    Write-Host "`n哈希文件：$hashFile" -ForegroundColor Green
    Get-Content $hashFile | ForEach-Object { Write-Host "  $_" }
}

Write-Host "`n发布产物目录：$artifactRoot" -ForegroundColor Cyan
