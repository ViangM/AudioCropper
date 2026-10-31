namespace AudioCropper.Audio;

/// <summary>找不到可用的 FFmpeg 组件时抛出，属于可预期的环境错误。</summary>
public sealed class FfmpegNotFoundException : Exception
{
    public FfmpegNotFoundException(string message) : base(message)
    {
    }

    public FfmpegNotFoundException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>FFmpeg 执行失败（非零退出码）。</summary>
public sealed class FfmpegException : Exception
{
    public FfmpegException(string message, int exitCode, string standardError)
        : base(message)
    {
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    /// <summary>FFmpeg 的标准错误输出末尾片段，便于定位问题。</summary>
    public string StandardError { get; }
}
