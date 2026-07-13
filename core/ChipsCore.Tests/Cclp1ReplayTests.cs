using Xunit.Abstractions;

namespace ChipsCore.Tests;

/// <summary>
/// The M4 fidelity scoreboard: replay the community's public CCLP1
/// solutions through the engine. The floor is the currently-achieved pass
/// count — raise it whenever fidelity improves, and any regression that
/// breaks a previously-passing level fails this test.
/// </summary>
public class Cclp1ReplayTests
{
    /// <summary>Currently-passing replay count. 149 is the destination.</summary>
    private const int PassFloor = 67;

    private readonly ITestOutputHelper _output;

    public Cclp1ReplayTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "levels", "CCLP1.dat")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir == null, "repo root not found above test directory");
        return dir!;
    }

    [Fact]
    public void Replay_Scoreboard_Meets_The_Floor()
    {
        var root = RepoRoot();
        var levels = DatParser.Parse(File.ReadAllBytes(Path.Combine(root, "levels", "CCLP1.dat")));
        var solutions = TwsParser.Parse(File.ReadAllBytes(
            Path.Combine(root, "levels", "solutions", "public_CCLP1.dac.tws")));

        var results = levels
            .Where(l => solutions.ContainsKey(l.Number))
            .Select(l => ReplayRunner.Run(l, solutions[l.Number]))
            .ToList();

        var wins = results.Count(r => r.Outcome == ReplayOutcome.Win);
        foreach (var g in results.GroupBy(r => r.Outcome).OrderBy(g => g.Key))
            _output.WriteLine($"{g.Key}: {g.Count()}");
        foreach (var r in results.Where(r => r.Outcome != ReplayOutcome.Win))
            _output.WriteLine($"#{r.Level} {r.Title}: {r.Outcome} t={r.Tick} {r.Detail}");

        Assert.True(wins >= PassFloor,
            $"replay fidelity regressed: {wins} wins, floor is {PassFloor}");
    }

    [Fact]
    public void Tws_Parses_All_Solutions_With_Sane_Data()
    {
        var root = RepoRoot();
        var solutions = TwsParser.Parse(File.ReadAllBytes(
            Path.Combine(root, "levels", "solutions", "public_CCLP1.dac.tws")));
        Assert.Equal(149, solutions.Count);
        Assert.All(solutions.Values, s =>
        {
            Assert.InRange(s.LevelNumber, 1, 149);
            Assert.True(s.TimeTicks > 0, $"level {s.LevelNumber} has no duration");
            Assert.True(s.HasUnsupportedMoves || s.Moves.Count > 0,
                $"level {s.LevelNumber} has no moves");
            if (s.Moves.Count > 0)
                Assert.Equal(s.Moves.OrderBy(m => m.Tick), s.Moves);
        });
    }
}
