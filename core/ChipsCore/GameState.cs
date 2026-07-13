namespace ChipsCore;

public enum MoveResult
{
    Blocked,
    Moved,
    Won,
    Died,
}

/// <summary>
/// Complete state of a level in progress. The engine is deterministic:
/// state only changes through explicit calls (TryMove / SlideStep; a
/// tick-based Step() with monsters arrives in M3); no timers, no
/// wall-clock time, no randomness outside the level-seeded RNG.
///
/// M2 rule set: sliding (ice + corners, force floors), hazards
/// (water/fire/bomb) with boots, pushable blocks, thin-wall edges,
/// teleports, popup/appearing/fake walls, thief, green button toggles.
/// Fidelity notes: force-floor override here allows any perpendicular
/// move every step (MS is timing-based); ice bounce reverses direction.
/// Exact MS quirks get audited against TWS replays in M4.
/// </summary>
public sealed class GameState
{
    public const int Width = 32;
    public const int Height = 32;

    private readonly Tile[] _tiles = new Tile[Width * Height];
    private readonly HashSet<int> _blocks = new();
    private readonly List<int> _teleports = new();
    private readonly Random _rng;

    private readonly Dictionary<Tile, int> _keys = new()
    {
        [Tile.KeyRed] = 0,
        [Tile.KeyBlue] = 0,
        [Tile.KeyYellow] = 0,
        [Tile.KeyGreen] = 0,
    };

    private static readonly Dictionary<Tile, Tile> DoorToKey = new()
    {
        [Tile.DoorRed] = Tile.KeyRed,
        [Tile.DoorBlue] = Tile.KeyBlue,
        [Tile.DoorYellow] = Tile.KeyYellow,
        [Tile.DoorGreen] = Tile.KeyGreen,
    };

    public int ChipX { get; private set; }
    public int ChipY { get; private set; }
    public int ChipsRemaining { get; private set; }
    public bool Won { get; private set; }
    public bool IsDead { get; private set; }
    public string DeathReason { get; private set; } = "";
    public string Hint { get; private set; } = "";
    public Direction SlideDir { get; private set; } = Direction.None;
    public bool HasFlippers { get; private set; }
    public bool HasFireBoots { get; private set; }
    public bool HasSkates { get; private set; }
    public bool HasSuction { get; private set; }
    public int Tick { get; private set; }

    public GameState()
    {
        _rng = new Random(0);
    }

    public GameState(LevelData level)
    {
        _rng = new Random(level.Number);

        // Terrain comes from the top layer except where an actor (Chip,
        // monster, block) stands — the terrain under an actor is in the
        // bottom layer. Blocks are lifted into the block set.
        for (var i = 0; i < LevelData.LayerSize; i++)
        {
            var top = level.TopLayer[i];
            if (TileCodes.IsBlock(top)) _blocks.Add(i);
            _tiles[i] = TileCodes.ToTile(TileCodes.IsActor(top) ? level.BottomLayer[i] : top);
            if (_tiles[i] == Tile.Teleport) _teleports.Add(i);
        }

        (ChipX, ChipY) = level.FindChipStart();
        ChipsRemaining = level.ChipsRequired;
        Hint = level.Hint;
    }

    public Tile GetTile(int x, int y)
    {
        CheckBounds(x, y);
        return _tiles[y * Width + x];
    }

    public void SetTile(int x, int y, Tile tile)
    {
        CheckBounds(x, y);
        _tiles[y * Width + x] = tile;
    }

    public bool HasBlockAt(int x, int y) => InBounds(x, y) && _blocks.Contains(y * Width + x);

    public int GetKeyCount(Tile key) => _keys.GetValueOrDefault(key);

    /// <summary>Whether Chip is standing on a hint tile (HUD shows the hint).</summary>
    public bool OnHint => GetTile(ChipX, ChipY) == Tile.Hint;

    // ---------------------------------------------------------------- moves

    /// <summary>Player-initiated move. While sliding on ice there is no
    /// control; on a force floor only perpendicular moves are allowed.</summary>
    public MoveResult TryMove(Direction dir)
    {
        if (Won || IsDead || dir == Direction.None) return MoveResult.Blocked;

        // No control on ice. On force floors any direction may be
        // overridden — CCLP1 #2 requires stepping against the force to
        // reach the skates, and its hint says as much. (MS technically
        // allows this only on alternating ticks; M4 refines the timing.)
        if (SlideDir != Direction.None && IsIce(GetTile(ChipX, ChipY)))
            return MoveResult.Blocked;

        return Step(dir);
    }

    private bool _iceBounceFailed;

    /// <summary>One involuntary slide step; the shell calls this on a fast
    /// cadence while SlideDir is set. Hitting a wall on ice bounces Chip
    /// back the way he came (MS rule); blocked in both directions, he
    /// stops and regains control. On a force floor he stays pressed in
    /// place.</summary>
    public MoveResult SlideStep()
    {
        if (Won || IsDead || SlideDir == Direction.None) return MoveResult.Blocked;

        var result = Step(SlideDir);
        if (result == MoveResult.Blocked)
        {
            if (IsIce(GetTile(ChipX, ChipY)))
            {
                if (_iceBounceFailed)
                {
                    SlideDir = Direction.None; // stuck both ways: stop
                    _iceBounceFailed = false;
                }
                else
                {
                    SlideDir = Opposite(SlideDir);
                    _iceBounceFailed = true;
                }
            }
            // on force floors, keep pushing: SlideDir stays
        }
        else
        {
            _iceBounceFailed = false;
        }
        return result;
    }

    private MoveResult Step(Direction dir)
    {
        var (dx, dy) = dir.Delta();
        var toX = ChipX + dx;
        var toY = ChipY + dy;

        if (!CanCross(ChipX, ChipY, dir)) return MoveResult.Blocked;

        // Pushable block in the way?
        if (HasBlockAt(toX, toY) && !TryPushBlock(toX, toY, dir))
            return MoveResult.Blocked;

        var target = GetTile(toX, toY);
        switch (target)
        {
            case Tile.Wall or Tile.InvisibleWall or Tile.ToggleClosed or Tile.CloneMachine:
                return MoveResult.Blocked;

            case Tile.AppearingWall:
                SetTile(toX, toY, Tile.Wall);
                return MoveResult.Blocked;

            case Tile.FakeWall:
                SetTile(toX, toY, Tile.Floor);
                return MoveResult.Blocked;

            case Tile.Socket when ChipsRemaining > 0:
                return MoveResult.Blocked;
            case Tile.Socket:
                SetTile(toX, toY, Tile.Floor);
                break;

            case Tile.DoorRed or Tile.DoorBlue or Tile.DoorYellow or Tile.DoorGreen:
                var key = DoorToKey[target];
                if (_keys[key] == 0) return MoveResult.Blocked;
                if (key != Tile.KeyGreen) _keys[key]--;
                SetTile(toX, toY, Tile.Floor);
                break;

            case Tile.Chip:
                if (ChipsRemaining > 0) ChipsRemaining--;
                SetTile(toX, toY, Tile.Floor);
                break;

            case Tile.KeyRed or Tile.KeyBlue or Tile.KeyYellow or Tile.KeyGreen:
                _keys[target]++;
                SetTile(toX, toY, Tile.Floor);
                break;

            case Tile.BootsWater:
                HasFlippers = true;
                SetTile(toX, toY, Tile.Floor);
                break;
            case Tile.BootsFire:
                HasFireBoots = true;
                SetTile(toX, toY, Tile.Floor);
                break;
            case Tile.BootsIce:
                HasSkates = true;
                SetTile(toX, toY, Tile.Floor);
                break;
            case Tile.BootsForce:
                HasSuction = true;
                SetTile(toX, toY, Tile.Floor);
                break;

            case Tile.Dirt:
                SetTile(toX, toY, Tile.Floor);
                break;

            case Tile.Thief:
                HasFlippers = HasFireBoots = HasSkates = HasSuction = false;
                break;
        }

        // Leave the old tile (popup walls seal behind Chip).
        var from = GetTile(ChipX, ChipY);
        if (from == Tile.PopupWall) SetTile(ChipX, ChipY, Tile.Wall);

        ChipX = toX;
        ChipY = toY;
        Tick++;

        return EnterCurrentTile(dir);
    }

    /// <summary>Effects of standing on the tile Chip just entered:
    /// hazards, exit, teleport relay, button presses, and slide startup.</summary>
    private MoveResult EnterCurrentTile(Direction dir)
    {
        var here = GetTile(ChipX, ChipY);
        switch (here)
        {
            case Tile.Water when !HasFlippers:
                return Die("Chip can't swim without flippers!");
            case Tile.Fire when !HasFireBoots:
                return Die("Walking on fire needs fire boots!");
            case Tile.Bomb:
                SetTile(ChipX, ChipY, Tile.Floor);
                return Die("Don't step on the bombs!");
            case Tile.Exit:
                Won = true;
                SlideDir = Direction.None;
                return MoveResult.Won;
            case Tile.ButtonGreen:
                ToggleWalls();
                break;
            case Tile.Teleport:
                return TeleportChip(dir);
        }

        SlideDir = ComputeSlide(here, dir);
        return MoveResult.Moved;
    }

    private MoveResult Die(string reason)
    {
        IsDead = true;
        DeathReason = reason;
        SlideDir = Direction.None;
        return MoveResult.Died;
    }

    private void ToggleWalls()
    {
        for (var i = 0; i < _tiles.Length; i++)
        {
            _tiles[i] = _tiles[i] switch
            {
                Tile.ToggleOpen => Tile.ToggleClosed,
                Tile.ToggleClosed => Tile.ToggleOpen,
                var t => t,
            };
        }
    }

    /// <summary>MS teleports: destination is the previous teleport in
    /// reading order (wrapping), skipping any whose exit in the travel
    /// direction is blocked. If every destination is blocked, Chip stays
    /// put on the teleport he entered.</summary>
    private MoveResult TeleportChip(Direction dir)
    {
        var entered = ChipY * Width + ChipX;
        var enteredAt = _teleports.IndexOf(entered);
        for (var n = 1; n <= _teleports.Count; n++)
        {
            var candidate = _teleports[Mod(enteredAt - n, _teleports.Count)];
            var cx = candidate % Width;
            var cy = candidate / Width;
            var (dx, dy) = dir.Delta();
            if (!CanCross(cx, cy, dir)) continue;
            if (HasBlockAt(cx + dx, cy + dy)) continue;
            if (!IsChipEnterable(GetTile(cx + dx, cy + dy))) continue;

            ChipX = cx;
            ChipY = cy;
            return Step(dir); // exit the destination teleport, full effects
        }
        SlideDir = Direction.None;
        return MoveResult.Moved;
    }

    /// <summary>Rough enterability used for teleport exits (full rules run
    /// in the Step that follows) — anything that isn't a hard wall.</summary>
    private bool IsChipEnterable(Tile t) => t switch
    {
        Tile.Wall or Tile.InvisibleWall or Tile.AppearingWall or Tile.FakeWall
            or Tile.ToggleClosed or Tile.CloneMachine => false,
        Tile.Socket => ChipsRemaining == 0,
        Tile.DoorRed or Tile.DoorBlue or Tile.DoorYellow or Tile.DoorGreen
            => _keys[DoorToKey[t]] > 0,
        _ => true,
    };

    private Direction ComputeSlide(Tile here, Direction dir)
    {
        if (IsIce(here) && !HasSkates)
            return RedirectOnIce(here, dir);
        if (IsForce(here) && !HasSuction)
            return here switch
            {
                Tile.ForceN => Direction.Up,
                Tile.ForceS => Direction.Down,
                Tile.ForceE => Direction.Right,
                Tile.ForceW => Direction.Left,
                _ => (Direction)_rng.Next(1, 5), // ForceRandom
            };
        return Direction.None;
    }

    /// <summary>Ice corners curve the slide: e.g. the NW corner (walls on
    /// N and W) connects its S and E openings.</summary>
    private static Direction RedirectOnIce(Tile ice, Direction moving) => (ice, moving) switch
    {
        (Tile.IceNW, Direction.Up) => Direction.Right,
        (Tile.IceNW, Direction.Left) => Direction.Down,
        (Tile.IceNE, Direction.Up) => Direction.Left,
        (Tile.IceNE, Direction.Right) => Direction.Down,
        (Tile.IceSW, Direction.Down) => Direction.Right,
        (Tile.IceSW, Direction.Left) => Direction.Up,
        (Tile.IceSE, Direction.Down) => Direction.Left,
        (Tile.IceSE, Direction.Right) => Direction.Up,
        _ => moving,
    };

    // ---------------------------------------------------------------- blocks

    private bool TryPushBlock(int blockX, int blockY, Direction dir)
    {
        var (dx, dy) = dir.Delta();
        var destX = blockX + dx;
        var destY = blockY + dy;
        if (!CanCross(blockX, blockY, dir)) return false;
        if (HasBlockAt(destX, destY)) return false;
        if (!BlockCanRest(GetTile(destX, destY))) return false;

        _blocks.Remove(blockY * Width + blockX);
        switch (GetTile(destX, destY))
        {
            case Tile.Water:
                SetTile(destX, destY, Tile.Dirt); // block sinks, makes dirt
                break;
            case Tile.Bomb:
                SetTile(destX, destY, Tile.Floor); // both destroyed
                break;
            default:
                _blocks.Add(destY * Width + destX);
                break;
        }
        return true;
    }

    private static bool BlockCanRest(Tile t) => t switch
    {
        Tile.Floor or Tile.Hint or Tile.Gravel or Tile.Water or Tile.Fire or Tile.Bomb
            or Tile.Trap or Tile.ToggleOpen
            or Tile.ButtonGreen or Tile.ButtonRed or Tile.ButtonBrown or Tile.ButtonBlue
            or Tile.Ice or Tile.IceNW or Tile.IceNE or Tile.IceSW or Tile.IceSE
            or Tile.ForceN or Tile.ForceS or Tile.ForceE or Tile.ForceW or Tile.ForceRandom
            or Tile.ThinN or Tile.ThinW or Tile.ThinS or Tile.ThinE or Tile.ThinSE
            => true,
        _ => false,
    };

    // ------------------------------------------------------------ edges/geometry

    /// <summary>Can something cross the edge between (x,y) and its neighbor
    /// in direction dir? Checks bounds plus thin-wall/ice-corner edges on
    /// both sides. Does not check the destination tile's own rules.</summary>
    public bool CanCross(int x, int y, Direction dir)
    {
        var (dx, dy) = dir.Delta();
        var nx = x + dx;
        var ny = y + dy;
        if (!InBounds(nx, ny)) return false;
        if (EdgeWall(GetTile(x, y), dir)) return false;
        if (EdgeWall(GetTile(nx, ny), Opposite(dir))) return false;
        return true;
    }

    /// <summary>Does this tile have a wall on the given edge?</summary>
    private static bool EdgeWall(Tile t, Direction edge) => (t, edge) switch
    {
        (Tile.ThinN, Direction.Up) => true,
        (Tile.ThinW, Direction.Left) => true,
        (Tile.ThinS, Direction.Down) => true,
        (Tile.ThinE, Direction.Right) => true,
        (Tile.ThinSE, Direction.Down or Direction.Right) => true,
        (Tile.IceNW, Direction.Up or Direction.Left) => true,
        (Tile.IceNE, Direction.Up or Direction.Right) => true,
        (Tile.IceSW, Direction.Down or Direction.Left) => true,
        (Tile.IceSE, Direction.Down or Direction.Right) => true,
        _ => false,
    };

    // ---------------------------------------------------------------- pathfinding view

    /// <summary>Whether tap-to-move may plan a route through (x,y): no
    /// walls or blocks, nothing lethal without the right boots, and no
    /// tiles that would take control away (slides, teleports).</summary>
    public bool PathSafe(int x, int y)
    {
        if (!InBounds(x, y) || HasBlockAt(x, y)) return false;
        var t = GetTile(x, y);
        return t switch
        {
            Tile.Wall or Tile.InvisibleWall or Tile.AppearingWall or Tile.FakeWall
                or Tile.ToggleClosed or Tile.CloneMachine => false,
            Tile.Water => HasFlippers,
            Tile.Fire => HasFireBoots,
            Tile.Bomb => false,
            Tile.Ice or Tile.IceNW or Tile.IceNE or Tile.IceSW or Tile.IceSE => HasSkates,
            Tile.ForceN or Tile.ForceS or Tile.ForceE or Tile.ForceW or Tile.ForceRandom
                => HasSuction,
            Tile.Teleport => false,
            Tile.Socket => ChipsRemaining == 0,
            Tile.DoorRed or Tile.DoorBlue or Tile.DoorYellow or Tile.DoorGreen
                => _keys[DoorToKey[t]] > 0,
            _ => true,
        };
    }

    // ---------------------------------------------------------------- helpers

    public static bool IsIce(Tile t) =>
        t is Tile.Ice or Tile.IceNW or Tile.IceNE or Tile.IceSW or Tile.IceSE;

    public static bool IsForce(Tile t) =>
        t is Tile.ForceN or Tile.ForceS or Tile.ForceE or Tile.ForceW or Tile.ForceRandom;

    private static bool InBounds(int x, int y) => x is >= 0 and < Width && y is >= 0 and < Height;

    private static Direction Opposite(Direction d) => d switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => Direction.None,
    };

    private static int Mod(int a, int m) => ((a % m) + m) % m;

    private static void CheckBounds(int x, int y)
    {
        if (!InBounds(x, y))
            throw new ArgumentOutOfRangeException($"({x},{y}) is outside the {Width}x{Height} grid");
    }
}
