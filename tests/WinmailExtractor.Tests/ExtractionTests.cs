using System.Security.Cryptography;
using System.Text;
using MimeKit;
using MimeKit.Tnef;
using WinmailExtractor.Core.Models;
using WinmailExtractor.Core.Services;

namespace WinmailExtractor.Tests;

public sealed class ExtractionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SyntheticTnefExtractsExactBytesAndNames(bool mapi)
    {
        using var fixture = new TnefFixture();
        string[] names = ["見積書 2026.pdf", "受注明細.docx", "集計.xlsx"];
        byte[][] payloads = [Encoding.UTF8.GetBytes("%PDF-1.7 synthetic"), [0x50, 0x4b, 0, 255, 1], new byte[2048]];
        for (int i = 0; i < names.Length; i++) fixture.Add(names[i], payloads[i], mapi);
        byte[] input = fixture.ToArray();
        var result = new TnefExtractionService().Extract(input);
        Assert.Equal(3, result.Attachments.Count);
        Assert.Empty(result.Warnings);
        for (int i = 0; i < names.Length; i++)
        {
            Assert.Equal(names[i], result.Attachments[i].FileName);
            Assert.Equal(SHA256.HashData(payloads[i]), SHA256.HashData(result.Attachments[i].Content));
        }
        // Cross-check synthetic input against the upstream high-level parser as well.
        using var part = new TnefPart { Content = new MimeContent(new MemoryStream(input)) };
        int count = 0;
        foreach (var entity in part.ExtractAttachments())
        {
            using (entity)
            {
                var file = Assert.IsAssignableFrom<MimePart>(entity);
                using var decoded = new MemoryStream();
                Assert.NotNull(file.Content);
                file.Content.DecodeTo(decoded);
                Assert.Equal(payloads[count++], decoded.ToArray());
            }
        }
        Assert.Equal(3, count);
    }

    [Fact] public void EmptyTnefHasNoAttachments()
    {
        using var fixture = new TnefFixture();
        Assert.Empty(new TnefExtractionService().Extract(fixture.ToArray()).Attachments);
    }

    [Theory]
    [InlineData(TnefPropertyType.String8)]
    [InlineData(TnefPropertyType.Unicode)]
    [InlineData(TnefPropertyType.Binary)]
    public void UnusedVariableMetadataBeforeNamedPropertyDoesNotRejectAttachment(TnefPropertyType type)
    {
        using var fixture = new TnefFixture();
        byte[] payload = Encoding.ASCII.GetBytes("%PDF-1.7 synthetic attachment");
        fixture.Add("document.pdf", payload, mapi: false);
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        writer.Write(4);
        writer.Write(((uint)TnefPropertyId.AttachMethod << 16) | (uint)TnefPropertyType.Long);
        writer.Write(1);
        TnefFixture.WriteVariable(writer, TnefPropertyId.DisplayName, type, [1, 2, 3, 0]);
        // A named MAPI property sets the high bit of the following tag. No real mail data.
        writer.Write(0x812d000bu); // named Boolean property
        writer.Write(new Guid("00020329-0000-0000-c000-000000000046").ToByteArray());
        writer.Write(0); // numeric named-property identifier
        writer.Write(0x1234);
        writer.Write(0); // Boolean value, padded to four bytes
        TnefFixture.WriteVariable(writer, TnefPropertyId.AttachLongFilename, TnefPropertyType.Unicode,
            Encoding.Unicode.GetBytes("document.pdf\0"));
        fixture.Attribute(TnefAttributeLevel.Attachment, TnefAttributeTag.Attachment, buffer.ToArray());

        var result = new TnefExtractionService().Extract(fixture.ToArray());
        var attachment = Assert.Single(result.Attachments);
        Assert.Equal("document.pdf", attachment.FileName);
        Assert.Equal(payload, attachment.Content);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void InvalidUnusedMetadataLengthIsStillRejected()
    {
        using var fixture = new TnefFixture();
        fixture.Add("document.pdf", [1, 2, 3]);
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        writer.Write(1);
        writer.Write(((uint)TnefPropertyId.DisplayName << 16) | (uint)TnefPropertyType.Binary);
        writer.Write(1);
        writer.Write(int.MaxValue);
        fixture.Attribute(TnefAttributeLevel.Attachment, TnefAttributeTag.Attachment, buffer.ToArray());
        Assert.Throws<ExtractionException>(() => new TnefExtractionService().Extract(fixture.ToArray()));
    }
    [Fact] public void NamelessAndEmptyFileArePreserved()
    {
        using var fixture = new TnefFixture();
        fixture.Add(null, []);
        var file = Assert.Single(new TnefExtractionService().Extract(fixture.ToArray()).Attachments);
        Assert.Equal("attachment-001.bin", file.FileName);
        Assert.Empty(file.Content);
    }
    [Fact] public void UnsupportedEmbeddedMessageHasReason()
    {
        using var fixture = new TnefFixture();
        fixture.Add("mail.msg", new byte[32], method: 5);
        var result = new TnefExtractionService().Extract(fixture.ToArray());
        Assert.Empty(result.Attachments);
        Assert.Contains("未対応", Assert.Single(result.Warnings));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(5)] [InlineData(9)] [InlineData(20)]
    public void TruncationIsRejected(int cut)
    {
        using var fixture = new TnefFixture();
        fixture.Add("a.pdf", new byte[20]);
        byte[] bytes = fixture.ToArray();
        bytes = cut == 0 ? [] : bytes[..^cut];
        Assert.Throws<ExtractionException>(() => new TnefExtractionService().Extract(bytes));
    }
    [Fact] public void ChecksumFailureIsRejected()
    {
        using var fixture = new TnefFixture(); fixture.Add("x", [1, 2, 3]);
        var bytes = fixture.ToArray(); bytes[^1] ^= 0xff;
        Assert.Throws<ExtractionException>(() => new TnefExtractionService().Extract(bytes));
    }
    [Fact] public void NonTnefIsRejected() => Assert.Throws<ExtractionException>(() => new TnefExtractionService().Extract(Encoding.UTF8.GetBytes("plain .dat content")));
    [Fact] public void AllResourceLimitsAreEnforced()
    {
        using var fixture = new TnefFixture(); fixture.Add("a", new byte[16]); fixture.Add("b", new byte[16]);
        byte[] bytes = fixture.ToArray();
        foreach (var limits in new[] { new ExtractionLimits(MaxInputBytes: 16), new ExtractionLimits(MaxAttachments: 1), new ExtractionLimits(MaxTotalBytes: 24), new ExtractionLimits(MaxAttachmentBytes: 8) })
            Assert.Throws<ExtractionException>(() => new TnefExtractionService(limits).Extract(bytes));
    }
    [Fact] public void InvalidLengthIsRejectedBeforeAllocation()
    {
        using var fixture = new TnefFixture();
        byte[] bytes = fixture.ToArray();
        Array.Copy(BitConverter.GetBytes(uint.MaxValue), 0, bytes, 11, 4);
        Assert.Throws<ExtractionException>(() => new TnefExtractionService().Extract(bytes));
    }
    [Fact] public void BodyIsNeverTreatedAsAnAttachmentOrDecompressed()
    {
        using var fixture = new TnefFixture();
        fixture.Attribute(TnefAttributeLevel.Message, TnefAttributeTag.Body, Encoding.ASCII.GetBytes("body\0"));
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        writer.Write(1);
        TnefFixture.WriteVariable(writer, TnefPropertyId.RtfCompressed, TnefPropertyType.Binary, [0xff, 0xff, 0xff, 0x7f]);
        fixture.Attribute(TnefAttributeLevel.Message, TnefAttributeTag.MapiProperties, buffer.ToArray());
        Assert.Empty(new TnefExtractionService().Extract(fixture.ToArray()).Attachments);
    }
    [Fact] public void CancellationIsHonored()
    {
        using var fixture = new TnefFixture();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new TnefExtractionService().Extract(fixture.ToArray(), cancellation.Token));
    }
    [Fact] public void RandomMalformedInputsHaveControlledErrors()
    {
        var random = new Random(1122);
        for (int i = 0; i < 500; i++)
        {
            var bytes = new byte[random.Next(6, 1024)]; random.NextBytes(bytes);
            if (i % 2 == 0) Array.Copy(BitConverter.GetBytes(0x223e9f78u), bytes, 4);
            Assert.Throws<ExtractionException>(() => new TnefExtractionService().Extract(bytes));
        }
    }
}
