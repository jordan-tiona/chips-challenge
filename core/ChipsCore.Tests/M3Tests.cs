namespace ChipsCore.Tests;

/// <summary>M3: monsters, per-species AI, contact death, button wiring.</summary>
public class M3Tests
{
    /// <summary>Chip at (5,5); tiles by MS byte code; every monster placed
    /// is added to the monster list (active) unless listed in inactive.</summary>
    private static GameState NewState(
        (int x, int y, byte code)[] tiles,
        (int bx, int by, int tx, int ty)[]? traps = null,
        (int bx, int by, int mx, int my)[]? clones = null,
        (int x, int y)[]? inactive = null)
    {
        var level = new LevelData();
        level.TopLayer[5 * 32 + 5] = 0x6E;
        foreach (var (x, y, code) in tiles)
        {
            level.TopLayer[y * 32 + x] = code;
            if (TileCodes.IsMonster(code) && !(inactive ?? Array.Empty<(int, int)>()).Contains((x, y)))
                level.MonsterList.Add((x, y));
        }
        level.TrapWiring = Wire(traps, pad: true);
        level.CloneWiring = Wire(clones, pad: false);
        return new GameState(level);
    }

    private static byte[] Wire((int, int, int, int)[]? entries, bool pad)
    {
        if (entries == null) return Array.Empty<byte>();
        var bytes = new List<byte>();
        foreach (var (a, b, c, d) in entries)
        {
            foreach (var v in new[] { a, b, c, d })
            {
                bytes.Add((byte)v);
                bytes.Add(0);
            }
            if (pad) { bytes.Add(0); bytes.Add(0); } // trap entries are 5 words
        }
        return bytes.ToArray();
    }

    // ------------------------------------------------------------- movement AI

    [Fact]
    public void Fireball_Goes_Straight_Then_Turns_Right()
    {
        // fireball at (10,5) heading east, wall at (12,5): straight then right (south)
        var s = NewState(new[] { (10, 5, (byte)0x47), (12, 5, (byte)0x01) });
        var m = s.Monsters.Single();
        s.MonsterTick();
        Assert.Equal((11, 5), (m.X, m.Y));
        s.MonsterTick();
        Assert.Equal((11, 6), (m.X, m.Y)); // turned right
    }

    [Fact]
    public void Glider_Turns_Left_When_Blocked()
    {
        var s = NewState(new[] { (10, 5, (byte)0x53), (11, 5, (byte)0x01) }); // glider E, wall E
        var m = s.Monsters.Single();
        s.MonsterTick();
        Assert.Equal((10, 4), (m.X, m.Y)); // turned left (north)
    }

    [Fact]
    public void Ball_Bounces_Back_And_Forth()
    {
        var s = NewState(new[] { (10, 5, (byte)0x4B), (11, 5, (byte)0x01), (8, 5, (byte)0x01) });
        var m = s.Monsters.Single();
        s.MonsterTick();
        Assert.Equal((9, 5), (m.X, m.Y)); // reversed west
        s.MonsterTick();
        Assert.Equal((10, 5), (m.X, m.Y)); // and back east
    }

    [Fact]
    public void Bug_Follows_Left_Wall()
    {
        // Bug heading north with a wall on its left (west): keeps going north.
        // When the wall opens, it turns left into the opening.
        var s = NewState(new[]
        {
            (10, 10, (byte)0x40),          // bug facing north
            (9, 10, (byte)0x01), (9, 9, (byte)0x01), // wall segment on its west
        });
        var m = s.Monsters.Single();
        s.MonsterTick();
        Assert.Equal((10, 9), (m.X, m.Y));
        s.MonsterTick();
        Assert.Equal((10, 8), (m.X, m.Y));
        s.MonsterTick();
        Assert.Equal((9, 8), (m.X, m.Y)); // wall ended; bug turns left
    }

    [Fact]
    public void Teeth_Chase_Chip_At_Half_Speed()
    {
        var s = NewState(new[] { (5, 10, (byte)0x56) }); // teeth south of Chip at (5,5)
        var m = s.Monsters.Single();
        s.MonsterTick();
        Assert.Equal((5, 9), (m.X, m.Y));  // moved toward Chip
        s.MonsterTick();
        Assert.Equal((5, 9), (m.X, m.Y));  // half speed: rests
        s.MonsterTick();
        Assert.Equal((5, 8), (m.X, m.Y));
    }

    [Fact]
    public void Inactive_Monsters_Do_Not_Move()
    {
        var s = NewState(new[] { (10, 5, (byte)0x47) }, inactive: new[] { (10, 5) });
        var m = s.Monsters.Single();
        s.MonsterTick();
        Assert.Equal((10, 5), (m.X, m.Y));
    }

    // ------------------------------------------------------------- death

    [Fact]
    public void Monster_Reaching_Chip_Kills()
    {
        var s = NewState(new[] { (7, 5, (byte)0x45) }); // fireball heading west, two tiles away
        Assert.Equal(MoveResult.Moved, s.MonsterTick());
        Assert.Equal(MoveResult.Died, s.MonsterTick());
        Assert.True(s.IsDead);
    }

    [Fact]
    public void Chip_Walking_Into_Monster_Dies()
    {
        var s = NewState(new[] { (6, 5, (byte)0x4B) });
        Assert.Equal(MoveResult.Died, s.TryMove(Direction.Right));
    }

    [Fact]
    public void Monsters_Drown_Except_Gliders()
    {
        var s = NewState(new[]
        {
            (10, 5, (byte)0x47), (11, 5, (byte)0x03),  // fireball into water
            (10, 8, (byte)0x53), (11, 8, (byte)0x03),  // glider into water
        });
        s.MonsterTick();
        Assert.True(s.Monsters.First(m => m.Type == ActorType.Fireball).Dead);
        Assert.False(s.Monsters.First(m => m.Type == ActorType.Glider).Dead);
    }

    [Fact]
    public void Fireballs_Survive_Fire_Others_Burn()
    {
        var s = NewState(new[]
        {
            (10, 5, (byte)0x47), (11, 5, (byte)0x04),
            (10, 8, (byte)0x4B), (11, 8, (byte)0x04),
        });
        s.MonsterTick();
        Assert.False(s.Monsters.First(m => m.Type == ActorType.Fireball).Dead);
        Assert.True(s.Monsters.First(m => m.Type == ActorType.Ball).Dead);
    }

    [Fact]
    public void Monsters_Blocked_By_Chips_And_Items()
    {
        var s = NewState(new[] { (10, 5, (byte)0x47), (11, 5, (byte)0x02), (10, 4, (byte)0x64) });
        var m = s.Monsters.Single();
        s.MonsterTick(); // chip tile blocks fwd, key blocks... fireball turns right
        Assert.Equal((10, 6), (m.X, m.Y));
    }

    // ------------------------------------------------------------- wiring

    [Fact]
    public void Blue_Button_Reverses_Tanks()
    {
        var s = NewState(new[] { (10, 5, (byte)0x4F), (6, 5, (byte)0x28) }); // tank E, blue button
        var m = s.Monsters.Single();
        s.MonsterTick();
        Assert.Equal((11, 5), (m.X, m.Y));
        s.TryMove(Direction.Right); // Chip presses the blue button
        s.MonsterTick();
        Assert.Equal((10, 5), (m.X, m.Y)); // heading west now
    }

    [Fact]
    public void Trap_Holds_Monster_Until_Button_Pressed()
    {
        var s = NewState(
            new[] { (10, 5, (byte)0x47), (11, 5, (byte)0x2B), (6, 5, (byte)0x27) },
            traps: new[] { (6, 5, 11, 5) }); // brown button at (6,5) wired to trap
        var m = s.Monsters.Single();
        s.MonsterTick();
        Assert.Equal((11, 5), (m.X, m.Y)); // walked into the trap
        s.MonsterTick();
        Assert.Equal((11, 5), (m.X, m.Y)); // held
        s.TryMove(Direction.Right);        // Chip stands on the button
        s.MonsterTick();
        Assert.Equal((12, 5), (m.X, m.Y)); // released
    }

    [Fact]
    public void Trap_Holds_Chip_Too()
    {
        var s = NewState(new[] { (6, 5, (byte)0x2B) }, traps: new[] { (20, 20, 6, 5) });
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Right));
        Assert.Equal(MoveResult.Blocked, s.TryMove(Direction.Left));
    }

    [Fact]
    public void Red_Button_Clones_From_Machine()
    {
        var s = NewState(
            new (int, int, byte)[] { (10, 5, 0x47), (6, 5, 0x24) },
            clones: new[] { (6, 5, 10, 5) },
            inactive: new[] { (10, 5) });
        // template fireball (east) sits on a clone machine, inactive
        Assert.Single(s.Monsters);
        s.TryMove(Direction.Right); // press red button
        Assert.Equal(2, s.Monsters.Count);
        var clone = s.Monsters[1];
        Assert.Equal((11, 5), (clone.X, clone.Y)); // spawned moving east
        Assert.True(clone.Active);
    }

    [Fact]
    public void Monster_Pressing_Button_Triggers_It()
    {
        // Fireball walks over a blue button; tank reverses.
        var s = NewState(new[]
        {
            (10, 5, (byte)0x47), (11, 5, (byte)0x28), // fireball E onto blue button
            (10, 8, (byte)0x4F),                      // tank E
        });
        var tank = s.Monsters.First(m => m.Type == ActorType.Tank);
        s.MonsterTick(); // fireball steps on button; tank (later in list) now flipped, moves W
        Assert.Equal((9, 8), (tank.X, tank.Y));
    }

    [Fact]
    public void Block_On_Button_Triggers_It()
    {
        var s = NewState(new[] { (6, 5, (byte)0x0A), (7, 5, (byte)0x23), (10, 5, (byte)0x25) });
        Assert.Equal(MoveResult.Moved, s.TryMove(Direction.Right)); // push block onto green button
        Assert.Equal(Tile.ToggleOpen, s.GetTile(10, 5));
    }

    // ------------------------------------------------------------- pathfinding

    [Fact]
    public void Pathfinder_Wont_Route_Through_Monsters()
    {
        var s = NewState(new[] { (6, 5, (byte)0x4B) }, inactive: new[] { (6, 5) });
        var path = Pathfinder.FindPath(s, 7, 5);
        Assert.NotNull(path);
        Assert.True(path!.Count > 2, "must route around the monster");
    }
}
