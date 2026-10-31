using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AudioCropper.Audio;

/// <summary>FFmpeg 一次执行的结果。</summary>
public sealed record FfmpegRunResult(int ExitCode, string StandardOutput, string StandardErrorTail)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// 以参数列表方式启动 FFmpeg（绝不拼接命令行字符串，避免文件名注入，
/// 见设计文档第 11 章"FFmpeg 调用参数安全处理"）。
/// </summary>
public static class FfmpegRunner
{
    public static async Task<FfmpegRunResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Action<string>? onStandardOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        var outputBuffer = new StringBuilder();
        var errorBuffer = new StringBuilder();
        var errorGate = new object();

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            lock (errorGate)
            {
                if (errorBuffer.Length < 32_768)
                {
                    errorBuffer.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        try
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (outputBuffer.Length < 65_536)
                {
                    outputBuffer.AppendLine(line);
                }

                onStandardOutputLine?.Invoke(line);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        lock (errorGate)
        {
            return new FfmpegRunResult(process.ExitCode, outputBuffer.ToString().Trim(), errorBuffer.ToString().Trim());
        }
    }

    /// <summary>把 -progress 输出行转成秒数；无法识别时返回 null。</summary>
    public static double? ParseProgressSeconds(string line)
    {
        const string microsecondsKey = "out_time_us=";
        const string millisecondsKey = "out_time_ms=";

        var value = line.StartsWith(microsecondsKey, StringComparison.Ordinal)
            ? line[microsecondsKey.Length..]
            : line.StartsWith(millisecondsKey, StringComparison.Ordinal)
                ? line[millisecondsKey.Length..]
                : null;

        if (value is null
            || !long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds)
            || microseconds < 0)
        {
            return null;
        }

        // ffmpeg 的 out_time_us / out_time_ms 都表示微秒。
        return microseconds / 1_000_000d;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 进程已退出或无权结束，忽略。
        }
    }
}
