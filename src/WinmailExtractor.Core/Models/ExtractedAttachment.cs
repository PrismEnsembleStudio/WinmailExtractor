namespace WinmailExtractor.Core.Models;

public sealed record ExtractedAttachment(string OriginalName, string FileName, byte[] Content)
{
    public long Size => Content.LongLength;
    public bool NameChanged => OriginalName != FileName;
    public bool RequiresCaution => FileNamePolicy.RequiresCaution(FileName);
}

public sealed record ExtractionResult(IReadOnlyList<ExtractedAttachment> Attachments, IReadOnlyList<string> Warnings);

public sealed class ExtractionException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record ExtractionLimits(
    int MaxInputBytes = 50 * 1024 * 1024,
    int MaxAttachments = 256,
    long MaxTotalBytes = 100 * 1024 * 1024,
    int MaxAttachmentBytes = 50 * 1024 * 1024)
{
    public const int MaxAttributes = 8192;
    public const int MaxProperties = 8192;
    public const int MaxNameBytes = 64 * 1024;
}
