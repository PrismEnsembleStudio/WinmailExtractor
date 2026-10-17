using System.Text;
using System.IO;
using MimeKit.Tnef;

namespace WinmailExtractor.Tests;

// Generated, non-personal test data. No business mail is used.
public sealed class TnefFixture : IDisposable
{
    private readonly MemoryStream stream = new();
    private readonly BinaryWriter writer;
    public TnefFixture()
    {
        writer = new(stream, Encoding.UTF8, true);
        writer.Write(0x223e9f78u);
        writer.Write((ushort)1);
        Attribute(TnefAttributeLevel.Message, TnefAttributeTag.TnefVersion, BitConverter.GetBytes(0x10000));
        Attribute(TnefAttributeLevel.Message, TnefAttributeTag.OemCodepage, [0xa4, 3, 0, 0, 0, 0, 0, 0]); // CP932
    }

    public void Add(string? name, byte[] data, bool mapi = true, int method = 1)
    {
        Attribute(TnefAttributeLevel.Attachment, TnefAttributeTag.AttachRenderData, [1, 0, 0xff, 0xff, 0xff, 0xff, 0, 0, 0, 0, 0, 0, 0, 0]);
        if (!mapi)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            if (name != null) Attribute(TnefAttributeLevel.Attachment, TnefAttributeTag.AttachTitle, Encoding.GetEncoding(932).GetBytes(name + '\0'));
            Attribute(TnefAttributeLevel.Attachment, TnefAttributeTag.AttachData, data);
            return;
        }
        using var buffer = new MemoryStream();
        using var properties = new BinaryWriter(buffer);
        properties.Write(name == null ? 2 : 3);
        properties.Write(((uint)TnefPropertyId.AttachMethod << 16) | (uint)TnefPropertyType.Long);
        properties.Write(method);
        if (name != null) WriteVariable(properties, TnefPropertyId.AttachLongFilename, TnefPropertyType.Unicode, Encoding.Unicode.GetBytes(name + '\0'));
        WriteVariable(properties, TnefPropertyId.AttachData, TnefPropertyType.Binary, data);
        Attribute(TnefAttributeLevel.Attachment, TnefAttributeTag.Attachment, buffer.ToArray());
    }

    public void Attribute(TnefAttributeLevel level, TnefAttributeTag tag, byte[] data)
    {
        writer.Write((byte)level);
        writer.Write((uint)tag);
        writer.Write(data.Length);
        writer.Write(data);
        int checksum = 0;
        foreach (byte value in data) checksum = (checksum + value) & 0xffff;
        writer.Write((ushort)checksum);
    }

    public static void WriteVariable(BinaryWriter writer, TnefPropertyId id, TnefPropertyType type, byte[] data)
    {
        writer.Write(((uint)id << 16) | (uint)type);
        writer.Write(1); // number of values
        writer.Write(data.Length);
        writer.Write(data);
        for (int n = data.Length; n % 4 != 0; n++) writer.Write((byte)0);
    }
    public byte[] ToArray() => stream.ToArray();
    public void Dispose() { writer.Dispose(); stream.Dispose(); }
}
