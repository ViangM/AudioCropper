using System.Diagnostics;
using System.Security.Cryptography;
using AudioCropper.Bootstrap;
using AudioCropper.Safety;

namespace AudioCropper.Install;

/// <summary>
/// 手动升级（设计文档 8.1）：校验新版本 → 备份旧文件 → 替换 → 试启动 → 失败回滚。
/// 第一版不引入常驻更新服务，由用户主动选择新版本 EXE。
/// </summary>
public sealed class UpgradeService
{
    public async Task<InstallResult> ApplyUpdateAsync(
        string newExecutablePath,
        string? expectedSha256 = null,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(newExecutablePath))
        {
            return InstallResult.Fail("新版本文件不存在。");
        }

        status?.Report("正在校验新版本文件…");

        var architecture = PortableExecutableInspector.TryReadArchitecture(newExecutablePath);
        if (architecture is null)
        {
            return InstallResult.Fail("无法识别新版本的文件格式，这可能不是有效的 Windows 可执行文件。");
        }

        if (architecture != AppEnvironment.ProcessArchitecture)
        {
            return InstallResult.Fail(
                $"新版本架构为 {architecture}，与当前运行的 {AppEnvironment.ProcessArchitecture} 不匹配。" +
                "请下载与设备架构一致的版本。");
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var actual = await ComputeSha256Async(newExecutablePath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return InstallResult.Fail("新版本文件哈希校验失败，文件可能不完整或被篡改。");
            }
        }

        var targetExecutable = ResolveCurrentExecutable();
        if (targetExecutable is null)
        {
            return InstallResult.Fail("无法确定当前程序位置，无法升级。");
        }

        status?.Report("正在替换程序文件…");

        var backupPath = targetExecutable + ".old";
        TryDeleteFile(backupPath);

        try
        {
            File.Move(targetExecutable, backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return InstallResult.Fail($"无法替换程序文件，请关闭 AudioCropper 的所有实例后重试：{ex.Message}");
        }

        try
        {
            File.Copy(newExecutablePath, targetExecutable, overwrite: false);

            var copiedArchitecture = PortableExecutableInspector.TryReadArchitecture(targetExecutable);
            if (copiedArchitecture != architecture)
            {
                throw new IOException("替换后的文件架构校验失败。");
            }

            status?.Report("正在验证新版本可启动…");
            if (!await CanStartAsync(targetExecutable, cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("新版本启动验证失败。");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 回滚到旧版本，保证用户至少还能使用原有程序。
            TryDeleteFile(targetExecutable);
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Move(backupPath, targetExecutable);
                }
            }
            catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException)
            {
                AppLogger.Error("升级回滚失败，需要用户手动恢复。", restoreError);
                return InstallResult.Fail($"升级失败，且自动回滚未成功。旧版本保存在：{backupPath}");
            }

            AppLogger.Error("升级失败，已回滚到旧版本。", ex);
            return InstallResult.Fail($"升级失败，已恢复旧版本：{ex.Message}");
        }

        // 旧版本文件可能仍在被当前进程占用，留到下次启动时清理。
        TryDeleteFile(backupPath);

        AppLogger.Info($"升级完成：{targetExecutable}");
        return InstallResult.Ok(targetExecutable, [], replaced: true) with
        {
            Message = "升级完成，请重新启动 AudioCropper。",
        };
    }

    /// <summary>清理上次升级遗留的 .old 文件；启动时调用。</summary>
    public static void TryCleanupOldBinaries()
    {
        try
        {
            var path = ResolveCurrentExecutable();
            if (path is null)
            {
                return;
            }

            var backup = path + ".old";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Warn($"清理旧版本文件失败：{ex.Message}");
        }
    }

    private static string? ResolveCurrentExecutable()
    {
        var registered = InstallRegistry.ReadInstallLocation();
        if (!string.IsNullOrWhiteSpace(registered))
        {
            var installed = Path.Combine(registered!, InstallerService.ExecutableFileName);
            if (File.Exists(installed))
            {
                return installed;
            }
        }

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath)
            && string.Equals(Path.GetFileName(processPath), InstallerService.ExecutableFileName, StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        return null;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    /// <summary>用 --version 试运行新版本，确认它至少能正常启动并退出。</summary>
    private static async Task<bool> CanStartAsync(string executablePath, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("--version");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // 已退出。
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            AppLogger.Warn($"试启动新版本失败：{ex.Message}");
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Warn($"删除文件失败：{path} — {ex.Message}");
        }
    }
}
