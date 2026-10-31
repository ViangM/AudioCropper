namespace AudioCropper.Safety;

/// <summary>
/// 一次性临时工作目录。设计文档第 7 章要求：
/// 在目标目录附近创建唯一临时目录，成功提交后再删除，失败时保留位置信息。
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    private bool _disposed;

    private TempWorkspace(string path)
    {
        Path = path;
        Directory.CreateDirectory(path);
    }

    /// <summary>临时工作目录的绝对路径。</summary>
    public string Path { get; }

    /// <summary>清理是否失败。失败时界面应提示用户该目录位置。</summary>
    public bool CleanupFailed { get; private set; }

    /// <summary>
    /// 创建一个唯一临时目录。优先放在 <paramref name="preferredParent"/> 下
    /// （通常是输出目录附近，可避免跨卷移动），失败时退回系统临时目录。
    /// </summary>
    public static TempWorkspace Create(string? preferredParent, string label = "part")
    {
        var safeLabel = string.IsNullOrWhiteSpace(label) ? "part" : SanitizeLabel(label);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var name = $"{safeLabel}-{stamp}-{suffix}";

        foreach (var root in CandidateRoots(preferredParent))
        {
            try
            {
                return new TempWorkspace(System.IO.Path.Combine(root, name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // 尝试下一个候选位置。
            }
        }

        throw new IOException("无法创建临时工作目录。请检查磁盘空间和目录权限。");
    }

    private static IEnumerable<string> CandidateRoots(string? preferredParent)
    {
        if (!string.IsNullOrWhiteSpace(preferredParent))
        {
            yield return preferredParent!;
        }

        yield return System.IO.Path.GetTempPath();
    }

    private static string SanitizeLabel(string label)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = label.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var text = new string(chars);
        return text.Length > 40 ? text[..40] : text;
    }

    /// <summary>删除本次工作目录。失败不抛出，仅记录状态。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CleanupFailed = !TryDelete(Path);
        if (CleanupFailed)
        {
            AppLogger.Warn($"临时目录清理失败，已保留：{Path}");
        }
    }

    /// <summary>尽量删除目录，带简单重试（文件句柄可能尚未释放）。</summary>
    public static bool TryDelete(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return true;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < 2)
                {
                    Thread.Sleep(150 * (attempt + 1));
                }
            }
        }

        return false;
    }
}
