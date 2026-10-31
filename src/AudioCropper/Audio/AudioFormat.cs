namespace AudioCropper.Audio;

/// <summary>
/// 一个可选的输出格式定义。
/// 注意（设计文档 4.2）：这里列出的是"预期支持"，不等于当前打包的 FFmpeg 构建
/// 实际具备的能力。发布前必须用 ffmpeg -encoders 实测，并只向界面暴露实测通过的项。
/// </summary>
public sealed record AudioFormat(
    string Id,
    string DisplayName,
    string Extension,
    IReadOnlyList<string> EncoderArguments)
{
    public static readonly AudioFormat Mp3 = new(
        "mp3", "MP3", ".mp3", ["-c:a", "libmp3lame", "-b:a", "192k"]);

    public static readonly AudioFormat M4a = new(
        "m4a", "M4A (AAC)", ".m4a", ["-c:a", "aac", "-b:a", "192k"]);

    public static readonly AudioFormat Wav = new(
        "wav", "WAV (PCM 16-bit)", ".wav", ["-c:a", "pcm_s16le"]);

    public static readonly AudioFormat Flac = new(
        "flac", "FLAC", ".flac", ["-c:a", "flac"]);

    public static readonly AudioFormat Ogg = new(
        "ogg", "OGG (Vorbis)", ".ogg", ["-c:a", "libvorbis", "-q:a", "5"]);

    public static readonly AudioFormat Opus = new(
        "opus", "Opus", ".opus", ["-c:a", "libopus", "-b:a", "96k"]);

    /// <summary>
    /// WebM：容器为 WebM，音频轨道使用 Opus。
    /// 输出扩展名 .webm 让 segment 复用器和普通输出都能正确选中 WebM 复用器。
    /// </summary>
    public static readonly AudioFormat Webm = new(
        "webm", "WebM (Opus)", ".webm", ["-c:a", "libopus", "-b:a", "128k"]);

    /// <summary>界面可选项。发布前应根据实测能力裁剪。</summary>
    public static IReadOnlyList<AudioFormat> All { get; } = [Mp3, M4a, Wav, Flac, Ogg, Opus, Webm];

    public override string ToString() => DisplayName;
}
