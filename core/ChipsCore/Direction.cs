namespace ChipsCore;

public enum Direction
{
    None,
    Up,
    Left,
    Down,
    Right,
}

public static class DirectionExtensions
{
    public static (int Dx, int Dy) Delta(this Direction dir) => dir switch
    {
        Direction.Up => (0, -1),
        Direction.Left => (-1, 0),
        Direction.Down => (0, 1),
        Direction.Right => (1, 0),
        _ => (0, 0),
    };
}
