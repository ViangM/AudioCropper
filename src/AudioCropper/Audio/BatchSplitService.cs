namespace AudioCropper.Audio;

/// <summary>批量任务中目标文件夹已存在时的统一处理方式。</summary>
public enum BatchConflictPolicy
{
    /// <summary>覆盖本次会生成的同名文件。</summary>
    OverwriteExisting,

    /// <summary>输出到新的编号目录（每个文件各自判断）。</summary>
    CreateNumberedFolder,

    /// <summary>跳过已有输出目录的文件，不做处理。</summary>
    SkipExisting,
}

/// <summary>批量进度：0..1 的整体比例 + 当前文件说明。</summary>
public sealed record BatchProgress(double Fraction, string Status);

/// <summary>单个文件的处理结果。</summary>
public sealed record BatchFileResult(
    string SourcePath,
    int Index,
    bool Success,
    bool Skipped,
    int FileCount,
    string? OutputDirectory,
    string? Message)
{
    public string StatusText => Skipped ? "已跳过" : Success ? $"完成（{FileCount} 段）" : "失败";
}

/// <summary>整批任务的结果汇总。</summary>
public sealed record BatchResult(IReadOnlyList<BatchFileResult> Items, bool Canceled)
{
    public int SucceededCount => Items.Count(item => item.Success);

    public int SkippedCount => Items.Count(item => item.Skipped);

    public int FailedCount => Items.Count(item => !item.Success && !item.Skipped);
}

/// <summary>
/// 批量分割：对每个源文件独立调用 <see cref="AudioSplitService"/>，
/// 每个文件的输出仍然落在"源文件同目录 / 源文件同名文件夹"，互不影响。
/// 单个文件失败不会中断整批，除非用户取消。
/// </summary>
public sealed class BatchSplitService
{
    private readonly AudioSplitService _splitter;

    public BatchSplitService(AudioSplitService? splitter = null)
    {
        _splitter = splitter ?? new AudioSplitService();
    }

    public async Task<BatchResult> RunAsync(
        IReadOnlyList<string> sources,
        SplitRequest template,
        BatchConflictPolicy policy,
        IProgress<BatchProgress>? progress = null,
        IProgress<BatchFileResult>? fileCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BatchFileResult>(sources.Count);
        var canceled = false;

        for (var index = 0; index < sources.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                break;
            }

            var source = sources[index];
            var name = Path.GetFileName(source);
            progress?.Report(new BatchProgress(
                (double)index / sources.Count,
                $"正在处理 {index + 1}/{sources.Count}：{name}"));

            if (policy == BatchConflictPolicy.SkipExisting && ExistingOutputDirectory(source) is { } existing)
            {
                var skipped = new BatchFileResult(source, index, false, true, 0, existing, "目标文件夹已存在，已跳过");
                results.Add(skipped);
                fileCompleted?.Report(skipped);
                continue;
            }

            var conflictPolicy = policy == BatchConflictPolicy.OverwriteExisting
                ? OutputConflictPolicy.OverwriteExisting
                : OutputConflictPolicy.CreateNumberedFolder;

            var request = template with
            {
                SourcePath = source,
                ConflictPolicy = conflictPolicy,
            };

            SplitResult result;
            try
            {
                result = await _splitter
                    .SplitAsync(request, new ScaledProgress(progress, index, sources.Count, name), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
                break;
            }

            var item = new BatchFileResult(
                source,
                index,
                result.Success,
                Skipped: false,
                result.Files.Count,
                result.OutputDirectory,
                result.Message);

            results.Add(item);
            fileCompleted?.Report(item);

            if (result.Canceled)
            {
                canceled = true;
                break;
            }
        }

        if (!canceled)
        {
            progress?.Report(new BatchProgress(1, "批量处理完成。"));
        }

        return new BatchResult(results, canceled);
    }

    private static string? ExistingOutputDirectory(string source)
    {
        try
        {
            var directory = OutputLayout.GetDefaultOutputDirectory(source);
            return Directory.Exists(directory) ? directory : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>把单个文件的进度折算到整批进度上。</summary>
    private sealed class ScaledProgress : IProgress<SplitProgress>
    {
        private readonly IProgress<BatchProgress>? _outer;
        private readonly int _index;
        private readonly int _total;
        private readonly string _name;

        public ScaledProgress(IProgress<BatchProgress>? outer, int index, int total, string name)
        {
            _outer = outer;
            _index = index;
            _total = total;
            _name = name;
        }

        public void Report(SplitProgress value)
        {
            if (_outer is null || _total <= 0)
            {
                return;
            }

            var fraction = Math.Clamp((_index + Math.Clamp(value.Fraction, 0, 1)) / _total, 0, 1);
            _outer.Report(new BatchProgress(fraction, $"{_name}：{value.Status}"));
        }
    }
}
