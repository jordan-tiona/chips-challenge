namespace ChipsCore.Tests;

/// <summary>Blocks sliding on ice and force floors (M3.1).</summary>
public class BlockSlideTests
{
    private static GameState NewState(params (int x, int y, byte code)[] tiles)
    {
        var level = new LevelData();
        level.TopLayer[5 * 32 + 5] = 0x6E;
        foreach (var (x, y, code) in tiles)
            level.TopLayer[y * 32 + x] = code;
        return new GameState(level);
    }

    private static void RunSlides(GameState s, int max = 40)
    {
        for (var i = 0; i < max && s.AnyBlocksSliding && !s.IsDead; i++)
            s.SlideBlocks();
    }

    [Fact]
    public void Block_Pushed_Onto_Ice_Skates_To_The_End()
    {
        var s = NewState((6, 5, 0x0A), (7, 5, 0x0C), (8, 5, 0x0C)); // block, ice x2
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.True(s.AnyBlocksSliding);
        RunSlides(s);
        Assert.True(s.HasBlockAt(9, 5)); // slid across both ice tiles onto floor
        Assert.False(s.AnyBlocksSliding);
    }

    [Fact]
    public void Sliding_Block_Bounces_Off_Wall_Once()
    {
        var s = NewState((6, 5, 0x0A), (7, 5, 0x0C), (8, 5, 0x01)); // ice then wall
        s.TryMove(Direction.Right);
        RunSlides(s);
        Assert.True(s.HasBlockAt(6, 5)); // bounced back off the ice to where it started
    }

    [Fact]
    public void Block_On_Force_Floor_Rides_The_Current()
    {
        var s = NewState((6, 5, 0x0A), (7, 5, 0x13), (8, 5, 0x13)); // force east x2
        s.TryMove(Direction.Right);
        RunSlides(s);
        Assert.True(s.HasBlockAt(9, 5));
    }

    [Fact]
    public void Sliding_Block_Sinks_In_Water_At_The_Far_End()
    {
        var s = NewState((6, 5, 0x0A), (7, 5, 0x0C), (8, 5, 0x03)); // ice into water
        s.TryMove(Direction.Right);
        RunSlides(s);
        Assert.Equal(Tile.Dirt, s.GetTile(8, 5));
        Assert.False(s.HasBlockAt(8, 5));
    }

    [Fact]
    public void Ice_Corner_Redirects_Sliding_Block()
    {
        // block slides east onto IceSE (walls S+E): turns north
        var s = NewState((6, 5, 0x0A), (7, 5, 0x0C), (8, 5, 0x1C), (8, 4, 0x0C));
        s.TryMove(Direction.Right);
        RunSlides(s);
        Assert.True(s.HasBlockAt(8, 3));
    }

    [Fact]
    public void Sliding_Block_Crushes_Chip()
    {
        // U-shaped ice: Chip pushes the block north; corners curve it east
        // then south, and it comes back down one column over — onto the
        // floor tile Chip has just stepped to.
        var s = NewState(
            (5, 4, 0x0A),  // block directly north of Chip
            (5, 3, 0x0C),  // ice heading north
            (5, 2, 0x1A),  // IceNW (walls N+W): northbound -> east
            (6, 2, 0x1B),  // IceNE (walls N+E): eastbound -> south
            (6, 3, 0x0C)); // ice heading south; (6,4) is plain floor
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Up));    // push; Chip to (5,4)
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // Chip to (6,4), in the lane
        RunSlides(s);
        Assert.True(s.IsDead);
        Assert.Contains("block", s.DeathReason);
    }
}
