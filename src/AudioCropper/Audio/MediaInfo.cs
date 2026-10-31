namespace AudioCropper.Audio;

/// <summary>媒体基本信息（设计文档第 3 章"媒体信息读取"）。</summary>
public sealed record MediaInfo(
    TimeSpan Duration,
    string? CodecName = null,
    int? SampleRate = null,
    int? Channels = null)
{
    public bool HasAudio => Duration > TimeSpan.Zero;
}
