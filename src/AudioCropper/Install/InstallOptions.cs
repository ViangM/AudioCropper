namespace AudioCropper.Install;

/// <summary>安装参数（设计文档 5.2）。</summary>
public sealed record InstallOptions
{
    public required string InstallDirectory { get; init; }

    public bool CreateDesktopShortcut { get; init; } = true;

    public bool CreateStartMenuShortcut { get; init; } = true;
}

/// <summary>安装结果。</summary>
public sealed record InstallResult
{
    public bool Success { get; init; }

    public string? InstalledExecutable { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>非致命问题，例如快捷方式创建失败。</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>安装前已存在旧安装。</summary>
    public bool ReplacedExistingInstall { get; init; }

    public static InstallResult Ok(string executable, IReadOnlyList<string> warnings, bool replaced) => new()
    {
        Success = true,
        InstalledExecutable = executable,
        Message = replaced ? "安装完成，已更新原有安装。" : "安装完成。",
        Warnings = warnings,
        ReplacedExistingInstall = replaced,
    };

    public static InstallResult Fail(string message, IReadOnlyList<string>? warnings = null) => new()
    {
        Message = message,
        Warnings = warnings ?? [],
    };
}

/// <summary>卸载结果。</summary>
public sealed record UninstallResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>无法删除、需要用户自行检查的文件或目录。</summary>
    public IReadOnlyList<string> Leftovers { get; init; } = [];

    /// <summary>安装目录已交给后台清理脚本，进程退出后再删除。</summary>
    public bool DeferredCleanup { get; init; }
}
