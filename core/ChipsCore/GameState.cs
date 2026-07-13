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
/// state only changes through explicit calls (TryMove / SlideStep /
/// MonsterTick); no timers, no wall-clock time, no randomness outside the
/// level-seeded RNG.
///
/// M3 rule set adds monsters (per-species AI, monster-list ordering,
/// contact death, hazard deaths, sliding) and button wiring: brown
/// buttons hold traps open while pressed, red buttons clone from clone
/// machines, blue buttons reverse tanks. Fidelity notes: force-floor
/// override timing, monster slip speed, and teeth tie-breaking are
/// approximations to be audited against TWS replays in M4.
/// </summary>
public sealed class GameState
{
    public const int Width = 32;
    public const int Height = 32;

    private readonly Tile[] _tiles = new Tile[Width * Height];
    private readonly Dictionary<int, Direction> _blocks = new(); // pos -> facing (clone blocks)
    private readonly List<int> _teleports = new();
    private readonly List<Actor> _monsters = new();
    private readonly Dictionary<int, Actor> _monsterAt = new();
    private readonly List<(int Button, int Target)> _trapWiring = new();
    private readonly List<(int Button, int Target)> _cloneWiring = new();
    private readonly Random _rng;
    private bool _monsterParity = true; // flipped at the start of each tick
    private bool _iceBounceFailed;

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

    public IReadOnlyList<Actor> Monsters => _monsters;

    public GameState()
    {
        _rng = new Random(0);
    }

    public GameState(LevelData level)
    {
        _rng = new Random(level.Number);

        // Terrain comes from the top layer except where an actor (Chip,
        // monster, block) stands — the terrain under an actor is in the
        // bottom layer. Blocks and monsters are lifted into entity lists.
        for (var i = 0; i < LevelData.LayerSize; i++)
        {
            var top = level.TopLayer[i];
            if (TileCodes.IsBlock(top))
            {
                var facing = top is >= 0x0E and <= 0x11
                    ? (Direction)((top - 0x0E) switch { 0 => 1, 1 => 2, 2 => 3, _ => 4 })
                    : Direction.None;
                _blocks[i] = facing;
            }
            else if (TileCodes.IsMonster(top))
            {
                var actor = Actor.FromCode(top, i % Width, i / Width);
                _monsters.Add(actor);
                _monsterAt[i] = actor;
            }
            _tiles[i] = TileCodes.ToTile(TileCodes.IsActor(top) ? level.BottomLayer[i] : top);
            if (_tiles[i] == Tile.Teleport) _teleports.Add(i);
        }

        // Only monsters on the level's monster list move (MS rule).
        foreach (var (mx, my) in level.MonsterList)
        {
            if (_monsterAt.TryGetValue(my * Width + mx, out var actor))
                actor.Active = true;
        }

        ParseWiring(level.TrapWiring, 10, _trapWiring);
        ParseWiring(level.CloneWiring, 8, _cloneWiring);

        (ChipX, ChipY) = level.FindChipStart();
        ChipsRemaining = level.ChipsRequired;
        Hint = level.Hint;
    }

    private static void ParseWiring(byte[] raw, int stride, List<(int, int)> into)
    {
        for (var i = 0; i + 7 < raw.Length; i += stride)
        {
            int bx = raw[i] | (raw[i + 1] << 8);
            int by = raw[i + 2] | (raw[i + 3] << 8);
            int tx = raw[i + 4] | (raw[i + 5] << 8);
            int ty = raw[i + 6] | (raw[i + 7] << 8);
            if (bx < Width && by < Height && tx < Width && ty < Height)
                into.Add((by * Width + bx, ty * Width + tx));
        }
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

    public bool HasBlockAt(int x, int y) => InBounds(x, y) && _blocks.ContainsKey(y * Width + x);

    public int GetKeyCount(Tile key) => _keys.GetValueOrDefault(key);

    /// <summary>Whether Chip is standing on a hint tile (HUD shows the hint).</summary>
    public bool OnHint => GetTile(ChipX, ChipY) == Tile.Hint;

    /// <summary>Any live, moving monster within taxicab distance of Chip —
    /// used by the shell to halt tap-to-move autowalking near danger.</summary>
    public bool MonsterNear(int distance) => _monsters.Any(m =>
        m is { Dead: false, Active: true }
        && Math.Abs(m.X - ChipX) + Math.Abs(m.Y - ChipY) <= distance);

    // ---------------------------------------------------------------- Chip moves

    /// <summary>Player-initiated move. While sliding on ice there is no
    /// control; force floors can be overridden in any direction (MS allows
    /// this on alternating ticks; M4 refines the timing).</summary>
    public MoveResult TryMove(Direction dir)
    {
        if (Won || IsDead || dir == Direction.None) return MoveResult.Blocked;

        if (SlideDir != Direction.None && IsIce(GetTile(ChipX, ChipY)))
            return MoveResult.Blocked;

        return Step(dir);
    }

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
        var fromIdx = ChipY * Width + ChipX;
        if (GetTile(ChipX, ChipY) == Tile.Trap && !IsTrapOpen(fromIdx))
            return MoveResult.Blocked;

        var (dx, dy) = dir.Delta();
        var toX = ChipX + dx;
        var toY = ChipY + dy;

        if (!CanCross(ChipX, ChipY, dir)) return MoveResult.Blocked;

        var toIdx = toY * Width + toX;
        if (_monsterAt.TryGetValue(toIdx, out var monster) && !monster.Dead)
            return Die("Look out for creatures!");

        // Pushable block in the way?
        if (_blocks.ContainsKey(toIdx) && !TryPushBlock(toX, toY, dir))
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
        if (GetTile(ChipX, ChipY) == Tile.PopupWall) SetTile(ChipX, ChipY, Tile.Wall);

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
            case Tile.ButtonGreen or Tile.ButtonRed or Tile.ButtonBlue or Tile.ButtonBrown:
                PressButton(ChipY * Width + ChipX);
                break;
            case Tile.Teleport:
                return TeleportChip(dir);
        }

        SlideDir = ComputeSlide(here, dir, hasSkates: HasSkates, hasSuction: HasSuction);
        return MoveResult.Moved;
    }

    private MoveResult Die(string reason)
    {
        IsDead = true;
        DeathReason = reason;
        SlideDir = Direction.None;
        return MoveResult.Died;
    }

    // ---------------------------------------------------------------- buttons/wiring

    private void PressButton(int idx)
    {
        switch (_tiles[idx])
        {
            case Tile.ButtonGreen:
                for (var i = 0; i < _tiles.Length; i++)
                {
                    _tiles[i] = _tiles[i] switch
                    {
                        Tile.ToggleOpen => Tile.ToggleClosed,
                        Tile.ToggleClosed => Tile.ToggleOpen,
                        var t => t,
                    };
                }
                break;

            case Tile.ButtonBlue:
                foreach (var tank in _monsters.Where(m => m is { Type: ActorType.Tank, Dead: false }))
                    tank.Dir = Opposite(tank.Dir);
                break;

            case Tile.ButtonRed:
                foreach (var (button, machine) in _cloneWiring)
                {
                    if (button == idx) Clone(machine);
                }
                break;

            // Brown buttons need no action: traps are open while any
            // wired button is held down (see IsTrapOpen).
        }
    }

    /// <summary>Spawn a copy of whatever sits on the clone machine, moving
    /// in its facing direction. No spawn if the way out is blocked.</summary>
    private void Clone(int machineIdx)
    {
        var mx = machineIdx % Width;
        var my = machineIdx / Width;

        if (_monsterAt.TryGetValue(machineIdx, out var template) && !template.Dead)
        {
            var clone = new Actor
            {
                Type = template.Type, X = mx, Y = my, Dir = template.Dir, Active = true,
            };
            if (TryActorStep(clone, clone.Dir))
            {
                _monsters.Add(clone);
                if (!clone.Dead) _monsterAt[clone.Y * Width + clone.X] = clone;
            }
            return;
        }

        if (_blocks.TryGetValue(machineIdx, out var facing) && facing != Direction.None)
        {
            var (dx, dy) = facing.Delta();
            var toX = mx + dx;
            var toY = my + dy;
            if (CanCross(mx, my, facing) && !HasBlockAt(toX, toY)
                && !_monsterAt.ContainsKey(toY * Width + toX)
                && BlockCanRest(GetTile(toX, toY)))
            {
                SettleBlock(toX, toY, facing);
            }
        }
    }

    private bool IsTrapOpen(int trapIdx) => _trapWiring.Any(w =>
        w.Target == trapIdx && IsOccupied(w.Button));

    private bool IsOccupied(int idx) =>
        idx == ChipY * Width + ChipX
        || _blocks.ContainsKey(idx)
        || (_monsterAt.TryGetValue(idx, out var m) && !m.Dead);

    // ---------------------------------------------------------------- monsters

    /// <summary>Advance every active monster one step, in monster-list
    /// order. Teeth and blobs move on alternate ticks. Returns Died when a
    /// monster reaches Chip.</summary>
    public MoveResult MonsterTick()
    {
        if (Won || IsDead) return MoveResult.Blocked;

        _monsterParity = !_monsterParity;
        foreach (var m in _monsters.ToList())
        {
            if (m.Dead || !m.Active) continue;
            if (m.Type is ActorType.Teeth or ActorType.Blob && _monsterParity) continue;
            if (GetTile(m.X, m.Y) == Tile.Trap && !IsTrapOpen(m.Y * Width + m.X)) continue;

            if (m.SlideDir != Direction.None)
            {
                if (!TryActorStep(m, m.SlideDir))
                {
                    if (IsIce(GetTile(m.X, m.Y)))
                    {
                        m.SlideDir = Opposite(m.SlideDir);
                        if (!TryActorStep(m, m.SlideDir)) m.SlideDir = Direction.None;
                    }
                    // force floors keep pressing, like Chip
                }
            }
            else
            {
                foreach (var dir in ChooseDirections(m))
                {
                    if (TryActorStep(m, dir)) break;
                    if (m.Type == ActorType.Tank) break; // tanks wait, never turn
                }
            }

            if (IsDead) return MoveResult.Died;
        }
        return MoveResult.Moved;
    }

    private Direction[] ChooseDirections(Actor m)
    {
        var f = m.Dir;
        var l = TurnLeft(f);
        var r = TurnRight(f);
        var b = Opposite(f);
        switch (m.Type)
        {
            case ActorType.Bug: return new[] { l, f, r, b };
            case ActorType.Paramecium: return new[] { r, f, l, b };
            case ActorType.Fireball: return new[] { f, r, l, b };
            case ActorType.Glider: return new[] { f, l, r, b };
            case ActorType.Ball: return new[] { f, b };
            case ActorType.Tank: return new[] { f };
            case ActorType.Walker:
                // straight, else a random new heading
                var others = new[] { l, r, b }.OrderBy(_ => _rng.Next()).ToArray();
                return new[] { f, others[0], others[1], others[2] };
            case ActorType.Blob:
                return new[] { (Direction)_rng.Next(1, 5) };
            case ActorType.Teeth:
                var dx = ChipX - m.X;
                var dy = ChipY - m.Y;
                var vertical = dy > 0 ? Direction.Down : Direction.Up;
                var horizontal = dx > 0 ? Direction.Right : Direction.Left;
                if (dx == 0) return new[] { vertical };
                if (dy == 0) return new[] { horizontal };
                return Math.Abs(dy) >= Math.Abs(dx)
                    ? new[] { vertical, horizontal }
                    : new[] { horizontal, vertical };
            default: return new[] { f };
        }
    }

    /// <summary>Try to move a monster one tile. Handles Chip contact,
    /// hazard deaths, buttons, traps, teleports, and slide startup.
    /// Updates facing on success.</summary>
    private bool TryActorStep(Actor m, Direction dir)
    {
        var (dx, dy) = dir.Delta();
        var toX = m.X + dx;
        var toY = m.Y + dy;
        if (!CanCross(m.X, m.Y, dir)) return false;

        var toIdx = toY * Width + toX;
        if (_blocks.ContainsKey(toIdx)) return false;
        if (_monsterAt.TryGetValue(toIdx, out var other) && !other.Dead) return false;

        var target = GetTile(toX, toY);
        if (toX == ChipX && toY == ChipY)
        {
            MoveActor(m, toX, toY, dir);
            Die("Look out for creatures!");
            return true;
        }
        if (!MonsterCanEnter(m.Type, target)) return false;

        MoveActor(m, toX, toY, dir);

        switch (target)
        {
            case Tile.Water when m.Type != ActorType.Glider:
            case Tile.Fire when m.Type != ActorType.Fireball:
                KillActor(m);
                return true;
            case Tile.Bomb:
                KillActor(m);
                SetTile(toX, toY, Tile.Floor);
                return true;
            case Tile.ButtonGreen or Tile.ButtonRed or Tile.ButtonBlue or Tile.ButtonBrown:
                PressButton(toIdx);
                break;
            case Tile.Teleport:
                TeleportActor(m, dir);
                break;
        }

        m.SlideDir = ComputeSlide(GetTile(m.X, m.Y), dir, hasSkates: false, hasSuction: false);
        return true;
    }

    private void MoveActor(Actor m, int toX, int toY, Direction dir)
    {
        _monsterAt.Remove(m.Y * Width + m.X);
        m.X = toX;
        m.Y = toY;
        m.Dir = dir;
        _monsterAt[toY * Width + toX] = m;
    }

    private void KillActor(Actor m)
    {
        m.Dead = true;
        _monsterAt.Remove(m.Y * Width + m.X);
    }

    private void TeleportActor(Actor m, Direction dir)
    {
        var entered = m.Y * Width + m.X;
        var enteredAt = _teleports.IndexOf(entered);
        if (enteredAt < 0) return;
        for (var n = 1; n <= _teleports.Count; n++)
        {
            var candidate = _teleports[Mod(enteredAt - n, _teleports.Count)];
            var cx = candidate % Width;
            var cy = candidate / Width;
            var (dx, dy) = dir.Delta();
            var outIdx = (cy + dy) * Width + (cx + dx);
            if (candidate != entered && _monsterAt.ContainsKey(candidate)) continue;
            if (!CanCross(cx, cy, dir)) continue;
            if (_blocks.ContainsKey(outIdx) || _monsterAt.ContainsKey(outIdx)) continue;
            if (!MonsterCanEnter(m.Type, _tiles[outIdx]) && !(cx + dx == ChipX && cy + dy == ChipY)) continue;

            MoveActor(m, cx, cy, dir);
            TryActorStep(m, dir);
            return;
        }
    }

    private static bool MonsterCanEnter(ActorType type, Tile t) => t switch
    {
        Tile.Floor or Tile.Water or Tile.Fire or Tile.Bomb or Tile.Trap or Tile.Teleport
            or Tile.ToggleOpen
            or Tile.ButtonGreen or Tile.ButtonRed or Tile.ButtonBrown or Tile.ButtonBlue
            or Tile.Ice or Tile.IceNW or Tile.IceNE or Tile.IceSW or Tile.IceSE
            or Tile.ForceN or Tile.ForceS or Tile.ForceE or Tile.ForceW or Tile.ForceRandom
            or Tile.ThinN or Tile.ThinW or Tile.ThinS or Tile.ThinE or Tile.ThinSE
            => true,
        _ => false, // walls, items, chips, doors, dirt, gravel, hint, exit, thief...
    };

    // ---------------------------------------------------------------- sliding

    private Direction ComputeSlide(Tile here, Direction dir, bool hasSkates, bool hasSuction)
    {
        if (IsIce(here) && !hasSkates)
            return RedirectOnIce(here, dir);
        if (IsForce(here) && !hasSuction)
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

    // ---------------------------------------------------------------- teleports (Chip)

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
            if (_monsterAt.ContainsKey(candidate)) continue;
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

    // ---------------------------------------------------------------- blocks

    private bool TryPushBlock(int blockX, int blockY, Direction dir)
    {
        var blockIdx = blockY * Width + blockX;
        if (GetTile(blockX, blockY) == Tile.Trap && !IsTrapOpen(blockIdx)) return false;

        var (dx, dy) = dir.Delta();
        var destX = blockX + dx;
        var destY = blockY + dy;
        if (!CanCross(blockX, blockY, dir)) return false;
        if (HasBlockAt(destX, destY)) return false;
        if (_monsterAt.TryGetValue(destY * Width + destX, out var m) && !m.Dead) return false;
        if (!BlockCanRest(GetTile(destX, destY))) return false;

        _blocks.Remove(blockIdx);
        SettleBlock(destX, destY, Direction.None);
        return true;
    }

    /// <summary>Land a block on a tile, resolving water/bombs/buttons.</summary>
    private void SettleBlock(int x, int y, Direction facing)
    {
        var idx = y * Width + x;
        switch (GetTile(x, y))
        {
            case Tile.Water:
                SetTile(x, y, Tile.Dirt); // block sinks, makes dirt
                break;
            case Tile.Bomb:
                SetTile(x, y, Tile.Floor); // both destroyed
                break;
            case Tile.ButtonGreen or Tile.ButtonRed or Tile.ButtonBlue or Tile.ButtonBrown:
                _blocks[idx] = facing;
                PressButton(idx);
                break;
            default:
                _blocks[idx] = facing;
                break;
        }
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
    /// walls, blocks, or monsters, nothing lethal without the right boots,
    /// and no tiles that would take control away (slides, teleports).</summary>
    public bool PathSafe(int x, int y)
    {
        if (!InBounds(x, y) || HasBlockAt(x, y)) return false;
        if (_monsterAt.TryGetValue(y * Width + x, out var m) && !m.Dead) return false;
        var t = GetTile(x, y);
        return t switch
        {
            Tile.Wall or Tile.InvisibleWall or Tile.AppearingWall or Tile.FakeWall
                or Tile.ToggleClosed or Tile.CloneMachine => false,
            Tile.Water => HasFlippers,
            Tile.Fire => HasFireBoots,
            Tile.Bomb => false,
            Tile.Trap => false,
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

    private static Direction TurnLeft(Direction d) => d switch
    {
        Direction.Up => Direction.Left,
        Direction.Left => Direction.Down,
        Direction.Down => Direction.Right,
        Direction.Right => Direction.Up,
        _ => Direction.None,
    };

    private static Direction TurnRight(Direction d) => Opposite(TurnLeft(d));

    private static int Mod(int a, int m) => ((a % m) + m) % m;

    private static void CheckBounds(int x, int y)
    {
        if (!InBounds(x, y))
            throw new ArgumentOutOfRangeException($"({x},{y}) is outside the {Width}x{Height} grid");
    }
}
