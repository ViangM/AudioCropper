using System.Globalization;
using AudioCropper.Safety;

namespace AudioCropper.Audio;

/// <summary>
/// 音频分割的完整流程（设计文档第 3、7 章）：
/// 读取媒体信息 → 在目标目录附近建立临时工作目录 → 生成片段 → 校验数量/大小/可读性
/// → 提交到最终目录（带备份回滚）→ 清理临时文件。
/// 界面只负责收集参数与展示结果，不参与 FFmpeg 参数拼接和文件提交逻辑。
/// </summary>
public sealed class AudioSplitService
{
    /// <summary>短于该时长的分段意义不大，直接拒绝。</summary>
    private const double MinimumSegmentSeconds = 0.2;

    private readonly IFfmpegLocator _locator;
    private readonly MediaProbe _probe;

    public AudioSplitService(IFfmpegLocator? locator = null, MediaProbe? probe = null)
    {
        _locator = locator ?? new FfmpegLocator();
        _probe = probe ?? new MediaProbe();
    }

    public async Task<SplitResult> SplitAsync(
        SplitRequest request,
        IProgress<SplitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourcePath))
        {
            return SplitResult.Fail("源文件不存在或已被移动。");
        }

        var sourcePath = Path.GetFullPath(request.SourcePath);
        var format = request.Format;

        string outputDirectory;
        try
        {
            outputDirectory = Path.GetFullPath(request.OutputDirectory is { Length: > 0 } custom
                ? custom
                : OutputLayout.GetDefaultOutputDirectory(sourcePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return SplitResult.Fail($"输出路径无效：{ex.Message}");
        }

        if (File.Exists(outputDirectory))
        {
            return SplitResult.Fail($"输出位置已被同名文件占用：{outputDirectory}");
        }

        if (Directory.Exists(outputDirectory))
        {
            switch (request.ConflictPolicy)
            {
                case OutputConflictPolicy.Cancel:
                    return SplitResult.Conflict(outputDirectory);
                case OutputConflictPolicy.CreateNumberedFolder:
                    outputDirectory = OutputLayout.CreateNumberedVariant(outputDirectory);
                    break;
            }
        }

        FfmpegToolset tools;
        try
        {
            tools = _locator.Resolve();
        }
        catch (FfmpegNotFoundException ex)
        {
            return SplitResult.Fail(ex.Message);
        }

        progress?.Report(new SplitProgress(0, "正在读取音频信息…"));

        MediaInfo info;
        try
        {
            info = await _probe.ProbeAsync(tools, sourcePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return SplitResult.CanceledResult();
        }
        catch (FfmpegException ex)
        {
            return SplitResult.Fail(ex.Message);
        }

        var totalSeconds = info.Duration.TotalSeconds;
        var segmentSeconds = request.Mode == SplitMode.ByCount
            ? totalSeconds / Math.Max(1, request.SegmentCount)
            : request.SegmentLength.TotalSeconds;

        if (request.Mode == SplitMode.ByCount && request.SegmentCount < 1)
        {
            return SplitResult.Fail("片段数量必须至少为 1。");
        }

        if (segmentSeconds < MinimumSegmentSeconds)
        {
            return SplitResult.Fail($"分段长度过短（{segmentSeconds:0.##} 秒），请减少片段数量或增大分段时长。");
        }

        var expectedCount = request.Mode == SplitMode.ByCount
            ? request.SegmentCount
            : (int)Math.Ceiling(totalSeconds / segmentSeconds);

        var workspaceParent = Path.GetDirectoryName(outputDirectory) ?? Path.GetTempPath();
        TempWorkspace workspace;
        try
        {
            workspace = TempWorkspace.Create(workspaceParent, Path.GetFileName(outputDirectory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SplitResult.Fail($"无法创建临时工作目录：{ex.Message}");
        }

        var result = SplitResult.Fail("未执行任何操作。");
        var leftoverTempDirectory = (string?)null;

        try
        {
            try
            {
                var produced = request.Mode == SplitMode.ByCount
                    ? await ProduceByCountAsync(tools, sourcePath, workspace.Path, format, segmentSeconds, expectedCount, progress, cancellationToken).ConfigureAwait(false)
                    : await ProduceByDurationAsync(tools, sourcePath, workspace.Path, format, segmentSeconds, totalSeconds, progress, cancellationToken).ConfigureAwait(false);

                var validated = await ValidateAndRenameAsync(
                        tools, produced, workspace.Path, format, request.Mode, expectedCount, cancellationToken)
                    .ConfigureAwait(false);
                if (validated.Error is not null)
                {
                    result = SplitResult.Fail(validated.Error);
                }
                else
                {
                    progress?.Report(new SplitProgress(1, "正在写入输出目录…"));
                    result = Commit(validated.Files, workspace.Path, outputDirectory, request.ConflictPolicy);
                }
            }
            catch (OperationCanceledException)
            {
                result = SplitResult.CanceledResult();
            }
            catch (FfmpegException ex)
            {
                AppLogger.Error($"FFmpeg 执行失败：{sourcePath}", ex);
                result = SplitResult.Fail(ex.Message);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLogger.Error($"分割过程发生文件错误：{sourcePath}", ex);
                result = SplitResult.Fail($"文件操作失败：{ex.Message}");
            }
        }
        finally
        {
            workspace.Dispose();
            if (workspace.CleanupFailed)
            {
                leftoverTempDirectory = workspace.Path;
                AppLogger.Warn($"未能清理临时目录：{workspace.Path}");
            }
        }

        if (leftoverTempDirectory is not null)
        {
            result = result with { LeftoverTempDirectory = leftoverTempDirectory };
        }

        if (result.Success)
        {
            AppLogger.Info($"分割完成：{sourcePath} → {outputDirectory}（{result.Files.Count} 个片段）");
        }
        else if (!result.Canceled)
        {
            AppLogger.Warn($"分割失败：{sourcePath} → {outputDirectory}：{result.Message}");
        }

        return result;
    }

    /// <summary>
    /// 按时长分段：一次 ffmpeg 调用完成，速度快。
    /// 用 -f segment 会按时间边界切分并重新编码，输出数量可能比理论值多一个极短尾片。
    /// </summary>
    private static async Task<IReadOnlyList<string>> ProduceByDurationAsync(
        FfmpegToolset tools,
        string sourcePath,
        string workspace,
        AudioFormat format,
        double segmentSeconds,
        double totalSeconds,
        IProgress<SplitProgress>? progress,
        CancellationToken cancellationToken)
    {
        var pattern = Path.Combine(workspace, "seg-%05d" + format.Extension);

        var arguments = new List<string>
        {
            "-hide_banner", "-nostdin", "-y",
            "-i", sourcePath,
            "-vn", "-sn", "-dn", "-map", "0:a:0",
            "-f", "segment",
            "-segment_time", FormatSeconds(segmentSeconds),
            "-reset_timestamps", "1",
            "-progress", "pipe:1", "-nostats",
        };
        arguments.AddRange(format.EncoderArguments);
        arguments.Add(pattern);

        var run = await FfmpegRunner.RunAsync(
            tools.FfmpegPath,
            arguments,
            line => ReportTimeProgress(line, totalSeconds, 0, 1, progress),
            cancellationToken).ConfigureAwait(false);

        if (!run.Succeeded)
        {
            throw new FfmpegException(
                "音频分割失败。" + Summarize(run.StandardErrorTail),
                run.ExitCode,
                run.StandardErrorTail);
        }

        return Directory
            .EnumerateFiles(workspace, "seg-*" + format.Extension)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 按数量分段：逐个调用 ffmpeg，保证输出数量与用户要求一致。
    /// 代价是比单次 segment 调用慢，但片段边界和数量可预期。
    /// </summary>
    private static async Task<IReadOnlyList<string>> ProduceByCountAsync(
        FfmpegToolset tools,
        string sourcePath,
        string workspace,
        AudioFormat format,
        double segmentSeconds,
        int count,
        IProgress<SplitProgress>? progress,
        CancellationToken cancellationToken)
    {
        var files = new List<string>(count);

        for (var index = 0; index < count; index++)
        {
            var start = index * segmentSeconds;
            var length = Math.Min(segmentSeconds, Math.Max(0.01, count * segmentSeconds - start));
            var output = Path.Combine(workspace, $"seg-{index:D5}{format.Extension}");

            var arguments = new List<string>
            {
                "-hide_banner", "-nostdin", "-y",
                "-ss", FormatSeconds(start),
                "-t", FormatSeconds(length),
                "-i", sourcePath,
                "-vn", "-sn", "-dn", "-map", "0:a:0",
                "-progress", "pipe:1", "-nostats",
            };
            arguments.AddRange(format.EncoderArguments);
            arguments.Add(output);

            var run = await FfmpegRunner.RunAsync(
                tools.FfmpegPath,
                arguments,
                line => ReportTimeProgress(line, segmentSeconds, index, count, progress),
                cancellationToken).ConfigureAwait(false);

            if (!run.Succeeded)
            {
                throw new FfmpegException(
                    $"生成第 {index + 1}/{count} 个片段失败。" + Summarize(run.StandardErrorTail),
                    run.ExitCode,
                    run.StandardErrorTail);
            }

            files.Add(output);
        }

        progress?.Report(new SplitProgress(1, "片段生成完成。"));
        return files;
    }

    private static void ReportTimeProgress(
        string line,
        double segmentTotalSeconds,
        int completedSegments,
        int totalSegments,
        IProgress<SplitProgress>? progress)
    {
        if (progress is null || segmentTotalSeconds <= 0)
        {
            return;
        }

        var seconds = FfmpegRunner.ParseProgressSeconds(line);
        if (seconds is null)
        {
            return;
        }

        var within = Math.Clamp(seconds.Value / segmentTotalSeconds, 0, 1);
        var fraction = totalSegments <= 1
            ? within
            : (completedSegments + within) / totalSegments;

        progress.Report(new SplitProgress(
            Math.Clamp(fraction, 0, 1),
            $"正在生成片段 {completedSegments + 1}/{Math.Max(1, totalSegments)}…"));
    }

    /// <summary>
    /// 校验片段可读性并重命名为 0001.ext 形式。
    /// 时长分段可能产生极短的尾段：某些编码器（如 MP3）在这种长度下会生成
    /// 没有任何有效帧的文件，虽然字节数不为 0 却无法播放。
    /// 因此这里逐个探测，只丢弃"无法解码的末段"，其余情况一律报错。
    /// </summary>
    private async Task<(IReadOnlyList<string> Files, string? Error)> ValidateAndRenameAsync(
        FfmpegToolset tools,
        IReadOnlyList<string> produced,
        string workspace,
        AudioFormat format,
        SplitMode mode,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        if (produced.Count == 0)
        {
            return ([], "未生成任何音频片段，处理失败。");
        }

        if (mode == SplitMode.ByCount && produced.Count != expectedCount)
        {
            return ([], $"片段数量不符：预期 {expectedCount} 个，实际 {produced.Count} 个。");
        }

        var readable = new List<string>(produced.Count);
        for (var index = 0; index < produced.Count; index++)
        {
            var file = new FileInfo(produced[index]);
            if (!file.Exists)
            {
                return ([], $"第 {index + 1} 个片段缺失。");
            }

            var isLast = index == produced.Count - 1;

            if (file.Length == 0)
            {
                if (mode == SplitMode.ByDuration && isLast && readable.Count > 0)
                {
                    AppLogger.Warn($"丢弃空的末段：{file.Name}");
                    continue;
                }

                return ([], $"第 {index + 1} 个片段为空文件（0 字节），处理失败。");
            }

            if (!await IsDecodableAsync(tools, file.FullName, cancellationToken).ConfigureAwait(false))
            {
                if (mode == SplitMode.ByDuration && isLast && readable.Count > 0)
                {
                    // 尾段过短导致编码器没有写出有效帧，丢弃不影响可听内容。
                    AppLogger.Warn($"丢弃无法解码的末段：{file.Name}（{file.Length} 字节）");
                    continue;
                }

                return ([], $"第 {index + 1} 个片段无法读取，处理失败。");
            }

            readable.Add(file.FullName);
        }

        if (readable.Count == 0)
        {
            return ([], "所有片段都无法读取，处理失败。");
        }

        var renamed = new List<string>(readable.Count);
        for (var index = 0; index < readable.Count; index++)
        {
            var target = Path.Combine(workspace, $"{index + 1:D4}{format.Extension}");
            if (!string.Equals(readable[index], target, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(readable[index], target);
            }

            renamed.Add(target);
        }

        return (renamed, null);
    }

    /// <summary>用探测结果判断片段是否真的可解码（有音频轨且时长大于 0）。</summary>
    private async Task<bool> IsDecodableAsync(FfmpegToolset tools, string path, CancellationToken cancellationToken)
    {
        try
        {
            var info = await _probe.ProbeAsync(tools, path, cancellationToken).ConfigureAwait(false);
            return info.HasAudio;
        }
        catch (FfmpegException)
        {
            return false;
        }
    }

    /// <summary>
    /// 把片段从临时目录提交到最终目录。
    /// 只移动本次任务生成的文件；已存在的同名文件先备份，失败时回滚，
    /// 绝不删除或清空其他文件。
    /// </summary>
    private static SplitResult Commit(
        IReadOnlyList<string> files,
        string workspace,
        string outputDirectory,
        OutputConflictPolicy policy)
    {
        var created = new List<string>(files.Count);
        var backups = new List<(string Backup, string Destination)>();
        var backupDirectory = Path.Combine(workspace, "_backup");

        try
        {
            Directory.CreateDirectory(outputDirectory);

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var destination = Path.Combine(outputDirectory, name);

                if (File.Exists(destination))
                {
                    if (policy != OutputConflictPolicy.OverwriteExisting)
                    {
                        throw new IOException($"目标文件已存在：{destination}");
                    }

                    Directory.CreateDirectory(backupDirectory);
                    var backup = Path.Combine(backupDirectory, $"{name}.{Guid.NewGuid():N}.bak");
                    File.Move(destination, backup);
                    backups.Add((backup, destination));
                }

                File.Move(file, destination);
                created.Add(destination);
            }

            foreach (var (backup, _) in backups)
            {
                TryDeleteFile(backup);
            }

            return SplitResult.Ok(outputDirectory, created);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 回滚：删除本次写入的文件，恢复被覆盖的旧文件。
            foreach (var path in created)
            {
                TryDeleteFile(path);
            }

            foreach (var (backup, destination) in backups)
            {
                try
                {
                    if (File.Exists(backup))
                    {
                        File.Move(backup, destination, overwrite: true);
                    }
                }
                catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException)
                {
                    AppLogger.Error($"恢复旧文件失败：{destination}", restoreError);
                }
            }

            return SplitResult.Fail($"写入输出目录失败：{ex.Message}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Warn($"删除文件失败：{path} — {ex.Message}");
        }
    }

    private static string FormatSeconds(double seconds) =>
        seconds.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>取 FFmpeg 错误输出的最后几行，避免把整段日志弹给用户。</summary>
    private static string Summarize(string standardError)
    {
        if (string.IsNullOrWhiteSpace(standardError))
        {
            return string.Empty;
        }

        var lines = standardError
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(3);

        return Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}
