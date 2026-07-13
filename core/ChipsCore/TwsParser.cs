using System.Text;

namespace ChipsCore;

public sealed class TwsSolution
{
    public int LevelNumber { get; init; }
    public string Password { get; init; } = "";
    public int Stepping { get; init; }
    public uint RngSeed { get; init; }
    public int TimeTicks { get; init; }
    public List<(int Tick, Direction Dir)> Moves { get; } = new();

    /// <summary>Lynx diagonals or MS mouse moves — we can't replay these.</summary>
    public bool HasUnsupportedMoves { get; set; }
}

/// <summary>
/// Parser for Tile World .tws solution files. Format documented in
/// tworld's solution.c; all integers little-endian, move encodings use
/// little-endian *bit* order (bit 0 is the LSB of the first byte).
/// </summary>
public static class TwsParser
{
    public const uint Magic = 0x999B3335;

    public const int MsRuleset = 2;

    public static Dictionary<int, TwsSolution> Parse(byte[] data)
    {
        using var reader = new BinaryReader(new MemoryStream(data));
        if (reader.ReadUInt32() != Magic)
            throw new InvalidDataException("Not a TWS solution file");
        var ruleset = reader.ReadByte();
        if (ruleset != MsRuleset)
            throw new InvalidDataException($"Expected MS-ruleset solutions, got ruleset {ruleset}");
        reader.ReadUInt16();                    // reserved
        int extra = reader.ReadByte();
        reader.BaseStream.Position += extra;

        var solutions = new Dictionary<int, TwsSolution>();
        while (reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
        {
            long size = reader.ReadUInt32();
            if (size == 0) continue;            // padding record
            var end = reader.BaseStream.Position + size;
            if (size >= 16)
            {
                var solution = ReadSolution(reader, end);
                // First record can be a settings pseudo-record (level 0).
                if (solution.LevelNumber > 0)
                    solutions[solution.LevelNumber] = solution;
            }
            reader.BaseStream.Position = end;   // size==6: password-only record
        }
        return solutions;
    }

    private static TwsSolution ReadSolution(BinaryReader reader, long end)
    {
        var number = reader.ReadUInt16();
        var password = Encoding.ASCII.GetString(reader.ReadBytes(4));
        reader.ReadByte(); // flags
        var b11 = reader.ReadByte();
        var solution = new TwsSolution
        {
            LevelNumber = number,
            Password = password,
            Stepping = (b11 >> 3) & 7,
            RngSeed = reader.ReadUInt32(),
            TimeTicks = (int)reader.ReadUInt32(),
        };
        ReadMoves(reader, end, solution);
        return solution;
    }

    private static void ReadMoves(BinaryReader reader, long end, TwsSolution solution)
    {
        var tick = 0;
        var first = true;

        void Add(long delta, int dirIndex)
        {
            // A handful of public-TWS records set time bits the spec says
            // are always zero (nonstandard tooling, likely mouse-optimized
            // solutions). Flag rather than produce garbage ticks.
            if (delta > 1_000_000)
            {
                solution.HasUnsupportedMoves = true;
                return;
            }
            tick += (int)delta + (first ? 0 : 1); // stored T is delta-1 except the first move
            first = false;
            if (dirIndex is >= 0 and <= 3)
            {
                solution.Moves.Add((tick, dirIndex switch
                {
                    0 => Direction.Up,
                    1 => Direction.Left,
                    2 => Direction.Down,
                    _ => Direction.Right,
                }));
            }
            else
            {
                solution.HasUnsupportedMoves = true; // diagonal or mouse move
            }
        }

        while (reader.BaseStream.Position < end)
        {
            int b0 = reader.ReadByte();
            switch (b0 & 3)
            {
                case 0: // format 3: 00DDEEFF — three moves, four ticks apart
                    Add(3, (b0 >> 2) & 3);
                    Add(3, (b0 >> 4) & 3);
                    Add(3, (b0 >> 6) & 3);
                    break;

                case 1: // format 1, one byte: NNDDDTTT with NN=01
                    Add((b0 >> 5) & 7, (b0 >> 2) & 7);
                    break;

                case 2: // format 1, two bytes: T is 11 bits
                {
                    int b1 = reader.ReadByte();
                    Add(((b0 >> 5) & 7) | (b1 << 3), (b0 >> 2) & 7);
                    break;
                }

                case 3 when (b0 & 0x10) == 0: // format 2: four bytes, 2-bit dir, 27-bit T
                {
                    int b1 = reader.ReadByte(), b2 = reader.ReadByte(), b3 = reader.ReadByte();
                    var t = (long)((b0 >> 5) & 7) | ((long)b1 << 3) | ((long)b2 << 11) | ((long)b3 << 19);
                    Add(t, (b0 >> 2) & 3);
                    break;
                }

                default: // format 4: 11NN1DDD DDDDDDTT ... (2..5 bytes, 9-bit dir)
                {
                    var extraBytes = ((b0 >> 2) & 3) + 1;
                    var rest = reader.ReadBytes(extraBytes);
                    var dir9 = ((b0 >> 5) & 7) | ((rest[0] & 0x3F) << 3);
                    var t = (long)((rest[0] >> 6) & 3);
                    for (var i = 1; i < rest.Length; i++)
                        t |= (long)rest[i] << (2 + 8 * (i - 1));
                    Add(t, dir9 <= 3 ? dir9 : -1);
                    break;
                }
            }
        }
    }
}
