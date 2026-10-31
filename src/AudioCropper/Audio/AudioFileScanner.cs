using System.IO;

namespace AudioCropper.Audio;

/// <summary>
/// 扫描可导入的音频文件，支持多级目录。
/// 只依据扩展名做初筛（容器与编码能力由 FFmpeg 在探测阶段确认），
/// 并跳过重解析点，避免目录联接导致无限递归。
/// </summary>
public static class AudioFileScanner
{
    /// <summary>单次导入的文件上限，避免误选巨大目录时界面卡死。</summary>
    public const int MaxFilesPerImport = 5000;

    /// <summary>可导入的扩展名（输入侧；实际能否解码仍以 FFmpeg 探测为准）。</summary>
    public static readonly IReadOnlySet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".m4b", ".aac", ".wav", ".flac", ".ogg", ".oga", ".opus",
        ".wma", ".aif", ".aiff", ".ape", ".alac", ".amr", ".ac3", ".eac3", ".dts",
        ".mp4", ".m4v", ".mkv", ".webm", ".mov", ".avi", ".ts", ".flv",
    };

    /// <summary>文件对话框过滤器。</summary>
    public const string OpenFileFilter =
        "音频/视频文件|*.mp3;*.m4a;*.m4b;*.aac;*.wav;*.flac;*.ogg;*.oga;*.opus;*.wma;*.aif;*.aiff;*.ape;*.amr;*.ac3;*.eac3;*.dts;*.mp4;*.m4v;*.mkv;*.webm;*.mov;*.avi;*.ts;*.flv|所有文件|*.*";

    public static bool IsSupportedFile(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// 展开用户选择的路径：文件直接收下（不校验扩展名以外的条件），目录按需递归扫描。
    /// 结果去重并按路径排序。
    /// </summary>
    public static IReadOnlyList<string> Expand(IEnumerable<string> paths, bool recursive, out int truncatedCount)
    {
        truncatedCount = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string path;
            try
            {
                path = Path.GetFullPath(raw);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (Directory.Exists(path))
            {
                foreach (var file in ScanDirectory(path, recursive))
                {
                    if (seen.Add(file))
                    {
                        result.Add(file);
                    }

                    if (result.Count >= MaxFilesPerImport)
                    {
                        truncatedCount = 1;
                        break;
                    }
                }
            }
            else if (File.Exists(path) && seen.Add(path))
            {
                result.Add(path);
            }

            if (result.Count >= MaxFilesPerImport)
            {
                truncatedCount = 1;
                break;
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>递归（可选）扫描目录下的音频文件。</summary>
    public static IEnumerable<string> ScanDirectory(string directory, bool recursive)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            // 跳过目录联接/符号链接，避免循环递归。
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        try
        {
            return Directory
                .EnumerateFiles(directory, "*.*", options)
                .Where(IsSupportedFile)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
