using System.Reflection;
using System.Security.Cryptography;
using AudioCropper.Bootstrap;
using AudioCropper.Safety;

namespace AudioCropper.Audio;

/// <summary>一组可用的 FFmpeg 可执行文件。</summary>
/// <param name="FfmpegPath">ffmpeg 可执行文件路径。</param>
/// <param name="FfprobePath">ffprobe 路径，可能为空（此时用 ffmpeg 解析媒体信息）。</param>
/// <param name="Origin">来源说明，仅用于日志与诊断。</param>
public sealed record FfmpegToolset(string FfmpegPath, string? FfprobePath, string Origin);

public interface IFfmpegLocator
{
    FfmpegToolset Resolve();
}

/// <summary>
/// 定位 FFmpeg（设计文档 4.3）。顺序：
/// 1. 环境变量 <c>AUDIOCROPPER_FFMPEG_DIR</c>（仅开发/测试用）；
/// 2. EXE 同目录（开发期放置的 sidecar）；
/// 3. 内嵌资源释放到应用专用缓存目录（正式发布路径）。
/// 不使用系统 PATH —— 避免依赖用户机器上恰好存在的 FFmpeg。
/// </summary>
public sealed class FfmpegLocator : IFfmpegLocator
{
    public const string OverrideEnvironmentVariable = "AUDIOCROPPER_FFMPEG_DIR";
    public const string FfmpegFileName = "ffmpeg.exe";
    public const string FfprobeFileName = "ffprobe.exe";

    private readonly object _gate = new();
    private FfmpegToolset? _cached;

    public FfmpegToolset Resolve()
    {
        lock (_gate)
        {
            return _cached ??= ResolveCore();
        }
    }

    private static FfmpegToolset ResolveCore()
    {
        var overridden = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridden) && TryCreateToolset(overridden!, "环境变量", out var fromEnvironment))
        {
            return fromEnvironment!;
        }

        var exeDirectory = AppPaths.ExecutableDirectory;
        if (TryCreateToolset(exeDirectory, "EXE 同目录", out var sidecar))
        {
            return sidecar!;
        }

        if (TryCreateToolset(Path.Combine(exeDirectory, "ffmpeg"), "EXE 同级 ffmpeg 目录", out var sidecarSub))
        {
            return sidecarSub!;
        }

        var extracted = EmbeddedFfmpegExtractor.TryExtract();
        if (extracted is not null)
        {
            return extracted;
        }

        var environmentValue = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        var hint = string.IsNullOrWhiteSpace(environmentValue)
            ? "未设置"
            : $"已设置为 {environmentValue}，但该目录下没有 {FfmpegFileName}";

        throw new FfmpegNotFoundException(
            $"未找到 FFmpeg 组件（{FfmpegFileName}），无法处理音频。" + Environment.NewLine + Environment.NewLine +
            "已查找：" + Environment.NewLine +
            $"· 环境变量 {OverrideEnvironmentVariable}：{hint}" + Environment.NewLine +
            $"· 程序目录：{exeDirectory}" + Environment.NewLine +
            $"· 内嵌资源：{AppEnvironment.ArchitectureTag}" + Environment.NewLine + Environment.NewLine +
            "解决方式：请使用带 FFmpeg 的完整版（文件名含 with-ffmpeg / full），" +
            "或用 build/pack-ffmpeg.ps1 重新打包，或设置 " + OverrideEnvironmentVariable + " 指向 ffmpeg.exe 所在目录。");
    }

    private static bool TryCreateToolset(string directory, string origin, out FfmpegToolset? toolset)
    {
        toolset = null;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var ffmpeg = Path.Combine(directory, FfmpegFileName);
        if (!File.Exists(ffmpeg))
        {
            return false;
        }

        var ffprobe = Path.Combine(directory, FfprobeFileName);
        toolset = new FfmpegToolset(ffmpeg, File.Exists(ffprobe) ? ffprobe : null, origin);
        return true;
    }
}

/// <summary>
/// 把嵌入主 EXE 的 FFmpeg 资源释放到应用专用缓存目录。
/// 满足设计文档 4.3 的要求：先校验哈希、使用专用目录、防止路径穿越、
/// 释放失败明确报错、不清理非本程序文件。
/// </summary>
internal static class EmbeddedFfmpegExtractor
{
    private const string ManifestSuffix = ".manifest.txt";

    public static FfmpegToolset? TryExtract()
    {
        var assembly = typeof(EmbeddedFfmpegExtractor).Assembly;
        var prefix = $"ffmpeg.{AppEnvironment.ArchitectureTag}.";
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (resources.Length == 0)
        {
            return null;
        }

        var manifestName = resources.FirstOrDefault(n => n.EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase));
        if (manifestName is null)
        {
            throw new FfmpegNotFoundException("内嵌 FFmpeg 资源缺少 manifest，发布包不完整。");
        }

        var entries = ReadManifest(assembly, manifestName);
        if (entries.Count == 0)
        {
            throw new FfmpegNotFoundException("内嵌 FFmpeg manifest 为空，发布包不完整。");
        }

        var versionKey = GetVersionKey(assembly);
        var targetDirectory = Path.Combine(AppPaths.NativeCacheDirectory, $"{AppEnvironment.ArchitectureTag}-{versionKey}");

        try
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (var (fileName, expectedHash) in entries)
            {
                var resourceName = prefix + fileName;
                var targetPath = Path.Combine(targetDirectory, fileName);

                if (File.Exists(targetPath) && HashMatches(targetPath, expectedHash))
                {
                    continue;
                }

                ExtractOne(assembly, resourceName, targetPath, expectedHash);
            }
        }
        catch (FfmpegNotFoundException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new FfmpegNotFoundException(
                $"释放音频组件失败：{ex.Message}。请检查目录权限和磁盘空间：{targetDirectory}", ex);
        }

        var ffmpegPath = Path.Combine(targetDirectory, FfmpegLocator.FfmpegFileName);
        if (!File.Exists(ffmpegPath))
        {
            throw new FfmpegNotFoundException("内嵌 FFmpeg 资源中缺少 ffmpeg.exe。");
        }

        var ffprobePath = Path.Combine(targetDirectory, FfmpegLocator.FfprobeFileName);
        return new FfmpegToolset(ffmpegPath, File.Exists(ffprobePath) ? ffprobePath : null, "内嵌资源");
    }

    private static void ExtractOne(Assembly assembly, string resourceName, string targetPath, string expectedHash)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FfmpegNotFoundException($"发布包缺少资源：{resourceName}");

        var temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(file);
            }

            if (!HashMatches(temporaryPath, expectedHash))
            {
                throw new FfmpegNotFoundException($"音频组件校验失败（SHA-256 不匹配）：{resourceName}");
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // 临时文件残留不影响主流程。
                }
            }
        }
    }

    private static bool HashMatches(string path, string expectedHex)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(SHA256.HashData(stream));
            return string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>读取 manifest：每行 "文件名 sha256"。</summary>
    private static List<(string FileName, string Sha256)> ReadManifest(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FfmpegNotFoundException($"发布包缺少资源：{resourceName}");
        using var reader = new StreamReader(stream);

        var result = new List<(string, string)>();
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var fileName = parts[0];
            var hash = parts[1];

            // 防路径穿越：只接受纯文件名。
            if (fileName != Path.GetFileName(fileName) || fileName.Contains("..") || hash.Length != 64)
            {
                AppLogger.Warn($"忽略可疑的 FFmpeg manifest 条目：{trimmed}");
                continue;
            }

            result.Add((fileName, hash));
        }

        return result;
    }

    private static string GetVersionKey(Assembly assembly)
    {
        var text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0";
        var chars = text.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_').ToArray();
        return new string(chars);
    }
}
