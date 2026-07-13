namespace ChipsCore;

public enum ReplayOutcome
{
    Win,
    Death,
    NoWin,          // ran out of recorded moves without winning or dying
    Unsupported,    // solution uses mouse/diagonal moves
}

public sealed record ReplayResult(int Level, string Title, ReplayOutcome Outcome, int Tick, string Detail);

/// <summary>
/// Replays a recorded TWS solution through the tick engine: one
/// Advance() per Tile World tick, feeding each recorded move at its
/// recorded tick. This is the fidelity oracle: a faithful engine wins
/// every level exactly as recorded. Divergences (an early death, a
/// missed exit) mean our rules or timing differ from MS — each failure
/// is a bug report with coordinates.
/// </summary>
public static class ReplayRunner
{
    /// <param name="tickBias">Offset between recorded move times and the
    /// engine's tick counter (TW's time bookkeeping is off-by-one-ish
    /// relative to solution timestamps; tuned empirically).</param>
    /// <param name="bufferInput">Hold an undelivered move until the engine
    /// accepts it instead of dropping it on a gated tick.</param>
    public static ReplayResult Run(LevelData level, TwsSolution solution,
        int tickBias = 0, bool bufferInput = true)
    {
        if (solution.HasUnsupportedMoves)
            return new ReplayResult(level.Number, level.Title, ReplayOutcome.Unsupported, 0,
                "solution uses mouse or diagonal moves");

        var s = new GameState(level);
        s.SeedRng(solution.RngSeed);
        s.Stepping = solution.Stepping;

        var moveIndex = 0;
        var pending = Direction.None;
        var maxTick = solution.TimeTicks + 200; // grace period past recorded end
        for (var t = 0; t <= maxTick; t++)
        {
            var input = pending;
            while (moveIndex < solution.Moves.Count && solution.Moves[moveIndex].Tick == t + tickBias)
            {
                input = solution.Moves[moveIndex].Dir;
                moveIndex++;
            }
            pending = Direction.None;

            var chipBefore = (s.ChipX, s.ChipY);
            var result = s.Advance(input);
            if (bufferInput && input != Direction.None
                && (s.ChipX, s.ChipY) == chipBefore && !s.IsDead && !s.Won)
                pending = input; // not accepted this tick; try again next
            if (result == MoveResult.Won || s.Won)
                return new ReplayResult(level.Number, level.Title, ReplayOutcome.Win, t, "");
            if (result == MoveResult.Died || s.IsDead)
                return new ReplayResult(level.Number, level.Title, ReplayOutcome.Death, t,
                    $"{s.DeathReason} at ({s.ChipX},{s.ChipY})");
        }

        return new ReplayResult(level.Number, level.Title, ReplayOutcome.NoWin, maxTick,
            $"moves used {moveIndex}/{solution.Moves.Count}, chip at ({s.ChipX},{s.ChipY}), chips left {s.ChipsRemaining}");
    }
}
