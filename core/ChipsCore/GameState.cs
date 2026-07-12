namespace ChipsCore;

public enum MoveResult
{
    Blocked,
    Moved,
    Won,
}

/// <summary>
/// Complete state of a level in progress. The engine is deterministic:
/// state only changes through explicit calls (TryMove now; a tick-based
/// Step() arrives with monsters in M3); no timers, no wall-clock time,
/// no randomness outside the seeded RNG the MS ruleset defines.
/// </summary>
public sealed class GameState
{
    public const int Width = 32;
    public const int Height = 32;

    private readonly Tile[] _tiles = new Tile[Width * Height];
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
    public string Hint { get; private set; } = "";
    public int Tick { get; private set; }

    public GameState() { }

    public GameState(LevelData level)
    {
        // Effective terrain: the top layer, except where an actor stands —
        // the terrain under an actor is in the bottom layer.
        for (var i = 0; i < LevelData.LayerSize; i++)
        {
            var code = TileCodes.IsActor(level.TopLayer[i])
                ? level.BottomLayer[i]
                : level.TopLayer[i];
            _tiles[i] = TileCodes.ToTile(code);
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

    public int GetKeyCount(Tile key) => _keys.GetValueOrDefault(key);

    /// <summary>Whether Chip is standing on a hint tile (HUD shows the hint).</summary>
    public bool OnHint => GetTile(ChipX, ChipY) == Tile.Hint;

    /// <summary>
    /// Pathfinding view of enterability given current inventory; no side
    /// effects. Mirrors TryMove's blocking rules except fake walls, which
    /// path as blocked — revealing them should be a deliberate bump, not a
    /// pathfinder side trip.
    /// </summary>
    public bool CanEnter(int x, int y)
    {
        if (x is < 0 or >= Width || y is < 0 or >= Height) return false;
        var tile = GetTile(x, y);
        return tile switch
        {
            Tile.Wall or Tile.Block or Tile.ToggleWall or Tile.CloneMachine or Tile.FakeWall
                => false,
            Tile.Socket => ChipsRemaining == 0,
            Tile.DoorRed or Tile.DoorBlue or Tile.DoorYellow or Tile.DoorGreen
                => _keys[DoorToKey[tile]] > 0,
            _ => true,
        };
    }

    /// <summary>
    /// M1 movement: walls and wall-likes block, chips/keys/boots are picked
    /// up, doors consume keys (green keys are never consumed, as in MS),
    /// the socket opens once all chips are collected, exit wins. Hazards
    /// (water, fire, monsters) and sliding (ice, force floors) arrive in
    /// M2/M3.
    /// </summary>
    public MoveResult TryMove(Direction dir)
    {
        if (Won) return MoveResult.Blocked;

        var (dx, dy) = dir.Delta();
        var x = ChipX + dx;
        var y = ChipY + dy;
        if (x is < 0 or >= Width || y is < 0 or >= Height)
            return MoveResult.Blocked;

        var target = GetTile(x, y);
        switch (target)
        {
            case Tile.Wall or Tile.Block or Tile.ToggleWall or Tile.CloneMachine:
                return MoveResult.Blocked;

            case Tile.FakeWall:
                // Fake blue wall: bumping reveals it (it vanishes) but the
                // bump itself does not move Chip.
                SetTile(x, y, Tile.Floor);
                return MoveResult.Blocked;

            case Tile.Socket when ChipsRemaining > 0:
                return MoveResult.Blocked;
            case Tile.Socket:
                SetTile(x, y, Tile.Floor);
                break;

            case Tile.DoorRed or Tile.DoorBlue or Tile.DoorYellow or Tile.DoorGreen:
                var key = DoorToKey[target];
                if (_keys[key] == 0) return MoveResult.Blocked;
                if (key != Tile.KeyGreen) _keys[key]--;
                SetTile(x, y, Tile.Floor);
                break;

            case Tile.Chip:
                if (ChipsRemaining > 0) ChipsRemaining--;
                SetTile(x, y, Tile.Floor);
                break;

            case Tile.KeyRed or Tile.KeyBlue or Tile.KeyYellow or Tile.KeyGreen:
                _keys[target]++;
                SetTile(x, y, Tile.Floor);
                break;

            case Tile.BootsWater or Tile.BootsFire or Tile.BootsIce or Tile.BootsForce:
                // Inventory arrives in M2; for now boots are just collected.
                SetTile(x, y, Tile.Floor);
                break;
        }

        ChipX = x;
        ChipY = y;
        if (target == Tile.Exit) Won = true;
        return Won ? MoveResult.Won : MoveResult.Moved;
    }

    private static void CheckBounds(int x, int y)
    {
        if (x is < 0 or >= Width || y is < 0 or >= Height)
            throw new ArgumentOutOfRangeException($"({x},{y}) is outside the {Width}x{Height} grid");
    }
}
