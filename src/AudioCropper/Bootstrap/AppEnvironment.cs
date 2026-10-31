using System.Reflection;
using System.Runtime.InteropServices;

namespace AudioCropper.Bootstrap;

/// <summary>
/// 运行环境探测：CPU 架构、系统版本与模拟运行判断（设计文档第 10 章）。
/// 单 EXE 按架构分别发布，因此进程架构与系统架构不一致通常意味着用户在 ARM64
/// 上运行了 x64 版本（Windows 会模拟执行），应提示改用 ARM64 原生版本。
/// </summary>
public static class AppEnvironment
{
    /// <summary>当前进程架构。</summary>
    public static Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;

    /// <summary>操作系统架构。</summary>
    public static Architecture OsArchitecture => RuntimeInformation.OSArchitecture;

    /// <summary>是否正在被模拟执行（x64 进程跑在 ARM64 系统上）。</summary>
    public static bool IsEmulated =>
        OsArchitecture == Architecture.Arm64 && ProcessArchitecture == Architecture.X64;

    /// <summary>当前 EXE 对应的发布架构标识。</summary>
    public static string ArchitectureTag => ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.Arm64 => "win-arm64",
        _ => "unsupported",
    };

    /// <summary>版本号（来自程序集信息）。</summary>
    public static string VersionText =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>系统版本描述，例如 "Windows 10.0.19045 (X64, 进程 X64)"。</summary>
    public static string Describe()
    {
        var emulated = IsEmulated ? "，模拟运行" : string.Empty;
        return $"Windows {Environment.OSVersion.Version} ({OsArchitecture}, 进程 {ProcessArchitecture}{emulated})";
    }

    /// <summary>如果架构不受支持或存在模拟，返回需要提示用户的文字。</summary>
    public static string? GetArchitectureNotice()
    {
        if (ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
        {
            return "当前程序的 CPU 架构不受支持，请下载对应架构的版本。";
        }

        if (IsEmulated)
        {
            return "检测到 x64 版本正在 ARM64 设备上模拟运行。为提高性能和兼容性，建议下载 ARM64 原生版本。";
        }

        return null;
    }
}
