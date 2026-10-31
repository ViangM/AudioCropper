namespace AudioCropper.Audio;

/// <summary>
/// 输出目录规则（设计文档第 6、7 章）：
/// 默认输出到"源文件同目录 / 源文件同名文件夹"，例如
/// <c>D:\Podcasts\lesson.mp3</c> → <c>D:\Podcasts\lesson\0001.mp3</c>。
/// </summary>
public static class OutputLayout
{
    /// <summary>源文件对应的默认输出目录。</summary>
    public static string GetDefaultOutputDirectory(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var directory = Path.GetDirectoryName(fullPath)
                        ?? throw new ArgumentException("源文件路径无效。", nameof(sourcePath));
        var name = Path.GetFileNameWithoutExtension(fullPath);
        return Path.Combine(directory, name);
    }

    /// <summary>
    /// 生成一个当前不存在的编号目录：<c>lesson</c> → <c>lesson (1)</c>。
    /// 已存在的目录一律不覆盖、不清空。
    /// </summary>
    public static string CreateNumberedVariant(string desiredDirectory)
    {
        var fullPath = Path.GetFullPath(desiredDirectory);
        if (!Directory.Exists(fullPath))
        {
            return fullPath;
        }

        var parent = Path.GetDirectoryName(fullPath)
                     ?? throw new ArgumentException("目录路径无效。", nameof(desiredDirectory));
        var name = Path.GetFileName(fullPath);

        for (var index = 1; index < 10_000; index++)
        {
            var candidate = Path.Combine(parent, $"{name} ({index})");
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("无法创建新的编号目录：同名目录过多。");
    }

    /// <summary>统计目录顶层的文件数量，用于向用户说明影响范围。</summary>
    public static int CountFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(directory).Count();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
