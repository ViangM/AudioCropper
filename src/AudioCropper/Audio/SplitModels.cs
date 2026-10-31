namespace AudioCropper.Audio;

/// <summary>分段方式。</summary>
public enum SplitMode
{
    /// <summary>按固定时长分段。</summary>
    ByDuration,

    /// <summary>按片段数量均分。</summary>
    ByCount,
}

/// <summary>目标文件夹已存在时的处理方式（设计文档第 7 章）。</summary>
public enum OutputConflictPolicy
{
    /// <summary>取消操作，不做任何写入。</summary>
    Cancel,

    /// <summary>覆盖本次任务会生成的那些文件（不影响其他文件）。</summary>
    OverwriteExisting,

    /// <summary>输出到新的编号目录，例如 <c>lesson (1)</c>。</summary>
    CreateNumberedFolder,
}

/// <summary>一次分割任务的输入。</summary>
public sealed record SplitRequest
{
    public required string SourcePath { get; init; }

    public SplitMode Mode { get; init; } = SplitMode.ByDuration;

    public TimeSpan SegmentLength { get; init; } = TimeSpan.FromMinutes(1);

    public int SegmentCount { get; init; } = 3;

    public AudioFormat Format { get; init; } = AudioFormat.Mp3;

    public OutputConflictPolicy ConflictPolicy { get; init; } = OutputConflictPolicy.Cancel;

    /// <summary>显式指定输出目录；为空则按默认规则推导。</summary>
    public string? OutputDirectory { get; init; }
}

/// <summary>进度回报：0..1 的比例 + 面向用户的文字。</summary>
public sealed record SplitProgress(double Fraction, string Status);

/// <summary>一次分割任务的结果。</summary>
public sealed record SplitResult
{
    public bool Success { get; init; }

    public bool Canceled { get; init; }

    /// <summary>实际写入的输出目录（成功后必有值）。</summary>
    public string? OutputDirectory { get; init; }

    public IReadOnlyList<string> Files { get; init; } = [];

    public string? Message { get; init; }

    /// <summary>清理失败时保留的临时目录，需要提示用户。</summary>
    public string? LeftoverTempDirectory { get; init; }

    /// <summary>目标目录已存在且等待用户决策。</summary>
    public bool NeedsConflictDecision { get; init; }

    public static SplitResult Ok(string outputDirectory, IReadOnlyList<string> files) => new()
    {
        Success = true,
        OutputDirectory = outputDirectory,
        Files = files,
    };

    public static SplitResult Fail(string message) => new() { Message = message };

    public static SplitResult CanceledResult(string? message = null) => new()
    {
        Canceled = true,
        Message = message ?? "操作已取消。",
    };

    public static SplitResult Conflict(string outputDirectory) => new()
    {
        NeedsConflictDecision = true,
        OutputDirectory = outputDirectory,
        Message = "目标文件夹已存在，需要确认处理方式。",
    };
}
