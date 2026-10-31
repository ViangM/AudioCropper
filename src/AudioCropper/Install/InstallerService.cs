using AudioCropper.Bootstrap;
using AudioCropper.Safety;

namespace AudioCropper.Install;

/// <summary>
/// 内置安装流程（设计文档 5.2）。默认按当前用户安装，不要求管理员权限：
/// 程序文件写入用户选择的目录，快捷方式与卸载信息写入 HKCU。
/// </summary>
public sealed class InstallerService
{
    public const string ExecutableFileName = "AudioCropper.exe";

    /// <summary>默认安装目录：已有安装则沿用原位置，否则用当前用户目录。</summary>
    public static string ResolveDefaultInstallDirectory()
    {
        var registered = InstallRegistry.ReadInstallLocation();
        return string.IsNullOrWhiteSpace(registered) ? AppPaths.DefaultInstallDirectory : registered!;
    }

    public async Task<InstallResult> InstallAsync(
        InstallOptions options,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        status?.Report("正在检查安装环境…");

        string installDirectory;
        try
        {
            installDirectory = Path.GetFullPath(options.InstallDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return InstallResult.Fail($"安装目录无效：{ex.Message}");
        }

        if (IsProtectedDirectory(installDirectory))
        {
            return InstallResult.Fail("不能安装到系统目录或磁盘根目录，请选择用户目录下的位置，例如“文档”或本地应用数据目录。");
        }

        var sourceExecutable = ResolveSourceExecutable();
        if (sourceExecutable is null)
        {
            return InstallResult.Fail("当前不是以发布版单文件 EXE 运行，无法执行安装。请使用发布包中的 AudioCropper.exe。");
        }

        var sourceArchitecture = PortableExecutableInspector.TryReadArchitecture(sourceExecutable);
        if (sourceArchitecture is not null && sourceArchitecture != AppEnvironment.ProcessArchitecture)
        {
            return InstallResult.Fail(
                $"待安装程序的架构（{sourceArchitecture}）与当前运行的进程架构（{AppEnvironment.ProcessArchitecture}）不一致。");
        }

        var targetExecutable = Path.Combine(installDirectory, ExecutableFileName);
        if (PathsEqual(sourceExecutable, targetExecutable))
        {
            return InstallResult.Ok(targetExecutable, [], replaced: true) with
            {
                Message = "程序已经安装在该目录。",
            };
        }

        var replacedExisting = InstallRegistry.IsInstalled || File.Exists(targetExecutable);
        var warnings = new List<string>();

        try
        {
            await Task.Run(
                () => InstallCore(installDirectory, sourceExecutable, targetExecutable, options, warnings, status, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return InstallResult.Fail("安装已取消。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AppLogger.Error($"安装失败：{installDirectory}", ex);
            return InstallResult.Fail($"安装失败：{ex.Message}", warnings);
        }

        AppLogger.Info($"安装完成：{targetExecutable}");
        return InstallResult.Ok(targetExecutable, warnings, replacedExisting);
    }

    private static void InstallCore(
        string installDirectory,
        string sourceExecutable,
        string targetExecutable,
        InstallOptions options,
        List<string> warnings,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        status?.Report("正在准备安装目录…");
        Directory.CreateDirectory(installDirectory);

        if (!AppPaths.IsDirectoryWritable(installDirectory))
        {
            throw new IOException($"没有写入权限：{installDirectory}。请选择其他目录，或以管理员身份运行。");
        }

        EnsureFreeSpace(installDirectory, new FileInfo(sourceExecutable).Length);

        cancellationToken.ThrowIfCancellationRequested();
        status?.Report("正在复制程序文件…");

        var temporaryPath = Path.Combine(installDirectory, $".install-{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourceExecutable, temporaryPath, overwrite: true);

            if (File.Exists(targetExecutable))
            {
                EnsureNotRunning(targetExecutable);
            }

            File.Move(temporaryPath, targetExecutable, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        status?.Report("正在创建快捷方式…");

        if (options.CreateDesktopShortcut)
        {
            TryCreateShortcut(ShortcutHelper.DesktopShortcutPath, targetExecutable, "AudioCropper 桌面快捷方式", warnings);
        }

        if (options.CreateStartMenuShortcut)
        {
            TryCreateShortcut(ShortcutHelper.StartMenuShortcutPath, targetExecutable, "AudioCropper 开始菜单快捷方式", warnings);
        }

        cancellationToken.ThrowIfCancellationRequested();
        status?.Report("正在登记卸载信息…");

        try
        {
            InstallRegistry.Write(options, targetExecutable, AppEnvironment.VersionText);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            warnings.Add($"卸载信息登记失败，可能无法在“应用和功能”中卸载：{ex.Message}");
        }

        status?.Report("正在验证安装结果…");
        var installed = new FileInfo(targetExecutable);
        if (!installed.Exists)
        {
            throw new IOException("安装后未找到程序文件，安装失败。");
        }

        if (installed.Length != new FileInfo(sourceExecutable).Length)
        {
            throw new IOException("安装后的文件大小与源文件不一致，安装可能不完整。");
        }
    }

    /// <summary>当前正在运行的可执行文件（发布版单文件）。</summary>
    private static string? ResolveSourceExecutable()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath)
            && File.Exists(processPath)
            && string.Equals(Path.GetFileName(processPath), ExecutableFileName, StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var localCopy = Path.Combine(AppContext.BaseDirectory, ExecutableFileName);
        return File.Exists(localCopy) ? localCopy : null;
    }

    private static bool IsProtectedDirectory(string directory)
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windowsDirectory)
            && directory.StartsWith(windowsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var root = Path.GetPathRoot(directory);
        return !string.IsNullOrEmpty(root) && string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureFreeSpace(string directory, long requiredBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        var drive = new DriveInfo(root);
        var needed = requiredBytes * 2 + 64L * 1024 * 1024;
        if (drive.AvailableFreeSpace < needed)
        {
            throw new IOException($"目标磁盘剩余空间不足，需要至少 {needed / 1024 / 1024} MB。");
        }
    }

    private static void EnsureNotRunning(string executablePath)
    {
        try
        {
            using var stream = new FileStream(executablePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new IOException("AudioCropper 正在运行，请先关闭程序再安装。", ex);
        }
    }

    private static void TryCreateShortcut(string linkPath, string targetExecutable, string description, List<string> warnings)
    {
        try
        {
            ShortcutHelper.CreateOrUpdate(linkPath, targetExecutable, description);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            warnings.Add($"快捷方式创建失败（{Path.GetFileName(linkPath)}）：{ex.Message}");
        }
    }

    private static void TryDelete(string path)
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
            AppLogger.Warn($"删除临时文件失败：{path}");
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
