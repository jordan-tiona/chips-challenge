namespace ChipsCore.Tests;

public class PathfinderTests
{
    /// <summary>Chip at (5,5); tiles placed by MS byte code.</summary>
    private static GameState NewState(params (int x, int y, byte code)[] tiles)
    {
        var level = new LevelData();
        level.TopLayer[5 * 32 + 5] = 0x6E;
        foreach (var (x, y, code) in tiles)
            level.TopLayer[y * 32 + x] = code;
        return new GameState(level);
    }

    private static void Walk(GameState s, List<Direction> path)
    {
        foreach (var dir in path)
            Assert.NotEqual(MoveResult.Blocked, s.TryMove(dir));
    }

    [Fact]
    public void Straight_Line()
    {
        var s = NewState();
        var path = Pathfinder.FindPath(s, 9, 5);
        Assert.NotNull(path);
        Assert.Equal(4, path!.Count);
        Walk(s, path);
        Assert.Equal((9, 5), (s.ChipX, s.ChipY));
    }

    [Fact]
    public void Routes_Around_Walls()
    {
        // vertical wall at x=7 from y=0..30 — must go around via y=31
        var walls = new List<(int, int, byte)>();
        for (var y = 0; y <= 30; y++) walls.Add((7, y, 0x01));
        var s = NewState(walls.ToArray());
        var path = Pathfinder.FindPath(s, 9, 5);
        Assert.NotNull(path);
        Walk(s, path);
        Assert.Equal((9, 5), (s.ChipX, s.ChipY));
        Assert.True(path!.Count > 4, "must detour around the wall");
    }

    [Fact]
    public void Unreachable_Returns_Null()
    {
        var s = NewState((4, 5, 0x01), (6, 5, 0x01), (5, 4, 0x01), (5, 6, 0x01));
        Assert.Null(Pathfinder.FindPath(s, 9, 5));
    }

    [Fact]
    public void Wall_Target_Returns_Null()
    {
        var s = NewState((9, 5, 0x01));
        Assert.Null(Pathfinder.FindPath(s, 9, 5));
    }

    [Fact]
    public void Own_Square_Returns_Empty_Path()
    {
        var s = NewState();
        var path = Pathfinder.FindPath(s, 5, 5);
        Assert.NotNull(path);
        Assert.Empty(path!);
    }

    [Fact]
    public void Door_Blocks_Without_Key_And_Opens_With_It()
    {
        // (7,5) sits in a sealed room whose only entrance is the red door at (6,5)
        var s = NewState((6, 4, 0x01), (7, 4, 0x01), (8, 4, 0x01),
                         (8, 5, 0x01),
                         (6, 6, 0x01), (7, 6, 0x01), (8, 6, 0x01),
                         (6, 5, 0x17),
                         (4, 5, 0x65)); // red key to the left
        Assert.Null(Pathfinder.FindPath(s, 7, 5));       // no key yet
        s.TryMove(Direction.Left);                        // grab key
        var path = Pathfinder.FindPath(s, 7, 5);
        Assert.NotNull(path);
        Walk(s, path!);
        Assert.Equal((7, 5), (s.ChipX, s.ChipY));
    }
}
