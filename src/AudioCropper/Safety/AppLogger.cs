using System.Text;

namespace AudioCropper.Safety;

/// <summary>
/// 简单的文件日志。设计文档第 6 章要求日志有独立位置和保留策略；
/// 具体保留天数仍是待定项，这里先实现按体积滚动 + 只保留最近若干个文件。
/// </summary>
public static class AppLogger
{
    private static readonly object Gate = new();
    private static string? _logFilePath;
    private static long _maxFileBytes = 2 * 1024 * 1024;
    private static int _maxFiles = 5;

    public static string? CurrentLogFile
    {
        get
        {
            lock (Gate)
            {
                return _logFilePath;
            }
        }
    }

    /// <summary>初始化日志。失败时不抛出，日志不可用不应导致程序无法启动。</summary>
    public static void Initialize(string? directory = null)
    {
        try
        {
            directory ??= AppPaths.LogsDirectory;
            Directory.CreateDirectory(directory);
            lock (Gate)
            {
                _logFilePath = Path.Combine(directory, $"{AppPaths.ProductName}-{DateTime.Now:yyyyMMdd}.log");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logFilePath = null;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        var text = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write("ERROR", text);
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
        lock (Gate)
        {
            if (_logFilePath is null)
            {
                return;
            }

            try
            {
                RollIfNeeded();
                File.AppendAllText(_logFilePath, line, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 日志写失败不能影响主流程。
            }
        }
    }

    private static void RollIfNeeded()
    {
        if (_logFilePath is null)
        {
            return;
        }

        var info = new FileInfo(_logFilePath);
        if (!info.Exists || info.Length < _maxFileBytes)
        {
            return;
        }

        // 当前文件 → .1，旧的依次后移，多余的删除。
        var directory = info.DirectoryName!;
        var baseName = Path.GetFileNameWithoutExtension(info.Name);
        for (var index = _maxFiles - 1; index >= 1; index--)
        {
            var from = Path.Combine(directory, $"{baseName}.{index}.log");
            var to = Path.Combine(directory, $"{baseName}.{index + 1}.log");
            if (File.Exists(from))
            {
                File.Move(from, to, overwrite: true);
            }
        }

        File.Move(_logFilePath, Path.Combine(directory, $"{baseName}.1.log"), overwrite: true);
    }
}
