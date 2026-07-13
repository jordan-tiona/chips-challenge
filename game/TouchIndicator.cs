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

    public override void _Draw()
    {
        if (_anchor is not { } anchor) return;

        var reach = (_finger - anchor).Length();
        var stepping = reach >= StepRadius && reach < RunRadius;
        var running = reach >= RunRadius;

        var dim = new Color(1f, 1f, 1f, 0.18f);
        var stepColor = stepping ? new Color("ffd75e", 0.9f) : dim;
        var runColor = running ? new Color("ff8850", 0.95f) : dim;

        DrawArc(anchor, StepRadius, 0, Mathf.Tau, 48, stepColor, stepping ? 3f : 2f, antialiased: true);
        DrawArc(anchor, RunRadius, 0, Mathf.Tau, 64, runColor, running ? 3f : 2f, antialiased: true);

        if (reach > 4f)
            DrawLine(anchor, _finger, new Color(1f, 1f, 1f, 0.25f), 2f, antialiased: true);
        DrawCircle(_finger, 7f, new Color(1f, 1f, 1f, running ? 0.9f : 0.6f));
    }
}
