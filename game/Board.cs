using ChipsCore;
using Godot;

namespace ChipsChallenge;

/// <summary>
/// Draws the 32x32 grid from the engine's GameState with placeholder
/// colors and glyphs (proper tileset art is M5). Never mutates state
/// except by forwarding TryMove.
/// </summary>
public partial class Board : Node2D
{
    public const int TileSize = 32;

    public GameState? State { get; private set; }

    private readonly Font _font = ThemeDB.FallbackFont;

    public void LoadLevel(LevelData level)
    {
        State = new GameState(level);
        QueueRedraw();
    }

    public MoveResult TryMove(Direction dir)
    {
        if (State == null) return MoveResult.Blocked;
        var result = State.TryMove(dir);
        QueueRedraw(); // even a blocked bump can change the map (fake walls)
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
                DrawRect(new Rect2(x * TileSize, y * TileSize, TileSize, TileSize), Background(tile));
                var glyph = Glyph(tile);
                if (glyph.Length > 0)
                    DrawString(_font, new Vector2(x * TileSize, y * TileSize + 23), glyph,
                        HorizontalAlignment.Center, TileSize, 18, GlyphColor(tile));
            }
        }

        DrawRect(new Rect2(0, 0, GameState.Width * TileSize, GameState.Height * TileSize),
            new Color(1, 1, 1, 0.25f), filled: false, width: 2);

        // Chip
        DrawCircle(ChipPixelCenter, 12, new Color("ffd75e"));
        DrawArc(ChipPixelCenter, 12, 0, Mathf.Tau, 24, new Color("1a1a2e"), 2, antialiased: true);
    }

    private static Color Background(Tile tile) => tile switch
    {
        Tile.Wall or Tile.FakeWall => new Color("565664"),
        Tile.Water => new Color("2a5db0"),
        Tile.Fire => new Color("c74a33"),
        Tile.Dirt => new Color("6b4a2f"),
        Tile.Gravel => new Color("55504a"),
        Tile.Ice => new Color("a9d5e2"),
        Tile.ForceFloor => new Color("463a6e"),
        Tile.Exit => new Color("2e8b57"),
        Tile.Socket => new Color("3a3a46"),
        Tile.DoorRed => new Color("a03030"),
        Tile.DoorBlue => new Color("3050a8"),
        Tile.DoorYellow => new Color("a89a30"),
        Tile.DoorGreen => new Color("30a058"),
        Tile.Hint => new Color("2f4f6f"),
        Tile.Thief => new Color("4e3a5e"),
        Tile.Block => new Color("7a5a38"),
        Tile.Teleport => new Color("6e2a80"),
        Tile.Trap => new Color("46424e"),
        Tile.ToggleWall => new Color("6a4848"),
        Tile.CloneMachine => new Color("6e6e3a"),
        Tile.PopupWall => new Color("42424e"),
        _ => new Color("1c1c24"), // floor and item-bearing floor
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
        Tile.ForceFloor => "~",
        Tile.Teleport => "@",
        Tile.Bomb => "*",
        Tile.Button => "o",
        Tile.ToggleWall => "=",
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
        Tile.BootsWater or Tile.BootsFire or Tile.BootsIce or Tile.BootsForce
            => new Color("c0c0d0"),
        _ => new Color(1, 1, 1, 0.85f),
    };
}
