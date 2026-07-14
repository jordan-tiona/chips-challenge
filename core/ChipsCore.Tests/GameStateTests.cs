using ChipsCore;

namespace ChipsCore.Tests;

public class GameStateTests
{
    [Fact]
    public void NewState_IsAllFloor()
    {
        var state = new GameState();
        for (var y = 0; y < GameState.Height; y++)
            for (var x = 0; x < GameState.Width; x++)
                Assert.Equal(Tile.Floor, state.GetTile(x, y));
    }

    [Fact]
    public void SetTile_RoundTrips()
    {
        var state = new GameState();
        state.SetTile(5, 7, Tile.Wall);
        Assert.Equal(Tile.Wall, state.GetTile(5, 7));
        Assert.Equal(Tile.Floor, state.GetTile(7, 5));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(32, 0)]
    [InlineData(0, 32)]
    public void GetTile_OutOfBounds_Throws(int x, int y)
    {
        var state = new GameState();
        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetTile(x, y));
    }

    [Theory]
    [InlineData((byte)0x02, Tile.Chip)]
    [InlineData((byte)0x65, Tile.KeyRed)]
    [InlineData((byte)0x69, Tile.BootsFire)]
    public void ItemCollected_Fires_On_Pickup(byte code, Tile expected)
    {
        var level = new LevelData();
        level.TopLayer[5 * 32 + 5] = 0x6E;   // Chip at (5,5)
        level.TopLayer[5 * 32 + 6] = code;   // item at (6,5)
        var state = new GameState(level);

        var collected = new List<(Tile Item, int X, int Y)>();
        state.ItemCollected += (item, x, y) => collected.Add((item, x, y));

        state.TryMove(Direction.Right);
        Assert.Equal([(expected, 6, 5)], collected);

        state.TryMove(Direction.Right); // plain floor: no event
        Assert.Single(collected);
    }
}
