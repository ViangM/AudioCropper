using System.Runtime.InteropServices;
using AudioCropper.Safety;

namespace AudioCropper.Install;

/// <summary>
/// 卸载注册信息。全部写入 HKCU，不需要管理员权限（设计文档 10.3）。
/// </summary>
internal static class InstallRegistry
{
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string KeyName = AppPaths.ProductName;

    public static bool IsInstalled
    {
        get
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{UninstallRoot}\{KeyName}");
            return key is not null;
        }
    }

    /// <summary>读取已登记的程序安装目录，未安装时返回 null。</summary>
    public static string? ReadInstallLocation()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{UninstallRoot}\{KeyName}");
        return key?.GetValue("InstallLocation") as string;
    }

    public static string? ReadDisplayVersion()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{UninstallRoot}\{KeyName}");
        return key?.GetValue("DisplayVersion") as string;
    }

    /// <summary>登记卸载信息。失败时抛出，由调用方转成警告或失败。</summary>
    public static void Write(InstallOptions options, string executablePath, string version)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"{UninstallRoot}\{KeyName}")
                        ?? throw new InvalidOperationException("无法创建卸载注册表项。");

        key.SetValue("DisplayName", AppPaths.ProductName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", AppPaths.ProductName);
        key.SetValue("InstallLocation", options.InstallDirectory);
        key.SetValue("DisplayIcon", $"\"{executablePath}\",0");
        key.SetValue("UninstallString", $"\"{executablePath}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{executablePath}\" --uninstall");
        key.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", EstimateSizeKb(options.InstallDirectory), Microsoft.Win32.RegistryValueKind.DWord);

        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            key.SetValue("Architecture", "arm64");
        }
    }

    public static void Delete()
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"{UninstallRoot}\{KeyName}", throwOnMissingSubKey: false);
    }

    private static int EstimateSizeKb(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            var bytes = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path =>
                {
                    try
                    {
                        return new FileInfo(path).Length;
                    }
                    catch (IOException)
                    {
                        return 0L;
                    }
                })
                .Sum();

            return (int)Math.Min(int.MaxValue, bytes / 1024);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}

/// <summary>快捷方式。使用 WScript.Shell 的 COM 接口创建 .lnk。</summary>
internal static class ShortcutHelper
{
    public static string DesktopShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        $"{AppPaths.ProductName}.lnk");

    public static string StartMenuShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        $"{AppPaths.ProductName}.lnk");

    /// <summary>创建或更新快捷方式。失败时抛出，由调用方降级为警告。</summary>
    public static void CreateOrUpdate(string linkPath, string targetExecutable, string description)
    {
        var directory = Path.GetDirectoryName(linkPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
                        ?? throw new InvalidOperationException("系统未提供 WScript.Shell，无法创建快捷方式。");

        dynamic shell = Activator.CreateInstance(shellType)
                        ?? throw new InvalidOperationException("无法创建 WScript.Shell 实例。");

        try
        {
            dynamic shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath = targetExecutable;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExecutable) ?? string.Empty;
            shortcut.IconLocation = $"{targetExecutable},0";
            shortcut.Description = description;
            shortcut.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    public static bool DeleteIfExists(string linkPath)
    {
        try
        {
            if (!File.Exists(linkPath))
            {
                return false;
            }

            File.Delete(linkPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Warn($"删除快捷方式失败：{linkPath} — {ex.Message}");
            return false;
        }
    }
}
