using System.Buffers.Binary;
using System.Text;
using MimeKit.Tnef;
using WinmailExtractor.Core.Models;

namespace WinmailExtractor.Core.Services;

public sealed class TnefExtractionService(ExtractionLimits? limits = null)
{
    private readonly ExtractionLimits limits = limits ?? new();

    public ExtractionResult Extract(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > limits.MaxInputBytes)
                throw new ExtractionException($"ファイルが読み込み上限（{limits.MaxInputBytes / 1024 / 1024} MiB）を超えています。");
            byte[] bytes = new byte[checked((int)input.Length)];
            input.ReadExactly(bytes);
            return Extract(bytes, cancellationToken);
        }
        catch (ExtractionException) { throw; }
        catch (UnauthorizedAccessException ex) { throw new ExtractionException("このファイルを読み取る権限がありません。", ex); }
        catch (IOException ex) { throw new ExtractionException("ファイルを読み取れません。存在することと、他のアプリで使用中でないことを確認してください。", ex); }
        catch (ArgumentException ex) { throw new ExtractionException("ファイルのパスが正しくありません。", ex); }
    }

    public ExtractionResult Extract(byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes.Length > limits.MaxInputBytes) throw new ExtractionException("ファイルが読み込みサイズの上限を超えています。");
        ValidateEnvelope(bytes, cancellationToken);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var attachments = new List<ExtractedAttachment>();
        var warnings = new List<string>();
        Pending? current = null;
        int count = 0;
        long total = 0;
        try
        {
            using var stream = new MemoryStream(bytes, false);
            using var reader = new TnefReader(stream, 0, TnefComplianceMode.Strict);
            while (reader.ReadNextAttribute())
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Deliberately skip mail bodies and compressed RTF. No body rendering or decompression.
                if (reader.AttributeLevel != TnefAttributeLevel.Attachment) continue;
                var prop = reader.TnefPropertyReader;
                switch (reader.AttributeTag)
                {
                    case TnefAttributeTag.AttachRenderData:
                        Complete();
                        if (++count > limits.MaxAttachments) throw new ExtractionException($"添付件数が上限（{limits.MaxAttachments}件）を超えています。");
                        current = new Pending();
                        break;
                    case TnefAttributeTag.AttachTitle when current != null:
                        current.Name ??= ReadName(prop);
                        break;
                    case TnefAttributeTag.AttachData when current != null:
                        current.Data = ReadData(prop);
                        break;
                    case TnefAttributeTag.Attachment when current != null:
                        if (prop.PropertyCount > ExtractionLimits.MaxProperties)
                            throw new ExtractionException("添付のプロパティ数が多すぎます。");
                        int properties = 0;
                        while (prop.ReadNextProperty())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (++properties > ExtractionLimits.MaxProperties) throw new ExtractionException("添付のプロパティ数が多すぎます。");
                            if (prop.ValueCount > 256)
                                throw new ExtractionException("添付のプロパティ値の件数が多すぎます。");
                            switch (prop.PropertyTag.Id)
                            {
                                case TnefPropertyId.AttachLongFilename:
                                    current.Name = ReadName(prop);
                                    break;
                                case TnefPropertyId.AttachFilename:
                                    string shortName = ReadName(prop);
                                    current.Name ??= shortName;
                                    break;
                                case TnefPropertyId.AttachMethod:
                                    current.Method = (TnefAttachMethod)prop.ReadValueAsInt32();
                                    break;
                                case TnefPropertyId.AttachData:
                                    if (prop.PropertyTag.ValueTnefType != TnefPropertyType.Binary || prop.ValueCount != 1)
                                    {
                                        current.UnsupportedData = true;
                                        SkipUnusedSingleValue(prop);
                                    }
                                    else
                                        current.Data = ReadData(prop);
                                    break;
                                default:
                                    SkipUnusedSingleValue(prop);
                                    break;
                            }
                        }
                        if (properties != prop.PropertyCount)
                            throw new ExtractionException("添付のプロパティ情報が途中で切れています。");
                        break;
                    default:
                        if (current == null && (reader.AttributeTag == TnefAttributeTag.Attachment || reader.AttributeTag == TnefAttributeTag.AttachData))
                            throw new ExtractionException("添付の開始情報が欠けています。破損したTNEFの可能性があります。");
                        break;
                }
            }
            Complete();
            return new(attachments, warnings);
        }
        catch (ExtractionException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new ExtractionException("TNEFの添付情報を読み取れませんでした。ファイルが破損しているか、このアプリが未対応の形式です。", ex);
        }

        string ReadName(TnefPropertyReader property)
        {
            if (property.RawValueLength < 0 || property.RawValueLength > ExtractionLimits.MaxNameBytes)
                throw new ExtractionException("添付ファイル名のデータが長すぎるか、不正です。");
            return property.ReadValueAsString();
        }

        byte[] ReadData(TnefPropertyReader property)
        {
            if (property.RawValueLength < 0 || property.RawValueLength > limits.MaxAttachmentBytes)
                throw new ExtractionException("添付ファイルが1件あたりのサイズ上限を超えています。");
            // Count all encountered data, including overwritten/repeated properties, before allocating.
            total += property.RawValueLength;
            if (total > limits.MaxTotalBytes) throw new ExtractionException("添付ファイルの合計展開サイズが上限を超えています。");
            return property.ReadValueAsBytes();
        }

        void SkipUnusedSingleValue(TnefPropertyReader property)
        {
            if (property.ValueCount != 1) return;
            // Consume ignored values explicitly. MimeKit 4.18.1's implicit skip can
            // mistake a following named-property tag for a variable-value length.
            // A bounded raw stream avoids decoding/allocating unrelated metadata
            // and retains strict length, checksum and property validation.
            using var value = property.GetRawValueReadStream();
            Span<byte> buffer = stackalloc byte[4096];
            while (value.Read(buffer) > 0) cancellationToken.ThrowIfCancellationRequested();
        }

        void Complete()
        {
            if (current == null) return;
            if (current.Method != TnefAttachMethod.ByValue || current.UnsupportedData)
                warnings.Add($"添付{count}: 埋め込みメール・OLE・参照形式（方式 {(int)current.Method}）は未対応のため保存候補から除外しました。");
            else if (current.Data == null)
                warnings.Add($"添付{count}: 通常のファイルデータがないため保存候補から除外しました。");
            else
            {
                string name = FileNamePolicy.Sanitize(current.Name, count);
                attachments.Add(new(current.Name ?? "", name, current.Data));
            }
            current = null;
        }
    }

    private static void ValidateEnvelope(byte[] data, CancellationToken cancellationToken)
    {
        if (data.Length < 6 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 0x223e9f78)
            throw new ExtractionException("TNEF（winmail.dat）形式のファイルではありません。拡張子だけを変更しても読み込めません。");
        int position = 6;
        int attributes = 0;
        while (position < data.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++attributes > ExtractionLimits.MaxAttributes) throw new ExtractionException("TNEFの属性数が上限を超えています。");
            if (data.Length - position < 11) throw new ExtractionException("TNEFが途中で切れています。ファイルをもう一度取得してください。");
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 5));
            if (length > data.Length - position - 11) throw new ExtractionException("TNEFのデータ長が不正です。破損したファイルの可能性があります。");
            position += checked(11 + (int)length);
        }
    }

    private sealed class Pending
    {
        public string? Name;
        public byte[]? Data;
        public TnefAttachMethod Method = TnefAttachMethod.ByValue;
        public bool UnsupportedData;
    }
}
