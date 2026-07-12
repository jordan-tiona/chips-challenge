namespace ChipsCore.Tests;

/// <summary>Integration tests against the real bundled CCLP1.dat.</summary>
public class Cclp1Tests
{
    private static byte[] LoadCclp1()
    {
        // Walk up from the test bin directory to the repo root.
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "levels", "CCLP1.dat")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir == null, "levels/CCLP1.dat not found above test directory");
        return File.ReadAllBytes(Path.Combine(dir!, "levels", "CCLP1.dat"));
    }

    [Fact]
    public void Parses_All_149_Levels()
    {
        var levels = DatParser.Parse(LoadCclp1());
        Assert.Equal(149, levels.Count);
        Assert.All(levels, l =>
        {
            Assert.False(string.IsNullOrEmpty(l.Title), $"level {l.Number} has no title");
            Assert.False(string.IsNullOrEmpty(l.Password), $"level {l.Number} has no password");
        });
    }

    [Fact]
    public void Levels_With_Hint_Tiles_Have_Hint_Text()
    {
        var levels = DatParser.Parse(LoadCclp1());
        var withHintTile = levels.Where(l =>
            l.TopLayer.Contains((byte)0x2F) || l.BottomLayer.Contains((byte)0x2F)).ToList();
        Assert.NotEmpty(withHintTile);
        Assert.All(withHintTile, l =>
            Assert.False(string.IsNullOrWhiteSpace(l.Hint),
                $"level {l.Number} ({l.Title}) has a hint tile but no hint text"));
    }

    [Fact]
    public void Every_Level_Loads_Into_A_GameState_With_A_Chip_Start()
    {
        var levels = DatParser.Parse(LoadCclp1());
        foreach (var level in levels)
        {
            var start = level.FindChipStart();
            Assert.True(TileCodes.IsChipStart(level.TopLayer[start.Y * 32 + start.X]),
                $"level {level.Number} ({level.Title}) has no Chip start tile");
            var state = new GameState(level);
            Assert.Equal(start, (state.ChipX, state.ChipY));
        }
    }
}
