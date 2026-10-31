using System.Diagnostics;
using AudioCropper.Safety;

namespace AudioCropper.Install;

/// <summary>
/// 卸载流程（设计文档第 9 章）。只删除能确认属于 AudioCropper 的文件与登记信息：
/// 源音频与导出片段永远不在安装目录内，因此不会被触碰。
/// </summary>
public sealed class UninstallService
{
    private const string DeferredScriptFileName = "audiocropper-uninstall.cmd";

    public UninstallResult Uninstall(bool removeUserSettings)
    {
        var leftovers = new List<string>();
        var registered = InstallRegistry.ReadInstallLocation();
        var installDirectory = string.IsNullOrWhiteSpace(registered)
            ? AppPaths.ExecutableDirectory
            : registered!;

        // 1. 本程序创建的快捷方式
        ShortcutHelper.DeleteIfExists(ShortcutHelper.DesktopShortcutPath);
        ShortcutHelper.DeleteIfExists(ShortcutHelper.StartMenuShortcutPath);

        // 2. 卸载注册信息
        try
        {
            InstallRegistry.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            leftovers.Add($@"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppPaths.ProductName}");
            AppLogger.Warn($"删除卸载注册信息失败：{ex.Message}");
        }

        // 3. 用户设置（默认保留，仅按用户选择删除）
        if (removeUserSettings && !IsInside(AppPaths.DataRoot, installDirectory))
        {
            if (!TempWorkspace.TryDelete(AppPaths.DataRoot))
            {
                leftovers.Add(AppPaths.DataRoot);
            }
        }

        // 4. 程序文件
        var executablePath = Path.Combine(installDirectory, InstallerService.ExecutableFileName);
        var deferredCleanup = false;
        var currentProcess = Environment.ProcessPath;

        if (File.Exists(executablePath))
        {
            if (currentProcess is not null && IsSamePath(executablePath, currentProcess))
            {
                // 正在运行的就是要删除的文件：交给后台脚本在进程退出后清理。
                deferredCleanup = TryScheduleDeferredCleanup(executablePath, installDirectory);
                if (!deferredCleanup)
                {
                    leftovers.Add(executablePath);
                }
            }
            else if (!TryDeleteFile(executablePath))
            {
                leftovers.Add(executablePath);
            }
        }

        // 5. 目录：只在空目录时移除，并列出无法确认归属的残留文件。
        if (!deferredCleanup && Directory.Exists(installDirectory))
        {
            var remaining = EnumerateTopLevel(installDirectory);
            foreach (var item in remaining)
            {
                leftovers.Add(item);
            }

            if (remaining.Count == 0 && !TryDeleteEmptyDirectory(installDirectory))
            {
                leftovers.Add(installDirectory);
            }
        }

        AppLogger.Info($"卸载完成：{installDirectory}（残留 {leftovers.Count} 项）");

        return new UninstallResult
        {
            Success = true,
            Message = deferredCleanup
                ? "程序文件将在 AudioCropper 退出后自动删除。"
                : "卸载完成。",
            Leftovers = leftovers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            DeferredCleanup = deferredCleanup,
        };
    }

    /// <summary>
    /// 用纯 ASCII 的批处理脚本等待当前进程释放 EXE，再删除文件与安装目录。
    /// 路径通过参数传入，避免脚本文件本身出现非 ASCII 编码问题。
    /// </summary>
    private static bool TryScheduleDeferredCleanup(string executablePath, string installDirectory)
    {
        try
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), DeferredScriptFileName);
            File.WriteAllText(scriptPath, """
                @echo off
                setlocal
                :wait
                timeout /t 1 /nobreak >nul
                2>nul del /f /q "%~1"
                if exist "%~1" goto wait
                2>nul rmdir /s /q "%~2"
                2>nul del /f /q "%~f0"
                """);

            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add(executablePath);
            startInfo.ArgumentList.Add(installDirectory);

            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            AppLogger.Warn($"无法启动卸载清理脚本：{ex.Message}");
            return false;
        }
    }

    private static List<string> EnumerateTopLevel(string directory)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [directory];
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Warn($"删除文件失败：{path} — {ex.Message}");
            return false;
        }
    }

    private static bool TryDeleteEmptyDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Warn($"删除目录失败：{directory} — {ex.Message}");
            return false;
        }
    }

    private static bool IsSamePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsInside(string candidate, string parent)
    {
        try
        {
            var parentFull = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidateFull = Path.GetFullPath(candidate);
            return candidateFull.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            return false;
        }
    }
}
