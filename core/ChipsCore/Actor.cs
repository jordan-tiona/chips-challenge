namespace ChipsCore;

public enum ActorType
{
    Bug,        // follows the wall on its left
    Fireball,   // straight; turns right when blocked; immune to fire
    Ball,       // bounces back and forth
    Tank,       // straight; blue button reverses all tanks
    Glider,     // straight; turns left when blocked; immune to water
    Teeth,      // chases Chip at half speed
    Walker,     // straight; random turn when blocked
    Blob,       // fully random, half speed
    Paramecium, // follows the wall on its right
}

/// <summary>A monster. Blocks are tracked separately (they never move on
/// their own); Chip is not an actor. Only actors on the level's monster
/// list are Active (move on ticks) — MS ignores the rest, and so do we.</summary>
public sealed class Actor
{
    public required ActorType Type { get; init; }
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Dir { get; set; }
    public Direction SlideDir { get; set; } = Direction.None;
    public bool Active { get; set; }
    public bool Dead { get; set; }

    /// <summary>Monster codes are 0x40–0x63: four direction variants
    /// (N, W, S, E) per species.</summary>
    public static Actor FromCode(byte code, int x, int y) => new()
    {
        Type = (ActorType)((code - 0x40) / 4),
        X = x,
        Y = y,
        Dir = (code & 3) switch
        {
            0 => Direction.Up,
            1 => Direction.Left,
            2 => Direction.Down,
            _ => Direction.Right,
        },
    };
}
