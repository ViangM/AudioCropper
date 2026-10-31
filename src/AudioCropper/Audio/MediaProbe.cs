using System.Globalization;
using System.Text.RegularExpressions;

namespace AudioCropper.Audio;

/// <summary>
/// 读取媒体时长与音频参数。优先 ffprobe；发布包未带 ffprobe 时退回解析
/// <c>ffmpeg -i</c> 的输出，保证单文件分发只需要 ffmpeg。
/// </summary>
public sealed partial class MediaProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(60);

    public async Task<MediaInfo> ProbeAsync(FfmpegToolset tools, string sourcePath, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        return tools.FfprobePath is not null
            ? await ProbeWithFfprobeAsync(tools.FfprobePath, sourcePath, timeout.Token).ConfigureAwait(false)
            : await ProbeWithFfmpegAsync(tools.FfmpegPath, sourcePath, timeout.Token).ConfigureAwait(false);
    }

    private static async Task<MediaInfo> ProbeWithFfprobeAsync(string ffprobePath, string sourcePath, CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "-v", "error",
            "-select_streams", "a:0",
            "-show_entries", "format=duration:stream=codec_name,sample_rate,channels",
            "-of", "default=noprint_wrappers=1",
            sourcePath,
        ];

        var result = await FfmpegRunner.RunAsync(ffprobePath, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new FfmpegException("无法读取音频信息。该文件可能损坏或不是受支持的音频格式。", result.ExitCode, result.StandardErrorTail);
        }

        var values = ParseKeyValueLines(result.StandardOutput);
        return BuildInfo(values);
    }

    private static async Task<MediaInfo> ProbeWithFfmpegAsync(string ffmpegPath, string sourcePath, CancellationToken cancellationToken)
    {
        // ffmpeg -i 在没有输出时以非零码退出，这是正常行为，不作为失败。
        string[] arguments = ["-hide_banner", "-nostdin", "-i", sourcePath];
        var result = await FfmpegRunner.RunAsync(ffmpegPath, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        var text = result.StandardErrorTail;

        var durationMatch = DurationRegex().Match(text);
        if (!durationMatch.Success)
        {
            throw new FfmpegException("无法从文件中读取到音频时长。该文件可能不含音频轨或格式不受支持。", result.ExitCode, text);
        }

        var hours = int.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(durationMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        var seconds = double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        var duration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);

        var codecMatch = CodecRegex().Match(text);
        return new MediaInfo(duration, codecMatch.Success ? codecMatch.Groups[1].Value.Trim() : null);
    }

    private static Dictionary<string, string> ParseKeyValueLines(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
        }

        return values;
    }

    private static MediaInfo BuildInfo(Dictionary<string, string> values)
    {
        var duration = TimeSpan.Zero;
        if (values.TryGetValue("duration", out var rawDuration)
            && double.TryParse(rawDuration, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && seconds > 0)
        {
            duration = TimeSpan.FromSeconds(seconds);
        }
        else
        {
            throw new FfmpegException(
                "无法从文件中读取到音频时长。该文件可能不含音频轨或格式不受支持。", 0, string.Join('\n', values));
        }

        int? sampleRate = values.TryGetValue("sample_rate", out var rawRate)
                           && int.TryParse(rawRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rate)
            ? rate
            : null;

        int? channels = values.TryGetValue("channels", out var rawChannels)
                        && int.TryParse(rawChannels, NumberStyles.Integer, CultureInfo.InvariantCulture, out var channelCount)
            ? channelCount
            : null;

        return new MediaInfo(
            duration,
            values.TryGetValue("codec_name", out var codec) ? codec : null,
            sampleRate,
            channels);
    }

    [GeneratedRegex(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"Audio:\s*([A-Za-z0-9_\-\.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CodecRegex();
}
