using System.Text;

namespace ChipsCore.Tests;

public class DatParserTests
{
    /// <summary>Builds a minimal, valid one-level DAT file in memory.</summary>
    private static byte[] BuildDat(
        ushort time = 100, ushort chips = 2,
        string title = "TEST LEVEL", string password = "ABCD", string hint = "hi",
        Action<byte[]>? layoutTop = null)
    {
        var top = new byte[1024];
        layoutTop?.Invoke(top);

        using var levelBody = new MemoryStream();
        using var w = new BinaryWriter(levelBody);
        w.Write((ushort)1);      // level number
        w.Write(time);
        w.Write(chips);
        w.Write((ushort)1);      // map detail

        WriteLayerRle(w, top);
        WriteLayerRle(w, new byte[1024]); // bottom: all floor

        using var fields = new MemoryStream();
        using var fw = new BinaryWriter(fields);
        WriteField(fw, 3, Encoding.Latin1.GetBytes(title + "\0"));
        var pw = Encoding.Latin1.GetBytes(password + "\0");
        for (var i = 0; i < pw.Length; i++) pw[i] ^= 0x99;
        WriteField(fw, 6, pw);
        WriteField(fw, 7, Encoding.Latin1.GetBytes(hint + "\0"));
        WriteField(fw, 10, new byte[] { 5, 6, 7, 8 }); // two monsters

        w.Write((ushort)fields.Length);
        fields.WriteTo(levelBody);

        using var file = new MemoryStream();
        using var fwOut = new BinaryWriter(file);
        fwOut.Write(DatParser.MagicMs);
        fwOut.Write((ushort)1);                 // level count
        fwOut.Write((ushort)levelBody.Length);  // level size
        levelBody.WriteTo(file);
        return file.ToArray();
    }

    private static void WriteLayerRle(BinaryWriter w, byte[] layer)
    {
        // Encode with one deliberate RLE run so decoding is exercised:
        // emit the first 100 bytes as a run if uniform, else all literal.
        using var ms = new MemoryStream();
        var i = 0;
        while (i < layer.Length)
        {
            var runValue = layer[i];
            var runLength = 1;
            while (i + runLength < layer.Length && layer[i + runLength] == runValue && runLength < 255)
                runLength++;
            if (runLength >= 4)
            {
                ms.WriteByte(0xFF);
                ms.WriteByte((byte)runLength);
                ms.WriteByte(runValue);
            }
            else
            {
                for (var j = 0; j < runLength; j++) ms.WriteByte(runValue);
            }
            i += runLength;
        }
        w.Write((ushort)ms.Length);
        ms.WriteTo(w.BaseStream);
    }

    private static void WriteField(BinaryWriter w, byte type, byte[] payload)
    {
        w.Write(type);
        w.Write((byte)payload.Length);
        w.Write(payload);
    }

    [Fact]
    public void Parses_Header_And_Metadata()
    {
        var levels = DatParser.Parse(BuildDat(time: 250, chips: 7));
        var level = Assert.Single(levels);
        Assert.Equal(1, level.Number);
        Assert.Equal(250, level.TimeLimit);
        Assert.Equal(7, level.ChipsRequired);
        Assert.Equal("TEST LEVEL", level.Title);
        Assert.Equal("ABCD", level.Password);
        Assert.Equal("hi", level.Hint);
        Assert.Equal(2, level.MonsterList.Count);
        Assert.Equal((5, 6), level.MonsterList[0]);
    }

    [Fact]
    public void Decodes_Rle_Layers()
    {
        var levels = DatParser.Parse(BuildDat(layoutTop: top =>
        {
            for (var i = 0; i < 32; i++) top[i] = 0x01;  // top row of walls
            top[100] = 0x02;                             // one chip
        }));
        var level = levels[0];
        Assert.All(Enumerable.Range(0, 32), i => Assert.Equal(0x01, level.TopLayer[i]));
        Assert.Equal(0x02, level.TopLayer[100]);
        Assert.Equal(0x00, level.TopLayer[99]);
        Assert.Equal(1024, level.TopLayer.Length);
    }

    [Fact]
    public void Finds_Chip_Start()
    {
        var levels = DatParser.Parse(BuildDat(layoutTop: top => top[5 * 32 + 9] = 0x6E));
        Assert.Equal((9, 5), levels[0].FindChipStart());
    }

    [Fact]
    public void Rejects_Non_Dat_Data()
    {
        Assert.Throws<InvalidDataException>(() => DatParser.Parse(new byte[] { 1, 2, 3, 4, 5, 6 }));
    }
}
