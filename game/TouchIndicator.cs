using ChipsCore;
using Godot;

namespace ChipsChallenge;

/// <summary>
/// Screen overlay showing the touch-control thresholds: two concentric
/// rings around the finger's anchor point. Inside the inner ring nothing
/// happens (tap zone); past it is a single step; past the outer ring is a
/// run. The ring you're currently "in" lights up.
/// </summary>
public partial class TouchIndicator : Control
{
    public float StepRadius { get; set; } = 48f;
    public float RunRadius { get; set; } = 88f;

    private Vector2? _anchor;
    private Vector2 _finger;

    public void Begin(Vector2 position)
    {
        _anchor = position;
        _finger = position;
        QueueRedraw();
    }

    public void Update(Vector2 position)
    {
        if (_anchor == null) return;
        _finger = position;
        QueueRedraw();
    }

    public void End()
    {
        _anchor = null;
        QueueRedraw();
    }

    private const float GapHalf = 0.09f; // radians shaved off each arc end

    /// <summary>Quadrant center angles (Godot screen space, +Y is down).</summary>
    private static readonly (Direction Dir, float Angle)[] Quadrants =
    {
        (ChipsCore.Direction.Right, 0f),
        (ChipsCore.Direction.Down, Mathf.Pi / 2f),
        (ChipsCore.Direction.Left, Mathf.Pi),
        (ChipsCore.Direction.Up, -Mathf.Pi / 2f),
    };

    public override void _Draw()
    {
        if (_anchor is not { } anchor) return;

        var delta = _finger - anchor;
        var reach = delta.Length();
        var stepping = reach >= StepRadius;
        var running = reach >= RunRadius;

        // Same dominant-axis rule the input code uses.
        var dir = !stepping ? ChipsCore.Direction.None
            : Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y)
                ? (delta.X > 0 ? ChipsCore.Direction.Right : ChipsCore.Direction.Left)
                : (delta.Y > 0 ? ChipsCore.Direction.Down : ChipsCore.Direction.Up);

        var dim = new Color(1f, 1f, 1f, 0.18f);
        var gold = new Color("ffd75e", 0.9f);
        var orange = new Color("ff8850", 0.95f);

        foreach (var (quadDir, angle) in Quadrants)
        {
            var from = angle - Mathf.Pi / 4f + GapHalf;
            var to = angle + Mathf.Pi / 4f - GapHalf;
            var active = quadDir == dir;

            DrawArc(anchor, StepRadius, from, to, 16,
                active ? gold : dim, active ? 3.5f : 2f, antialiased: true);
            DrawArc(anchor, RunRadius, from, to, 20,
                active && running ? orange : dim, active && running ? 3.5f : 2f, antialiased: true);
        }

        if (reach > 4f)
            DrawLine(anchor, _finger, new Color(1f, 1f, 1f, 0.25f), 2f, antialiased: true);
        DrawCircle(_finger, 7f, new Color(1f, 1f, 1f, running ? 0.9f : 0.6f));
    }
}
