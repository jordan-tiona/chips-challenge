namespace ChipsCore;

/// <summary>
/// BFS shortest-path for tap-to-move. Plans over tiles that are safely
/// walkable *right now* (per GameState.PathSafe — no hazards without the
/// right boots, no slides or teleports that would take control away); it
/// does not simulate pickups or key consumption along the way. The
/// executor re-validates every step by calling TryMove and aborts the
/// path on a blocked result, so a stale plan degrades into a shorter
/// walk, never an illegal or lethal one.
/// </summary>
public static class Pathfinder
{
    private static readonly Direction[] Order =
        { Direction.Up, Direction.Left, Direction.Down, Direction.Right };

    /// <summary>Directions from Chip to (targetX, targetY), or null if the
    /// target is unreachable. An empty list means Chip is already there.</summary>
    public static List<Direction>? FindPath(GameState state, int targetX, int targetY)
    {
        if (targetX is < 0 or >= GameState.Width || targetY is < 0 or >= GameState.Height)
            return null;

        var start = state.ChipY * GameState.Width + state.ChipX;
        var target = targetY * GameState.Width + targetX;
        if (start == target) return new List<Direction>();
        if (!state.PathSafe(targetX, targetY)) return null;

        var cameBy = new Direction[GameState.Width * GameState.Height];
        var queue = new Queue<int>();
        queue.Enqueue(start);
        cameBy[start] = (Direction)(-1); // mark visited; never read as a step

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % GameState.Width;
            var y = index / GameState.Width;
            foreach (var dir in Order)
            {
                var (dx, dy) = dir.Delta();
                var nx = x + dx;
                var ny = y + dy;
                if (nx is < 0 or >= GameState.Width || ny is < 0 or >= GameState.Height)
                    continue;
                var next = ny * GameState.Width + nx;
                if (cameBy[next] != Direction.None || next == start)
                    continue;
                if (!state.CanCross(x, y, dir) || !state.PathSafe(nx, ny))
                    continue;
                cameBy[next] = dir;
                if (next == target)
                    return Reconstruct(cameBy, start, target);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private static List<Direction> Reconstruct(Direction[] cameBy, int start, int target)
    {
        var path = new List<Direction>();
        var index = target;
        while (index != start)
        {
            var dir = cameBy[index];
            path.Add(dir);
            var (dx, dy) = dir.Delta();
            index -= dy * GameState.Width + dx;
        }
        path.Reverse();
        return path;
    }
}
