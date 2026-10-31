#requires -Version 5.1
<#
.SYNOPSIS
    校验发布产物：哈希、PE 架构、可执行性。

.DESCRIPTION
    对应设计文档第 13 章的验收项 R-01/R-02/R-03/R-14 中可自动检查的部分：
      - SHA256SUMS.txt 与实际文件一致；
      - 每个 EXE 的 PE 机器类型与文件名声明的架构一致（防止改名伪装）；
      - 当前架构对应的 EXE 能以 --version 正常启动。

.EXAMPLE
    pwsh build/verify-release.ps1 -Version 0.1.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ArtifactDirectory
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $ArtifactDirectory = Join-Path $repoRoot "artifacts/AudioCropper-v$Version"
}

if (-not (Test-Path $ArtifactDirectory)) {
    throw "找不到产物目录：$ArtifactDirectory"
}

$failures = [System.Collections.Generic.List[string]]::new()

function Get-PeArchitecture([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 0x40) { return 'invalid' }
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    if ($peOffset -le 0 -or $peOffset + 6 -gt $bytes.Length) { return 'invalid' }
    if ([BitConverter]::ToUInt32($bytes, $peOffset) -ne 0x00004550) { return 'invalid' }
    switch ([BitConverter]::ToUInt16($bytes, $peOffset + 4)) {
        0x8664 { 'win-x64' }
        0xAA64 { 'win-arm64' }
        0x014C { 'win-x86' }
        default { 'unknown' }
    }
}

# 1. 哈希校验
$hashFile = Join-Path $ArtifactDirectory 'SHA256SUMS.txt'
if (-not (Test-Path $hashFile)) {
    $failures.Add('缺少 SHA256SUMS.txt')
}
else {
    $checked = 0
    foreach ($line in Get-Content $hashFile) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split '\s+', 2
        if ($parts.Count -ne 2) { $failures.Add("哈希行格式无效：$line"); continue }

        $expected = $parts[0].Trim().ToLowerInvariant()
        $name = $parts[1].Trim()
        $path = Join-Path $ArtifactDirectory $name
        if (-not (Test-Path $path)) { $failures.Add("缺少文件：$name"); continue }

        $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected) {
            $failures.Add("哈希不匹配：$name")
        }
        Write-Host "  哈希 OK  $name"
        $checked++
    }
    if ($checked -eq 0) { $failures.Add('SHA256SUMS.txt 为空') }
}

# 2. 架构校验：文件名里的 win-<rid> 必须与 PE 机器类型一致
Get-ChildItem $ArtifactDirectory -Filter '*.exe' | ForEach-Object {
    $expected = if ($_.Name -match 'win-arm64') { 'win-arm64' }
                elseif ($_.Name -match 'win-x64') { 'win-x64' }
                else { $null }

    if ($null -eq $expected) {
        $failures.Add("无法从文件名判断架构：$($_.Name)")
        return
    }

    $actual = Get-PeArchitecture $_.FullName
    if ($actual -ne $expected) {
        $failures.Add("架构不符：$($_.Name) 文件名声明 $expected，实际 $actual")
    }
    else {
        Write-Host "  架构 OK  $($_.Name) -> $actual"
    }
}

# 3. 可执行性：仅当当前机器架构与某个产物一致时才试运行
$currentRid = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
    ([System.Runtime.InteropServices.Architecture]::X64)   { 'win-x64' }
    ([System.Runtime.InteropServices.Architecture]::Arm64) { 'win-arm64' }
    default { $null }
}

$matchingExe = Get-ChildItem $ArtifactDirectory -Filter "*$currentRid*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($matchingExe) {
    # 优先试普通发布包（不带 -full 后缀），没有则退回任意匹配项。
    # WinExe 被 PowerShell 直接调用时不会等待，必须显式等待才能拿到退出码。
    $stdoutFile = [System.IO.Path]::GetTempFileName()
    $stderrFile = [System.IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $matchingExe.FullName `
            -ArgumentList '--version' `
            -Wait -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput $stdoutFile `
            -RedirectStandardError $stderrFile
        $output = (Get-Content $stdoutFile -Raw -ErrorAction SilentlyContinue) +
                  (Get-Content $stderrFile -Raw -ErrorAction SilentlyContinue)
        $exitCode = $process.ExitCode
    }
    finally {
        Remove-Item $stdoutFile, $stderrFile -Force -ErrorAction SilentlyContinue
    }

    if ($exitCode -ne 0) {
        $failures.Add("--version 退出码异常：$exitCode")
    }
    elseif (-not ($output -match 'AudioCropper')) {
        $failures.Add('--version 输出不符合预期')
    }
    else {
        Write-Host "  运行 OK  $($matchingExe.Name) -> $($output.Trim())"
    }
}
else {
    Write-Host "  跳过运行检查（当前架构 $currentRid 无对应产物）" -ForegroundColor Yellow
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "校验失败（$($failures.Count) 项）：" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "全部校验通过：$ArtifactDirectory" -ForegroundColor Green
