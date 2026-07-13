namespace ChipsCore.Tests;

/// <summary>M2 mechanics: sliding, hazards, boots, blocks, walls, teleports.</summary>
public class M2Tests
{
    /// <summary>Chip at (5,5); tiles placed by MS byte code in the top layer.</summary>
    private static GameState NewState(params (int x, int y, byte code)[] tiles)
    {
        var level = new LevelData();
        level.TopLayer[5 * 32 + 5] = 0x6E;
        foreach (var (x, y, code) in tiles)
            level.TopLayer[y * 32 + x] = code;
        return new GameState(level);
    }

    // ------------------------------------------------------------- sliding

    [Fact]
    public void Ice_Slides_Until_It_Ends()
    {
        var s = NewState((6, 5, 0x0C), (7, 5, 0x0C));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal((6, 5), (s.ChipX, s.ChipY));
        Assert.Equal(Direction.Right, s.SlideDir);
        Assert.Equal(MoveResult.Moved, s.SlideStep());
        Assert.Equal(Direction.Right, s.SlideDir);
        Assert.Equal(MoveResult.Moved, s.SlideStep());
        Assert.Equal((8, 5), (s.ChipX, s.ChipY));
        Assert.Equal(Direction.None, s.SlideDir);
    }

    [Fact]
    public void Ice_Blocks_Manual_Input_While_Sliding()
    {
        var s = NewState((6, 5, 0x0C), (7, 5, 0x0C));
        s.TryMove(Direction.Right);
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Up));
    }

    [Fact]
    public void Ice_Bounces_Off_Walls()
    {
        var s = NewState((6, 5, 0x0C), (7, 5, 0x01));
        s.TryMove(Direction.Right);
        Assert.Equal(MoveResult.Blocked, s.SlideStep());
        Assert.Equal(Direction.Left, s.SlideDir);
        Assert.Equal(MoveResult.Moved, s.SlideStep());
        Assert.Equal((5, 5), (s.ChipX, s.ChipY));
        Assert.Equal(Direction.None, s.SlideDir);
    }

    [Fact]
    public void Ice_Blocked_Both_Ways_Stops_And_Returns_Control()
    {
        // Chip starts on a popup wall that seals behind him; the ice pocket
        // below is walled. Bounce fails both ways -> control returns.
        var level = new LevelData();
        level.TopLayer[5 * 32 + 6] = 0x6E;      // Chip at (6,5)
        level.BottomLayer[5 * 32 + 6] = 0x2E;   // ...standing on a popup wall
        level.TopLayer[6 * 32 + 6] = 0x0C;      // ice below
        level.TopLayer[7 * 32 + 6] = 0x01;      // wall below the ice
        var s = new GameState(level);

        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Down));
        Assert.Equal(Tile.Wall, s.GetTile(6, 5)); // popup sealed behind
        Assert.Equal(MoveResult.Blocked, s.SlideStep()); // wall below; bounce up
        Assert.Equal(MoveResult.Blocked, s.SlideStep()); // sealed above; stop
        Assert.Equal(Direction.None, s.SlideDir);
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Left)); // control is back
    }

    [Fact]
    public void Skates_Prevent_Sliding()
    {
        var s = NewState((4, 5, 0x6A), (6, 5, 0x0C));
        s.TryMove(Direction.Left);
        Assert.True(s.HasSkates);
        s.TryMove(Direction.Right);
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal(Direction.None, s.SlideDir);
    }

    [Fact]
    public void Ice_Corner_Redirects_The_Slide()
    {
        // 0x1C = walls S+E: entered moving right, the curve turns the slide north.
        var s = NewState((6, 5, 0x1C));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal(Direction.Up, s.SlideDir);
        Assert.Equal(MoveResult.Moved, s.SlideStep());
        Assert.Equal((6, 4), (s.ChipX, s.ChipY));
    }

    [Fact]
    public void Ice_Corner_Walls_Block_Entry()
    {
        // 0x1A = walls N+W: entering from the west crosses its W wall.
        var s = NewState((6, 5, 0x1A));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
    }

    [Fact]
    public void Force_Floor_Carries_Chip()
    {
        var s = NewState((6, 5, 0x13)); // force east
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal(Direction.Right, s.SlideDir);
        Assert.Equal(MoveResult.Moved, s.SlideStep());
        Assert.Equal((7, 5), (s.ChipX, s.ChipY));
        Assert.Equal(Direction.None, s.SlideDir);
    }

    [Fact]
    public void Force_Floor_Can_Be_Overridden_In_Any_Direction()
    {
        // Including against the force: CCLP1 #2's skates sit behind a
        // south-pointing force floor.
        var s = NewState((6, 5, 0x0D)); // force south
        s.TryMove(Direction.Right);
        Assert.Equal(Direction.Down, s.SlideDir);
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // push through
        Assert.Equal((7, 5), (s.ChipX, s.ChipY));
        Assert.Equal(Direction.None, s.SlideDir);
    }

    [Fact]
    public void Suction_Boots_Negate_Force_Floors()
    {
        var s = NewState((4, 5, 0x6B), (6, 5, 0x13));
        s.TryMove(Direction.Left);
        s.TryMove(Direction.Right);
        s.TryMove(Direction.Right);
        Assert.Equal(Direction.None, s.SlideDir);
    }

    [Fact]
    public void Sliding_Into_The_Exit_Wins()
    {
        var s = NewState((6, 5, 0x0C), (7, 5, 0x15));
        s.TryMove(Direction.Right);
        Assert.Equal(MoveResult.Won, s.SlideStep());
        Assert.True(s.Won);
    }

    // ------------------------------------------------------------- hazards

    [Fact]
    public void Water_Drowns_Without_Flippers()
    {
        var s = NewState((6, 5, 0x03));
        Assert.Equal(MoveResult.Died, s.TryMove(Direction.Right));
        Assert.True(s.IsDead);
        Assert.Contains("flippers", s.DeathReason);
    }

    [Fact]
    public void Flippers_Allow_Swimming()
    {
        var s = NewState((4, 5, 0x68), (6, 5, 0x03));
        s.TryMove(Direction.Left);
        s.TryMove(Direction.Right);
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.False(s.IsDead);
    }

    [Fact]
    public void Fire_Burns_Without_Boots()
    {
        var s = NewState((6, 5, 0x04));
        Assert.Equal(MoveResult.Died, s.TryMove(Direction.Right));
    }

    [Fact]
    public void Fire_Boots_Allow_Walking_On_Fire()
    {
        var s = NewState((4, 5, 0x69), (6, 5, 0x04));
        s.TryMove(Direction.Left);
        s.TryMove(Direction.Right);
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
    }

    [Fact]
    public void Bombs_Kill()
    {
        var s = NewState((6, 5, 0x2A));
        Assert.Equal(MoveResult.Died, s.TryMove(Direction.Right));
        Assert.Equal(Tile.Floor, s.GetTile(6, 5));
    }

    [Fact]
    public void No_Movement_After_Death()
    {
        var s = NewState((6, 5, 0x2A));
        s.TryMove(Direction.Right);
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Down));
    }

    [Fact]
    public void Thief_Steals_All_Boots()
    {
        var s = NewState((4, 5, 0x68), (6, 5, 0x21));
        s.TryMove(Direction.Left);
        Assert.True(s.HasFlippers);
        s.TryMove(Direction.Right);
        s.TryMove(Direction.Right);
        Assert.False(s.HasFlippers);
    }

    [Fact]
    public void Dirt_Turns_To_Floor_When_Stepped_On()
    {
        var s = NewState((6, 5, 0x0B));
        s.TryMove(Direction.Right);
        Assert.Equal(Tile.Floor, s.GetTile(6, 5));
    }

    // ------------------------------------------------------------- blocks

    [Fact]
    public void Block_Pushes_Onto_Floor()
    {
        var s = NewState((6, 5, 0x0A));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal((6, 5), (s.ChipX, s.ChipY));
        Assert.True(s.HasBlockAt(7, 5));
        Assert.False(s.HasBlockAt(6, 5));
    }

    [Fact]
    public void Block_Into_Water_Makes_Dirt()
    {
        var s = NewState((6, 5, 0x0A), (7, 5, 0x03));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal(Tile.Dirt, s.GetTile(7, 5));
        Assert.False(s.HasBlockAt(7, 5));
    }

    [Fact]
    public void Block_Against_Wall_Wont_Push()
    {
        var s = NewState((6, 5, 0x0A), (7, 5, 0x01));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
    }

    [Fact]
    public void Block_Against_Block_Wont_Push()
    {
        var s = NewState((6, 5, 0x0A), (7, 5, 0x0A));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
    }

    [Fact]
    public void Block_Into_Bomb_Destroys_Both()
    {
        var s = NewState((6, 5, 0x0A), (7, 5, 0x2A));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal(Tile.Floor, s.GetTile(7, 5));
        Assert.False(s.HasBlockAt(7, 5));
    }

    // ------------------------------------------------------------- walls

    [Fact]
    public void Thin_Wall_Blocks_Exit_Side_Only()
    {
        var s = NewState((6, 5, 0x09)); // thin wall on east edge
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // enter from west
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right)); // east edge walled
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Left)); // back out west
    }

    [Fact]
    public void Thin_Wall_Blocks_Entry_Across_Its_Edge()
    {
        var s = NewState((6, 5, 0x07)); // thin wall on west edge
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
    }

    [Fact]
    public void Popup_Wall_Seals_Behind_Chip()
    {
        var s = NewState((6, 5, 0x2E));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal(Tile.Wall, s.GetTile(6, 5));
    }

    [Fact]
    public void Appearing_Wall_Reveals_On_Bump()
    {
        var s = NewState((6, 5, 0x2C));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
        Assert.Equal(Tile.Wall, s.GetTile(6, 5));
    }

    [Fact]
    public void Green_Button_Toggles_Walls()
    {
        var s = NewState((6, 5, 0x23), (7, 5, 0x25), (8, 5, 0x26));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // press button
        Assert.Equal(Tile.ToggleOpen, s.GetTile(7, 5));
        Assert.Equal(Tile.ToggleClosed, s.GetTile(8, 5));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
    }

    // ------------------------------------------------------------- teleports

    [Fact]
    public void Teleport_Relays_To_Previous_In_Reading_Order()
    {
        // Teleports are slip floors: enter, then the relay happens on the
        // slide clock (which is also what makes teleport boosting work).
        var s = NewState((6, 5, 0x29), (2, 2, 0x29));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal((6, 5), (s.ChipX, s.ChipY));
        Assert.Equal(Direction.Right, s.SlideDir);
        Assert.Equal(MoveResult.Moved, s.SlideStep());
        Assert.Equal((3, 2), (s.ChipX, s.ChipY)); // exited the other teleport, still moving right
    }

    [Fact]
    public void Teleport_With_Blocked_Exits_Bounces_Chip_Back_Out()
    {
        // Both forward exits and the other teleport's reverse exit are
        // walled; TW reverses the slip and Chip pops back out where he
        // entered.
        var s = NewState((6, 5, 0x29), (2, 2, 0x29),
            (7, 5, 0x01), (3, 2, 0x01), (1, 2, 0x01));
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        for (var i = 0; i < 4 && s.SlideDir != Direction.None; i++)
            s.SlideStep();
        Assert.Equal((5, 5), (s.ChipX, s.ChipY)); // back where he started
        Assert.Equal(Direction.None, s.SlideDir);
    }

    // ------------------------------------------------------------- pathfinder safety

    [Fact]
    public void Pathfinder_Routes_Around_Water()
    {
        var s = NewState((6, 5, 0x03));
        var path = Pathfinder.FindPath(s, 7, 5);
        Assert.NotNull(path);
        Assert.True(path!.Count > 2, "must detour around the water");
    }

    [Fact]
    public void Pathfinder_Crosses_Water_With_Flippers()
    {
        var s = NewState((4, 5, 0x68), (6, 5, 0x03));
        s.TryMove(Direction.Left);
        var path = Pathfinder.FindPath(s, 7, 5);
        Assert.NotNull(path);
    }

    [Fact]
    public void Pathfinder_Wont_Route_Through_Blocks_Or_Ice()
    {
        var s = NewState((6, 5, 0x0A), (6, 4, 0x0C), (6, 6, 0x0C));
        var path = Pathfinder.FindPath(s, 7, 5);
        Assert.NotNull(path); // long way around exists
        Assert.True(path!.Count > 2);
    }
}
