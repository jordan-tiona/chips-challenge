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
/// Replays a recorded TWS solution through the engine on Tile World's
/// clock: 20 ticks/second, Chip and monsters stepping every 4 ticks,
/// slides every 2. This is the fidelity oracle: a faithful engine wins
/// every level exactly as recorded. Divergences (an early death, a
/// missed exit) mean our rules or timing differ from MS — each failure
/// is a bug report with coordinates.
/// </summary>
/// <summary>Tick-scheduling knobs, tuned empirically against the CCLP1
/// public solutions (see M4 notes in DESIGN.md).</summary>
public sealed record ReplayOptions(int MonsterOffset, int SlideOffset, bool MovesFirst, int TeethOffset = 0)
{
    public static readonly ReplayOptions Default = new(2, 1, true, 1);
}

public static class ReplayRunner
{
    public static ReplayResult Run(LevelData level, TwsSolution solution, ReplayOptions? options = null)
    {
        var opt = options ?? ReplayOptions.Default;
        if (solution.HasUnsupportedMoves)
            return new ReplayResult(level.Number, level.Title, ReplayOutcome.Unsupported, 0,
                "solution uses mouse or diagonal moves");

        var s = new GameState(level);
        s.SeedRng(solution.RngSeed);
        s.TeethOffset = opt.TeethOffset;

        var moveIndex = 0;
        var maxTick = solution.TimeTicks + 200; // grace period past recorded end

        ReplayResult? CheckEnd(int t)
        {
            if (s.IsDead) return Fail(level, s, t);
            if (s.Won) return new ReplayResult(level.Number, level.Title, ReplayOutcome.Win, t, "");
            return null;
        }

        for (var t = 0; t <= maxTick; t++)
        {
            if (opt.MovesFirst)
            {
                while (moveIndex < solution.Moves.Count && solution.Moves[moveIndex].Tick == t)
                {
                    s.TryMove(solution.Moves[moveIndex].Dir);
                    moveIndex++;
                    if (CheckEnd(t) is { } r0) return r0;
                }
            }

            if (t % 4 == opt.MonsterOffset % 4)
            {
                s.MonsterTick();
                if (CheckEnd(t) is { } r1) return r1;
            }
            if (t % 2 == opt.SlideOffset % 2)
            {
                if (s.SlideDir != Direction.None) s.SlideStep();
                if (s.AnyBlocksSliding) s.SlideBlocks();
                s.MonstersSlideTick();
                if (CheckEnd(t) is { } r2) return r2;
            }

            if (!opt.MovesFirst)
            {
                while (moveIndex < solution.Moves.Count && solution.Moves[moveIndex].Tick == t)
                {
                    s.TryMove(solution.Moves[moveIndex].Dir);
                    moveIndex++;
                    if (CheckEnd(t) is { } r3) return r3;
                }
            }
        }

        return new ReplayResult(level.Number, level.Title, ReplayOutcome.NoWin, maxTick,
            $"moves used {moveIndex}/{solution.Moves.Count}, chip at ({s.ChipX},{s.ChipY}), chips left {s.ChipsRemaining}");
    }

    private static ReplayResult Fail(LevelData level, GameState s, int tick) =>
        new(level.Number, level.Title, ReplayOutcome.Death, tick,
            $"{s.DeathReason} at ({s.ChipX},{s.ChipY})");
}
