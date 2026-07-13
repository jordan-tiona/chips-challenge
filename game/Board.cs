using System.Collections.Generic;
using System.Linq;
using ChipsCore;
using Godot;

namespace ChipsChallenge;

/// <summary>
/// Draws the 32x32 grid from the engine's GameState with placeholder
/// colors and glyphs (proper tileset art is M5). Never mutates state
/// except by forwarding TryMove/SlideStep.
/// </summary>
public partial class Board : Node2D
{
    public const int TileSize = 32;

    public GameState? State { get; private set; }

    private readonly Font _font = ThemeDB.FallbackFont;

    // Visual positions chase the true grid positions each frame so motion
    // glides while the engine stays perfectly discrete. Each entity's
    // visual speed equals its true movement rate, so consecutive steps
    // chain into constant velocity (fully continuous motion, up to ~1
    // tile behind the lethal truth — the Lynx look). Falling further
    // behind triggers catch-up; jumps beyond 1.6 tiles (teleports) snap.
    private const float SnapDistance = TileSize * 2.5f; // true teleports only
    private const float CatchUpDistance = TileSize * 1.1f;
    private Vector2 _chipVisual;
    private readonly Dictionary<Actor, Vector2> _monsterVisual = new();
    private readonly Dictionary<int, Vector2> _blockVisual = new();

    public void LoadLevel(LevelData level)
    {
        State = new GameState(level);
        _monsterVisual.Clear();
        _blockVisual.Clear();
        _chipVisual = ChipPixelCenter;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (State == null) return;

        var chipRate = State.SlideDir != Direction.None ? 10f : 5f;
        _chipVisual = Chase(_chipVisual, ChipPixelCenter, (float)delta, chipRate);
        foreach (var m in State.Monsters)
        {
            if (m.Dead) { _monsterVisual.Remove(m); continue; }
            var rate = m.SlideDir != Direction.None ? 10f
                : m.Type is ActorType.Teeth or ActorType.Blob ? 2.5f : 5f;
            var target = new Vector2(m.X * TileSize + TileSize / 2f, m.Y * TileSize + TileSize / 2f);
            _monsterVisual[m] = Chase(_monsterVisual.GetValueOrDefault(m, target), target, (float)delta, rate);
        }
        var seen = new HashSet<int>();
        foreach (var (id, x, y) in State.BlockPositions)
        {
            seen.Add(id);
            var target = new Vector2(x * TileSize, y * TileSize);
            _blockVisual[id] = Chase(_blockVisual.GetValueOrDefault(id, target), target, (float)delta, 10f);
        }
        foreach (var gone in _blockVisual.Keys.Where(k => !seen.Contains(k)).ToList())
            _blockVisual.Remove(gone);

        QueueRedraw();
    }

    /// <summary>Move toward target at the entity's true tiles/second (so
    /// chained steps read as continuous motion), sprinting to catch up if
    /// the visual falls more than a step behind.</summary>
    private static Vector2 Chase(Vector2 current, Vector2 target, float delta, float tilesPerSecond)
    {
        delta = Mathf.Min(delta, 0.1f); // hitch guard, mirrors the shell
        var distance = current.DistanceTo(target);
        if (distance > SnapDistance) return target;
        var speed = TileSize * tilesPerSecond * 1.08f;
        if (distance > CatchUpDistance)
            speed = Mathf.Max(speed * 2f, distance / 0.12f); // sprint, don't teleport
        return current.MoveToward(target, speed * delta);
    }

    /// <summary>One tick of the engine (20/second); everything — Chip's
    /// input, monsters, slides — advances inside GameState.Advance.</summary>
    public MoveResult Advance(Direction input)
    {
        if (State == null) return MoveResult.Blocked;
        var result = State.Advance(input);
        QueueRedraw();
        return result;
    }

    public Vector2 ChipPixelCenter => State == null
        ? Vector2.Zero
        : new Vector2(
            State.ChipX * TileSize + TileSize / 2f,
            State.ChipY * TileSize + TileSize / 2f);

    public override void _Draw()
    {
        if (State == null) return;

        for (var y = 0; y < GameState.Height; y++)
        {
            for (var x = 0; x < GameState.Width; x++)
            {
                var tile = State.GetTile(x, y);
                var origin = new Vector2(x * TileSize, y * TileSize);
                // Traps telegraph their state: red = armed, green = held open.
                var bg = tile == Tile.Trap
                    ? (State.IsTrapOpenAt(x, y) ? new Color("2e5a38") : new Color("5e3434"))
                    : Background(tile);
                DrawRect(new Rect2(origin, TileSize, TileSize), bg);

                var glyph = Glyph(tile);
                if (glyph.Length > 0)
                    DrawString(_font, origin + new Vector2(0, 23), glyph,
                        HorizontalAlignment.Center, TileSize, 18, GlyphColor(tile));

                DrawEdgeWalls(origin, tile);
            }
        }

        foreach (var pos in _blockVisual.Values)
        {
            DrawRect(new Rect2(pos + new Vector2(3, 3), TileSize - 6, TileSize - 6),
                new Color("8a6a42"));
            DrawRect(new Rect2(pos + new Vector2(3, 3), TileSize - 6, TileSize - 6),
                new Color("5a4225"), filled: false, width: 2);
        }

        DrawRect(new Rect2(0, 0, GameState.Width * TileSize, GameState.Height * TileSize),
            new Color(1, 1, 1, 0.25f), filled: false, width: 2);

        foreach (var m in State.Monsters)
        {
            if (m.Dead || !_monsterVisual.TryGetValue(m, out var c)) continue;
            DrawCircle(c, 11, MonsterColor(m.Type));
            DrawString(_font, new Vector2(c.X - TileSize / 2f, c.Y + 7),
                MonsterGlyph(m.Type), HorizontalAlignment.Center, TileSize, 16,
                new Color("14141c"));
        }

        // Chip
        DrawCircle(_chipVisual, 12, new Color("ffd75e"));
        DrawArc(_chipVisual, 12, 0, Mathf.Tau, 24, new Color("1a1a2e"), 2, antialiased: true);
    }

    /// <summary>Thin walls and ice-corner walls drawn as bright edge lines.</summary>
    private void DrawEdgeWalls(Vector2 o, Tile tile)
    {
        var c = new Color(0.9f, 0.9f, 0.95f);
        const float w = 3f;
        var (n, s, e, west) = tile switch
        {
            Tile.ThinN => (true, false, false, false),
            Tile.ThinS => (false, true, false, false),
            Tile.ThinE => (false, false, true, false),
            Tile.ThinW => (false, false, false, true),
            Tile.ThinSE => (false, true, true, false),
            Tile.IceNW => (true, false, false, true),
            Tile.IceNE => (true, false, true, false),
            Tile.IceSW => (false, true, false, true),
            Tile.IceSE => (false, true, true, false),
            _ => (false, false, false, false),
        };
        if (n) DrawLine(o, o + new Vector2(TileSize, 0), c, w);
        if (s) DrawLine(o + new Vector2(0, TileSize), o + new Vector2(TileSize, TileSize), c, w);
        if (e) DrawLine(o + new Vector2(TileSize, 0), o + new Vector2(TileSize, TileSize), c, w);
        if (west) DrawLine(o, o + new Vector2(0, TileSize), c, w);
    }

    private static Color MonsterColor(ActorType type) => type switch
    {
        ActorType.Bug => new Color("70c040"),
        ActorType.Fireball => new Color("ff8830"),
        ActorType.Ball => new Color("ff70b0"),
        ActorType.Tank => new Color("40a0b8"),
        ActorType.Glider => new Color("c8c8e0"),
        ActorType.Teeth => new Color("c05070"),
        ActorType.Walker => new Color("c0a878"),
        ActorType.Blob => new Color("88d048"),
        _ => new Color("e090d0"), // paramecium
    };

    private static string MonsterGlyph(ActorType type) => type switch
    {
        ActorType.Bug => "b",
        ActorType.Fireball => "f",
        ActorType.Ball => "o",
        ActorType.Tank => "t",
        ActorType.Glider => "g",
        ActorType.Teeth => "T",
        ActorType.Walker => "w",
        ActorType.Blob => "B",
        _ => "p",
    };

    private static Color Background(Tile tile) => tile switch
    {
        Tile.Wall => new Color("565664"),
        Tile.FakeWall => new Color("565664"),       // disguised as a wall
        Tile.Water => new Color("2a5db0"),
        Tile.Fire => new Color("c74a33"),
        Tile.Dirt => new Color("6b4a2f"),
        Tile.Gravel => new Color("55504a"),
        Tile.Ice or Tile.IceNW or Tile.IceNE or Tile.IceSW or Tile.IceSE
            => new Color("a9d5e2"),
        Tile.ForceN or Tile.ForceS or Tile.ForceE or Tile.ForceW or Tile.ForceRandom
            => new Color("463a6e"),
        Tile.Exit => new Color("2e8b57"),
        Tile.Socket => new Color("3a3a46"),
        Tile.DoorRed => new Color("a03030"),
        Tile.DoorBlue => new Color("3050a8"),
        Tile.DoorYellow => new Color("a89a30"),
        Tile.DoorGreen => new Color("30a058"),
        Tile.Hint => new Color("2f4f6f"),
        Tile.Thief => new Color("4e3a5e"),
        Tile.Teleport => new Color("6e2a80"),
        Tile.ToggleClosed => new Color("6a4848"),
        Tile.ToggleOpen => new Color("2a2028"),
        Tile.CloneMachine => new Color("6e6e3a"),
        Tile.PopupWall => new Color("42424e"),
        // InvisibleWall and AppearingWall deliberately draw as floor.
        _ => new Color("1c1c24"),
    };

    private static string Glyph(Tile tile) => tile switch
    {
        Tile.Chip => "C",
        Tile.Exit => "E",
        Tile.Socket => "#",
        Tile.Hint => "?",
        Tile.Thief => "!",
        Tile.DoorRed or Tile.DoorBlue or Tile.DoorYellow or Tile.DoorGreen => "D",
        Tile.KeyRed or Tile.KeyBlue or Tile.KeyYellow or Tile.KeyGreen => "K",
        Tile.BootsWater or Tile.BootsFire or Tile.BootsIce or Tile.BootsForce => "B",
        Tile.ForceN => "^",
        Tile.ForceS => "v",
        Tile.ForceE => ">",
        Tile.ForceW => "<",
        Tile.ForceRandom => "%",
        Tile.Teleport => "@",
        Tile.Bomb => "*",
        Tile.ButtonGreen or Tile.ButtonRed or Tile.ButtonBrown or Tile.ButtonBlue => "o",
        Tile.ToggleClosed => "=",
        Tile.ToggleOpen => "-",
        Tile.Trap => "u",
        Tile.CloneMachine => "M",
        _ => "",
    };

    private static Color GlyphColor(Tile tile) => tile switch
    {
        Tile.Chip => new Color("ffd75e"),
        Tile.KeyRed => new Color("e05050"),
        Tile.KeyBlue => new Color("5080e0"),
        Tile.KeyYellow => new Color("e0d050"),
        Tile.KeyGreen => new Color("50e080"),
        Tile.Bomb => new Color("ff6050"),
        Tile.ButtonGreen => new Color("50e080"),
        Tile.ButtonRed => new Color("e05050"),
        Tile.ButtonBrown => new Color("b08050"),
        Tile.ButtonBlue => new Color("5080e0"),
        Tile.BootsWater => new Color("60a0ff"),
        Tile.BootsFire => new Color("ff8050"),
        Tile.BootsIce => new Color("b0e0f0"),
        Tile.BootsForce => new Color("b090e0"),
        _ => new Color(1, 1, 1, 0.85f),
    };
}
