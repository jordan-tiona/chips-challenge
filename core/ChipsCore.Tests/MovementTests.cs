namespace ChipsCore.Tests;

public class MovementTests
{
    /// <summary>Chip at (5,5) on an empty floor level requiring 1 chip.</summary>
    private static GameState NewState(params (int x, int y, byte code)[] tiles)
    {
        var level = new LevelData { ChipsRequired = 1 };
        level.TopLayer[5 * 32 + 5] = 0x6E; // Chip facing south at (5,5)
        foreach (var (x, y, code) in tiles)
            level.TopLayer[y * 32 + x] = code;
        return new GameState(level);
    }

    [Fact]
    public void Walks_On_Floor()
    {
        var s = NewState();
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal((6, 5), (s.ChipX, s.ChipY));
    }

    [Fact]
    public void Wall_Blocks()
    {
        var s = NewState((6, 5, 0x01));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
        Assert.Equal((5, 5), (s.ChipX, s.ChipY));
    }

    [Fact]
    public void Map_Edge_Blocks()
    {
        var level = new LevelData();
        level.TopLayer[0] = 0x6E; // Chip at (0,0)
        var s = new GameState(level);
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Up));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Left));
    }

    [Fact]
    public void Collects_Chip_And_Opens_Socket()
    {
        var s = NewState((6, 5, 0x02), (7, 5, 0x22), (8, 5, 0x15));
        Assert.Equal(1, s.ChipsRemaining);
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // pick up chip
        Assert.Equal(0, s.ChipsRemaining);
        Assert.Equal(Tile.Floor, s.GetTile(6, 5));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // through socket
        Assert.Equal(MoveResult.Won, s.TryMove(Direction.Right));   // exit
        Assert.True(s.Won);
    }

    [Fact]
    public void Socket_Blocks_While_Chips_Remain()
    {
        var s = NewState((6, 5, 0x22));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
    }

    [Fact]
    public void Door_Needs_Key_And_Consumes_It()
    {
        var s = NewState((6, 5, 0x17), (5, 6, 0x65)); // red door right, red key below
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Down)); // grab key
        Assert.Equal(1, s.GetKeyCount(Tile.KeyRed));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Up));   // back
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // through door
        Assert.Equal(0, s.GetKeyCount(Tile.KeyRed));
        Assert.Equal(Tile.Floor, s.GetTile(6, 5));
    }

    [Fact]
    public void Green_Key_Is_Never_Consumed()
    {
        var s = NewState((5, 6, 0x66), (6, 5, 0x18), (7, 5, 0x18));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Down)); // green key
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Up));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // door 1
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // door 2
        Assert.Equal(1, s.GetKeyCount(Tile.KeyGreen));
    }

    [Fact]
    public void Fake_Wall_Vanishes_On_Bump_Without_Moving()
    {
        var s = NewState((6, 5, 0x1E));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
        Assert.Equal(Tile.Floor, s.GetTile(6, 5));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
    }

    [Fact]
    public void No_Movement_After_Winning()
    {
        var s = NewState((6, 5, 0x15));
        var level = new LevelData(); // exit right of chip, no chips required
        level.TopLayer[5 * 32 + 5] = 0x6E;
        level.TopLayer[5 * 32 + 6] = 0x15;
        s = new GameState(level);
        Assert.Equal(MoveResult.Won, s.TryMove(Direction.Right));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Left));
    }

    [Fact]
    public void Terrain_Under_Actors_Comes_From_Bottom_Layer()
    {
        var level = new LevelData();
        level.TopLayer[5 * 32 + 5] = 0x6E;   // Chip
        level.BottomLayer[5 * 32 + 5] = 0x2F; // standing on a hint tile
        level.TopLayer[5 * 32 + 6] = 0x40;   // a bug (ignored in M1)
        level.BottomLayer[5 * 32 + 6] = 0x0C; // on ice
        var s = new GameState(level);
        Assert.True(s.OnHint);
        Assert.Equal(Tile.Ice, s.GetTile(6, 5));
    }
}
