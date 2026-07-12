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
}
