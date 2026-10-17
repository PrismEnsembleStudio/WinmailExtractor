using WinmailExtractor.Core.Models;

namespace WinmailExtractor.ViewModels;

public sealed class AttachmentViewModel(ExtractedAttachment attachment) : ObservableObject
{
    public ExtractedAttachment Attachment { get; } = attachment;
    private bool selected = true;
    public bool Selected { get => selected; set { selected = value; Changed(); } }
    public string FileName => Attachment.FileName;
    public string Size => FormatSize(Attachment.Size);
    public string Kind => System.IO.Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant() is { Length: > 0 } ext ? ext : "FILE";
    public string Detail => Attachment.RequiresCaution ? "実行形式・スクリプト等：開く前に確認してください" : Attachment.NameChanged ? "安全なファイル名に調整しました" : "";
    public string Tooltip => Attachment.NameChanged ? $"元の名前: {Attachment.OriginalName}\n保存名: {FileName}" : FileName;
    public static string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1048576d:0.##} MiB" : bytes >= 1024 ? $"{bytes / 1024d:0.#} KiB" : $"{bytes:N0} B";
}
