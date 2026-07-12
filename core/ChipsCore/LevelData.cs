namespace ChipsCore;

/// <summary>
/// One parsed level from a .DAT file: immutable input to a GameState.
/// Layers hold raw MS tile byte codes in row-major 32x32 order.
/// </summary>
public sealed class LevelData
{
    public const int LayerSize = 32 * 32;

    public int Number { get; init; }
    public int TimeLimit { get; init; }           // seconds; 0 = untimed
    public int ChipsRequired { get; init; }
    public string Title { get; set; } = "";
    public string Password { get; set; } = "";    // decoded (XOR 0x99 in file)
    public string Hint { get; set; } = "";
    public byte[] TopLayer { get; init; } = new byte[LayerSize];
    public byte[] BottomLayer { get; init; } = new byte[LayerSize];

    /// <summary>Initial monster order, as (x, y) positions. Drives monster
    /// movement order in the MS ruleset (M3).</summary>
    public List<(int X, int Y)> MonsterList { get; } = new();

    /// <summary>Raw trap wiring field (type 4): buttonX, buttonY, trapX,
    /// trapY, state — five uint16 per entry. Decoded properly in M2.</summary>
    public byte[] TrapWiring { get; set; } = Array.Empty<byte>();

    /// <summary>Raw clone machine wiring field (type 5): buttonX, buttonY,
    /// machineX, machineY — four uint16 per entry. Decoded properly in M2.</summary>
    public byte[] CloneWiring { get; set; } = Array.Empty<byte>();

    /// <summary>Find Chip's start position (tile codes 0x6C–0x6F in the top
    /// layer; last occurrence wins, matching MS). Falls back to (0, 0).</summary>
    public (int X, int Y) FindChipStart()
    {
        var result = (X: 0, Y: 0);
        for (var i = 0; i < LayerSize; i++)
        {
            if (TileCodes.IsChipStart(TopLayer[i]))
                result = (i % 32, i / 32);
        }
        return result;
    }
}
