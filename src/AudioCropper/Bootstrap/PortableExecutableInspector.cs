using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace AudioCropper.Bootstrap;

/// <summary>
/// 读取 PE 文件的机器类型。用于确认"下载的文件确实是目标架构"，
/// 满足设计文档 10.1"不能仅通过更改文件名把 x64 伪装成 ARM64"。
/// </summary>
public static class PortableExecutableInspector
{
    private const int PeHeaderOffsetPosition = 0x3C;
    private const uint PeSignature = 0x0000_4550;

    public static Architecture? TryReadArchitecture(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);

            if (stream.Length < PeHeaderOffsetPosition + 4)
            {
                return null;
            }

            stream.Position = PeHeaderOffsetPosition;
            var peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset + 6 > stream.Length)
            {
                return null;
            }

            stream.Position = peOffset;
            if (reader.ReadUInt32() != PeSignature)
            {
                return null;
            }

            return reader.ReadUInt16() switch
            {
                0x8664 => Architecture.X64,
                0xAA64 => Architecture.Arm64,
                0x014C => Architecture.X86,
                _ => (Architecture?)null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
