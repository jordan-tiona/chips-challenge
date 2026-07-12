using System.Text;

namespace ChipsCore;

/// <summary>
/// Parser for the CC1 .DAT level file format (CHIPS.DAT, CCLPx.dat).
/// Format: uint32 magic, uint16 level count, then per level a uint16 byte
/// count followed by header, two RLE-compressed 32x32 layers, and a set of
/// optional metadata fields. All integers little-endian.
/// </summary>
public static class DatParser
{
    public const uint MagicMs = 0x0002AAAC;
    public const uint MagicLynx = 0x0102AAAC; // Tile World "Lynx" variant

    public static List<LevelData> Parse(byte[] data)
    {
        using var reader = new BinaryReader(new MemoryStream(data));

        var magic = reader.ReadUInt32();
        if (magic != MagicMs && magic != MagicLynx)
            throw new InvalidDataException($"Not a CC1 .DAT file (magic 0x{magic:X8})");

        int levelCount = reader.ReadUInt16();
        var levels = new List<LevelData>(levelCount);
        for (var i = 0; i < levelCount; i++)
            levels.Add(ReadLevel(reader));
        return levels;
    }

    private static LevelData ReadLevel(BinaryReader reader)
    {
        int levelBytes = reader.ReadUInt16();
        var levelEnd = reader.BaseStream.Position + levelBytes;

        var level = new LevelData
        {
            Number = reader.ReadUInt16(),
            TimeLimit = reader.ReadUInt16(),
            ChipsRequired = reader.ReadUInt16(),
        };
        reader.ReadUInt16(); // map detail; always 1

        ReadLayer(reader, level.TopLayer);
        ReadLayer(reader, level.BottomLayer);

        int optionalBytes = reader.ReadUInt16();
        var optionalEnd = reader.BaseStream.Position + optionalBytes;
        while (reader.BaseStream.Position < optionalEnd)
        {
            var fieldType = reader.ReadByte();
            var fieldLength = reader.ReadByte();
            var payload = reader.ReadBytes(fieldLength);
            switch (fieldType)
            {
                case 3:
                    level.Title = ReadStringZ(payload);
                    break;
                case 4:
                    level.TrapWiring = payload;
                    break;
                case 5:
                    level.CloneWiring = payload;
                    break;
                case 6:
                    for (var i = 0; i < payload.Length; i++)
                        payload[i] ^= 0x99;
                    level.Password = ReadStringZ(payload);
                    break;
                case 7:
                    level.Hint = ReadStringZ(payload);
                    break;
                case 10:
                    for (var i = 0; i + 1 < payload.Length; i += 2)
                        level.MonsterList.Add((payload[i], payload[i + 1]));
                    break;
                // 8 (plain password) and unknown fields: ignore
            }
        }

        // Trust the declared level size over our own accounting.
        reader.BaseStream.Position = levelEnd;
        return level;
    }

    /// <summary>RLE decode one layer: 0xFF introduces a (count, value) run;
    /// any other byte is a literal tile code.</summary>
    private static void ReadLayer(BinaryReader reader, byte[] layer)
    {
        int compressedBytes = reader.ReadUInt16();
        var end = reader.BaseStream.Position + compressedBytes;
        var written = 0;
        while (reader.BaseStream.Position < end && written < layer.Length)
        {
            var b = reader.ReadByte();
            if (b == 0xFF)
            {
                int count = reader.ReadByte();
                var value = reader.ReadByte();
                for (var i = 0; i < count && written < layer.Length; i++)
                    layer[written++] = value;
            }
            else
            {
                layer[written++] = b;
            }
        }
        reader.BaseStream.Position = end;
    }

    private static string ReadStringZ(byte[] payload)
    {
        var length = Array.IndexOf(payload, (byte)0);
        if (length < 0) length = payload.Length;
        return Encoding.Latin1.GetString(payload, 0, length);
    }
}
