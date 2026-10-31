namespace Hmis.BuildingBlocks.Application.Files;

public sealed record DetectedFileType(string ContentType, string Extension);

/// <summary>
/// Identifies uploads from their leading bytes ("magic numbers"), never from the file name or the client's
/// Content-Type, so a renamed script or HTML file cannot be stored as an image or PDF.
/// </summary>
public static class FileSignatures
{
    public static DetectedFileType? DetectImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return new DetectedFileType("image/png", "png");
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return new DetectedFileType("image/jpeg", "jpg");
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return new DetectedFileType("image/webp", "webp");
        }

        return null;
    }

    public static DetectedFileType? DetectPdf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8) ? new DetectedFileType("application/pdf", "pdf") : null;
}
