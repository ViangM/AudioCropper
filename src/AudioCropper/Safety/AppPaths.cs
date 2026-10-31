namespace AudioCropper.Safety;

/// <summary>
/// 应用使用的全部目录与文件位置。
/// 设计文档第 6 章：程序文件、用户设置、日志、临时文件必须分开管理。
/// 所有路径都可由测试通过 <see cref="OverrideForTesting"/> 替换。
/// </summary>
public static class AppPaths
{
    /// <summary>产品名，同时用作安装目录名与注册表项名。</summary>
    public const string ProductName = "AudioCropper";

    /// <summary>便携模式标记文件：与 EXE 同目录存在时，数据写入 EXE 旁的 Data 目录。</summary>
    public const string PortableFlagFileName = "AudioCropper.portable";

    private static string? _overrideRoot;

    /// <summary>EXE 所在目录（单文件发布时为实际 EXE 路径所在目录）。</summary>
    public static string ExecutableDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)
        ?? AppContext.BaseDirectory;

    /// <summary>便携模式是否启用（存在标记文件且目录可写）。</summary>
    public static bool IsPortable
    {
        get
        {
            if (_overrideRoot is not null)
            {
                return false;
            }

            var flag = Path.Combine(ExecutableDirectory, PortableFlagFileName);
            return File.Exists(flag) && IsDirectoryWritable(ExecutableDirectory);
        }
    }

    /// <summary>用户数据根目录（设置、日志、缓存）。</summary>
    public static string DataRoot =>
        _overrideRoot ?? (IsPortable
            ? Path.Combine(ExecutableDirectory, "Data")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductName));

    /// <summary>用户设置文件。</summary>
    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");

    /// <summary>日志目录。</summary>
    public static string LogsDirectory => Path.Combine(DataRoot, "logs");

    /// <summary>从 EXE 释放出的原生组件缓存目录（按版本/架构隔离）。</summary>
    public static string NativeCacheDirectory => Path.Combine(DataRoot, "cache", "native");

    /// <summary>默认安装目录：当前用户范围，无需管理员权限。</summary>
    public static string DefaultInstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        ProductName);

    /// <summary>确保目录存在并返回该目录。</summary>
    public static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>测试用：把数据根目录重定向到临时位置。</summary>
    public static void OverrideForTesting(string? dataRoot) => _overrideRoot = dataRoot;

    /// <summary>判断目录是否可写（用于便携模式回退判断）。</summary>
    public static bool IsDirectoryWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
            using (var fs = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                fs.WriteByte(0);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
