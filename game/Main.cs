using System.Collections.Generic;
using System.Linq;
using ChipsCore;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ChipsChallenge;

public partial class Main : Node2D
{
    private const double TickSeconds = 0.05;   // engine runs at 20 ticks/second
    private const double RepeatGraceSeconds = 0.2; // keyboard: hold this long to run
    private const float RunThreshold = 132f;   // touch: drag this far to run
    private const double RunEngageSeconds = 0.08; // ...held past it this long (flick guard)
    private const float SwipeThreshold = 72f;  // px of drag that counts as a swipe
    private const float TapSlop = 32f;         // max px of finger travel for a tap

    private List<LevelData> _levels = new();
    private int _levelIndex;
    private LevelData? _currentLevel;
    private bool _inputLocked;
    private bool _awaitingRestart;   // dead or timed out; any input restarts
    private double _timeLeft;        // seconds; <= 0 while untimed
    private int _shownSeconds = -1;
    private double _tickAccum;
    private int _pathStall;
    private Direction _heldPrev = Direction.None;
    private double _heldDuration;
    private double _runDepthTime; // time the drag has stayed past RunThreshold
    private bool _steppedOnce;   // this gesture already produced its single step

    private Board _board = null!;
    private Camera2D _camera = null!;
    private Label _titleLabel = null!;
    private Label _chipsLabel = null!;
    private Label _invLabel = null!;
    private Label _hintLabel = null!;
    private PanelContainer _hintPanel = null!;
    private PanelContainer _banner = null!;
    private Label _bannerLabel = null!;
    private TouchIndicator _touchIndicator = null!;

    private const float MinZoom = 0.7f;   // whole 32x32 map fits on screen
    private const float MaxZoom = 3f;

    private Vector2? _touchAnchor;
    private Direction _touchDir = Direction.None;
    private bool _touchRun;      // drag went deep enough to mean "run"
    private Queue<Direction>? _autoPath;  // tap-to-move plan being executed

    private readonly Dictionary<int, Vector2> _touches = new();
    private bool _pinching;   // two-finger camera gesture in progress
    private bool _freeCam;    // camera detached from Chip until next move
    private float _zoom = 2f;

    public override void _Ready()
    {
        var bytes = FileAccess.GetFileAsBytes("res://levels/CCLP1.dat");
        _levels = DatParser.Parse(bytes);
        GD.Print($"Loaded {_levels.Count} levels from CCLP1");

        _board = new Board();
        AddChild(_board);

        _camera = new Camera2D
        {
            Zoom = new Vector2(2f, 2f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 8f,
            LimitLeft = 0,
            LimitTop = 0,
            LimitRight = GameState.Width * Board.TileSize,
            LimitBottom = GameState.Height * Board.TileSize,
        };
        _board.AddChild(_camera);
        _camera.MakeCurrent();

        BuildHud();
        LoadLevel(0);
    }

    private MarginContainer _safeArea = null!;

    private void BuildHud()
    {
        var hud = new CanvasLayer();
        AddChild(hud);

        // The whole HUD lives inside the display's safe area so notches,
        // camera cutouts, rounded corners, and the gesture bar never clip
        // it. Both wrappers must ignore mouse or they'd eat board touches.
        _safeArea = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _safeArea.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(_safeArea);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _safeArea.AddChild(root);
        GetTree().Root.SizeChanged += ApplySafeAreaMargins;
        ApplySafeAreaMargins();

        _touchIndicator = new TouchIndicator
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            StepRadius = SwipeThreshold,
            RunRadius = RunThreshold,
        };
        _touchIndicator.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(_touchIndicator); // outside the safe area: rings follow the finger

        // ---- top bar: prev | title + chips | next ----
        var topBar = new PanelContainer();
        topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        root.AddChild(topBar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        topBar.AddChild(row);

        var prev = MakeNavButton("<");
        prev.Pressed += () => LoadLevel(_levelIndex - 1);
        row.AddChild(prev);

        var mid = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _titleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _titleLabel.AddThemeFontSizeOverride("font_size", 26);
        _chipsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _chipsLabel.AddThemeFontSizeOverride("font_size", 20);
        _invLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _invLabel.AddThemeFontSizeOverride("font_size", 16);
        _invLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f));
        mid.AddChild(_titleLabel);
        mid.AddChild(_chipsLabel);
        mid.AddChild(_invLabel);
        row.AddChild(mid);

        var next = MakeNavButton(">");
        next.Pressed += () => LoadLevel(_levelIndex + 1);
        row.AddChild(next);

        // ---- bottom hint panel ----
        _hintPanel = new PanelContainer
        {
            Visible = false,
            // Anchored to the bottom edge; must grow upward or the content
            // renders below the visible screen.
            GrowVertical = Control.GrowDirection.Begin,
        };
        _hintPanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _hintPanel.OffsetLeft = 24;
        _hintPanel.OffsetRight = -24;
        _hintPanel.OffsetBottom = -16;
        _hintLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _hintLabel.AddThemeFontSizeOverride("font_size", 22);
        _hintPanel.AddChild(_hintLabel);
        root.AddChild(_hintPanel);

        // ---- level-complete / death banner ----
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(center);
        _banner = new PanelContainer { Visible = false };
        _bannerLabel = new Label();
        _bannerLabel.AddThemeFontSizeOverride("font_size", 40);
        _banner.AddChild(_bannerLabel);
        center.AddChild(_banner);
    }

    /// <summary>Convert the OS-reported safe area (physical px) into canvas
    /// units and apply it as HUD margins. Rerun on window resize/rotation.</summary>
    private void ApplySafeAreaMargins()
    {
        var win = DisplayServer.WindowGetSize();
        var safe = DisplayServer.GetDisplaySafeArea();
        var canvas = GetViewport().GetVisibleRect().Size;
        if (win.X <= 0 || win.Y <= 0 || canvas.X <= 0) return;
        var scale = win.X / canvas.X;

        _safeArea.AddThemeConstantOverride("margin_left",
            Mathf.RoundToInt(safe.Position.X / scale));
        _safeArea.AddThemeConstantOverride("margin_top",
            Mathf.RoundToInt(safe.Position.Y / scale));
        _safeArea.AddThemeConstantOverride("margin_right",
            Mathf.RoundToInt((win.X - safe.Position.X - safe.Size.X) / scale));
        _safeArea.AddThemeConstantOverride("margin_bottom",
            Mathf.RoundToInt((win.Y - safe.Position.Y - safe.Size.Y) / scale));
    }

    private static Button MakeNavButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(96, 96),
            FocusMode = Control.FocusModeEnum.None,
        };
        button.AddThemeFontSizeOverride("font_size", 36);
        return button;
    }

    private void LoadLevel(int index)
    {
        _levelIndex = ((index % _levels.Count) + _levels.Count) % _levels.Count;
        var level = _levels[_levelIndex];
        _currentLevel = level;
        _board.LoadLevel(level);
        _titleLabel.Text = $"{level.Number}. {level.Title}";
        _banner.Visible = false;
        _inputLocked = false;
        _awaitingRestart = false;
        _timeLeft = level.TimeLimit;
        _shownSeconds = -1;
        _tickAccum = 0;
        _pathStall = 0;
        _autoPath = null;
        _touches.Clear();
        _pinching = false;
        _freeCam = false;
        _camera.PositionSmoothingEnabled = true;
        _camera.Position = _board.ChipPixelCenter;
        _camera.ResetSmoothing();
        RefreshHud();
        GD.Print($"Level {level.Number}: {level.Title} (password {level.Password})");
    }

    private void RefreshHud()
    {
        var state = _board.State;
        if (state == null) return;

        var time = _currentLevel is { TimeLimit: > 0 }
            ? $"   time: {Mathf.Max(0, Mathf.CeilToInt(_timeLeft))}"
            : "";
        _chipsLabel.Text = $"chips left: {state.ChipsRemaining}{time}";

        var keys = "";
        if (state.GetKeyCount(Tile.KeyRed) > 0) keys += $" R{state.GetKeyCount(Tile.KeyRed)}";
        if (state.GetKeyCount(Tile.KeyBlue) > 0) keys += $" B{state.GetKeyCount(Tile.KeyBlue)}";
        if (state.GetKeyCount(Tile.KeyYellow) > 0) keys += $" Y{state.GetKeyCount(Tile.KeyYellow)}";
        if (state.GetKeyCount(Tile.KeyGreen) > 0) keys += " G";
        var boots = "";
        if (state.HasFlippers) boots += " flippers";
        if (state.HasFireBoots) boots += " fire";
        if (state.HasSkates) boots += " skates";
        if (state.HasSuction) boots += " suction";
        _invLabel.Text = (keys.Length > 0 ? $"keys:{keys}" : "")
            + (keys.Length > 0 && boots.Length > 0 ? "   " : "")
            + (boots.Length > 0 ? $"boots:{boots}" : "");
        _invLabel.Visible = _invLabel.Text.Length > 0;

        _hintPanel.Visible = state.OnHint && state.Hint.Length > 0;
        _hintLabel.Text = state.Hint;
    }

    public override void _Process(double delta)
    {
        // A frame hitch must pause the world, not fast-forward it: without
        // this, banked ticks burst (multi-tile jumps) and a long frame can
        // inflate the hold clock past the run grace mid-swipe.
        delta = Mathf.Min(delta, 0.1);

        if (!_freeCam)
            _camera.Position = _board.ChipPixelCenter;

        if (_awaitingRestart)
        {
            if (KeyboardDirection() != Direction.None || Input.IsActionJustPressed("ui_accept"))
                LoadLevel(_levelIndex);
            return;
        }

        if (_inputLocked) return;

        // Level timer.
        if (_currentLevel is { TimeLimit: > 0 })
        {
            _timeLeft -= delta;
            var seconds = Mathf.CeilToInt(_timeLeft);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                RefreshHud();
            }
            if (_timeLeft <= 0)
            {
                ShowDeath("Out of time!");
                return;
            }
        }

        var state = _board.State;
        if (state == null) return;

        var keyboard = KeyboardDirection();
        var held = keyboard != Direction.None ? keyboard : _touchDir;

        if (held != Direction.None)
            _autoPath = null; // manual input always wins over a tapped path

        // Single-step debounce: a fresh gesture yields exactly one step.
        // Keyboard runs after a short hold; touch runs only when the drag
        // stays past RunThreshold for RunEngageSeconds (so a fast flick
        // crossing the ring in transit doesn't count). Within one touch, a
        // direction wobble does NOT re-arm the single step — one touch,
        // one step, unless genuinely running.
        if (held != _heldPrev)
        {
            var midTouchTurn = keyboard == Direction.None
                && held != Direction.None && _heldPrev != Direction.None;
            _heldPrev = held;
            _heldDuration = 0;
            if (!midTouchTurn) _steppedOnce = false;
        }
        else
        {
            _heldDuration += delta;
        }
        _runDepthTime = _touchRun ? _runDepthTime + delta : 0;
        var repeating = keyboard != Direction.None
            ? _heldDuration >= RepeatGraceSeconds
            : _runDepthTime >= RunEngageSeconds;

        // Pump the engine at 20 ticks/second; it does all its own gating
        // (walk speed, slides, monsters, boosting).
        _tickAccum += delta;
        var ticked = false;
        while (_tickAccum >= TickSeconds)
        {
            _tickAccum -= TickSeconds;
            ticked = true;

            var input = _steppedOnce && !repeating ? Direction.None : held;
            var pathMove = false;
            if (input == Direction.None && _autoPath is { Count: > 0 })
            {
                input = _autoPath.Peek();
                pathMove = true;
            }
            if (input != Direction.None) ResumeFollow();

            var before = (state.ChipX, state.ChipY);
            var tickNow = state.CurrentTick;
            HandleResult(_board.Advance(input));
            if (_awaitingRestart || _inputLocked) return;

            if (!pathMove && input != Direction.None
                && state.LastVoluntaryMoveTick == tickNow)
                _steppedOnce = true;

            if (pathMove && _autoPath != null)
            {
                if ((state.ChipX, state.ChipY) != before)
                {
                    _autoPath.Dequeue();
                    _pathStall = 0;
                }
                else if (++_pathStall > 12)
                {
                    _autoPath = null; // world disagreed with the plan; stop
                    _pathStall = 0;
                }
            }
        }
        if (ticked) RefreshHud();
    }

    private static Direction KeyboardDirection()
    {
        if (Input.IsActionPressed("ui_up")) return Direction.Up;
        if (Input.IsActionPressed("ui_down")) return Direction.Down;
        if (Input.IsActionPressed("ui_left")) return Direction.Left;
        if (Input.IsActionPressed("ui_right")) return Direction.Right;
        return Direction.None;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_awaitingRestart)
        {
            if (@event is InputEventScreenTouch { Pressed: false })
                LoadLevel(_levelIndex);
            return;
        }

        switch (@event)
        {
            case InputEventScreenTouch touch when touch.Pressed:
                _touches[touch.Index] = touch.Position;
                if (_touches.Count == 1)
                {
                    _touchAnchor = touch.Position;
                    _touchDir = Direction.None;
                    _touchIndicator.Begin(touch.Position);
                }
                else
                {
                    // Second finger down: this is a camera gesture, not movement.
                    _touchAnchor = null;
                    _touchDir = Direction.None;
                    _pinching = true;
                    _camera.PositionSmoothingEnabled = false; // 1:1 pan feel
                    _touchIndicator.End();
                }
                break;

            case InputEventScreenTouch touch:
                // A single-finger release that never became a swipe is a tap.
                if (!_pinching && _touchAnchor is { } start && _touchDir == Direction.None
                    && (touch.Position - start).Length() <= TapSlop)
                    HandleTap(touch.Position);
                _touches.Remove(touch.Index);
                if (_touches.Count == 0) _pinching = false;
                _touchAnchor = null;
                _touchDir = Direction.None;
                _touchRun = false;
                _touchIndicator.End();
                break;

            case InputEventScreenDrag drag:
                if (_pinching && _touches.Count >= 2)
                    UpdatePinch(drag);
                else if (_touchAnchor is { } anchor)
                {
                    var delta = drag.Position - anchor;
                    if (delta.Length() >= SwipeThreshold)
                        _touchDir = Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y)
                            ? (delta.X > 0 ? Direction.Right : Direction.Left)
                            : (delta.Y > 0 ? Direction.Down : Direction.Up);
                    else
                        _touchDir = Direction.None;
                    _touchRun = delta.Length() >= RunThreshold;
                    _touchIndicator.Update(drag.Position);
                }
                if (_touches.ContainsKey(drag.Index))
                    _touches[drag.Index] = drag.Position;
                break;

            case InputEventMouseButton { Pressed: true } wheel:
                // Desktop convenience; phones use pinch.
                if (wheel.ButtonIndex == MouseButton.WheelUp) SetZoom(_zoom * 1.1f);
                else if (wheel.ButtonIndex == MouseButton.WheelDown) SetZoom(_zoom / 1.1f);
                break;
        }
    }

    /// <summary>Two-finger pan/pinch. Uses the pre-update finger positions
    /// in _touches against the new position from the drag event.</summary>
    private void UpdatePinch(InputEventScreenDrag drag)
    {
        var ids = _touches.Keys.Order().Take(2).ToArray();
        if (!ids.Contains(drag.Index)) return;

        var a0 = _touches[ids[0]];
        var b0 = _touches[ids[1]];
        var a1 = drag.Index == ids[0] ? drag.Position : a0;
        var b1 = drag.Index == ids[1] ? drag.Position : b0;

        var oldDist = (a0 - b0).Length();
        var newDist = (a1 - b1).Length();
        if (oldDist > 1f) SetZoom(_zoom * newDist / oldDist);

        _freeCam = true;
        _camera.Position -= ((a1 + b1) / 2 - (a0 + b0) / 2) / _zoom;
    }

    private void SetZoom(float zoom)
    {
        _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        _camera.Zoom = new Vector2(_zoom, _zoom);
    }

    /// <summary>Any actual move re-attaches the camera to Chip.</summary>
    private void ResumeFollow()
    {
        if (!_freeCam) return;
        _freeCam = false;
        _camera.PositionSmoothingEnabled = true; // glide back, don't snap
    }

    private void HandleTap(Vector2 screenPos)
    {
        var state = _board.State;
        if (state == null) return;
        var local = _board.MakeCanvasPositionLocal(screenPos);
        var x = Mathf.FloorToInt(local.X / Board.TileSize);
        var y = Mathf.FloorToInt(local.Y / Board.TileSize);
        if (x is < 0 or >= GameState.Width || y is < 0 or >= GameState.Height) return;

        var path = Pathfinder.FindPath(state, x, y);
        if (path is { Count: > 0 })
        {
            _autoPath = new Queue<Direction>(path);
            _pathStall = 0;
        }
    }

    private void HandleResult(MoveResult result)
    {
        switch (result)
        {
            case MoveResult.Won:
                _inputLocked = true;
                ClearInputState();
                _bannerLabel.Text = "  Level Complete!  ";
                _banner.Visible = true;
                GetTree().CreateTimer(1.5).Timeout += () => LoadLevel(_levelIndex + 1);
                break;
            case MoveResult.Died:
                ShowDeath(_board.State?.DeathReason ?? "");
                break;
        }
    }

    private void ShowDeath(string reason)
    {
        _awaitingRestart = true;
        ClearInputState();
        _bannerLabel.Text = $"  Ooops! {reason}  \n  (tap to retry)  ";
        _banner.Visible = true;
    }

    private void ClearInputState()
    {
        _touchDir = Direction.None;
        _touchAnchor = null;
        _touchRun = false;
        _autoPath = null;
        _touchIndicator.End(); // don't leave the rings up over a death banner
    }
}
