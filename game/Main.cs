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
    private Label _chipsValue = null!;
    private PanelContainer _timePill = null!;
    private Label _timeValue = null!;
    private readonly List<(PanelContainer Pill, Label Label, Tile Key, string Letter)> _keyPills = new();
    private readonly List<(PanelContainer Pill, System.Func<GameState, bool> Has)> _bootPills = new();
    private PanelContainer _topBar = null!;
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
    private Progress _progress = new();

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
        _progress = Progress.Load();
        LoadLevel(Mathf.Clamp(_progress.LastLevelIndex, 0, _levels.Count - 1));
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

        // ---- top bar: prev | title + stat pills | next ----
        _topBar = new PanelContainer();
        _topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        root.AddChild(_topBar);
        // The board is framed below the bar (UpdateCameraFraming), so track
        // every bar move/resize: layout, rotation, safe-area changes.
        _topBar.ItemRectChanged += UpdateCameraFraming;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        _topBar.AddChild(row);

        var prev = MakeNavButton("<");
        prev.Pressed += () => LoadLevel(_levelIndex - 1);
        row.AddChild(prev);

        var mid = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        mid.AddThemeConstantOverride("separation", 6);
        _titleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _titleLabel.AddThemeFontSizeOverride("font_size", 26);
        mid.AddChild(_titleLabel);

        var stats = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stats.AddThemeConstantOverride("separation", 8);
        mid.AddChild(stats);
        row.AddChild(mid);

        (var chipsPill, _chipsValue) = MakePill(new Color("ffd75e"));
        stats.AddChild(chipsPill);
        (_timePill, _timeValue) = MakePill(new Color("a9d5e2"));
        stats.AddChild(_timePill);

        foreach (var (key, color, letter) in new[]
        {
            (Tile.KeyRed, new Color("e05050"), "R"),
            (Tile.KeyBlue, new Color("5080e0"), "B"),
            (Tile.KeyYellow, new Color("e0d050"), "Y"),
            (Tile.KeyGreen, new Color("50e080"), "G"),
        })
        {
            var (pill, label) = MakePill(color);
            label.Text = letter;
            stats.AddChild(pill);
            _keyPills.Add((pill, label, key, letter));
        }

        foreach (var (name, color, has) in new (string, Color, System.Func<GameState, bool>)[]
        {
            ("flip", new Color("60a0ff"), s => s.HasFlippers),
            ("fire", new Color("ff8050"), s => s.HasFireBoots),
            ("skate", new Color("b0e0f0"), s => s.HasSkates),
            ("grip", new Color("b090e0"), s => s.HasSuction),
        })
        {
            var (pill, label) = MakePill(color);
            label.Text = name;
            stats.AddChild(pill);
            _bootPills.Add((pill, has));
        }

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
        _bannerLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _bannerLabel.AddThemeFontSizeOverride("font_size", 40);
        _banner.AddChild(_bannerLabel);
        center.AddChild(_banner);
    }

    /// <summary>Convert the OS-reported safe area into window-relative
    /// insets (GetDisplaySafeArea is in global desktop coordinates — on a
    /// multi-monitor desktop it can start at e.g. x=1920) and apply them as
    /// HUD margins in canvas units. Rerun on window resize/rotation.</summary>
    private void ApplySafeAreaMargins()
    {
        var win = DisplayServer.WindowGetSize();
        var pos = DisplayServer.WindowGetPosition();
        var safe = DisplayServer.GetDisplaySafeArea();
        var canvas = GetViewport().GetVisibleRect().Size;
        if (win.X <= 0 || win.Y <= 0 || canvas.X <= 0) return;
        var scale = win.X / canvas.X;

        var left = Mathf.Max(0, safe.Position.X - pos.X);
        var top = Mathf.Max(0, safe.Position.Y - pos.Y);
        var right = Mathf.Max(0, pos.X + win.X - safe.End.X);
        var bottom = Mathf.Max(0, pos.Y + win.Y - safe.End.Y);

        _safeArea.AddThemeConstantOverride("margin_left", Mathf.RoundToInt(left / scale));
        _safeArea.AddThemeConstantOverride("margin_top", Mathf.RoundToInt(top / scale));
        _safeArea.AddThemeConstantOverride("margin_right", Mathf.RoundToInt(right / scale));
        _safeArea.AddThemeConstantOverride("margin_bottom", Mathf.RoundToInt(bottom / scale));
    }

    /// <summary>Rounded stat pill: accent border + tinted fill + accent text.
    /// Readable at a glance in the palette the board already uses.</summary>
    private static (PanelContainer Pill, Label Label) MakePill(Color accent)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(accent.R, accent.G, accent.B, 0.14f),
            BorderColor = accent,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(10);

        var pill = new PanelContainer();
        pill.AddThemeStyleboxOverride("panel", style);
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 19);
        label.AddThemeColorOverride("font_color", accent);
        pill.AddChild(label);
        return (pill, label);
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
        if (_progress.LastLevelIndex != _levelIndex)
        {
            _progress.LastLevelIndex = _levelIndex;
            _progress.Save();
        }
        GD.Print($"Level {level.Number}: {level.Title} (password {level.Password})");
    }

    private void RefreshHud()
    {
        var state = _board.State;
        if (state == null) return;

        _chipsValue.Text = $"chips {state.ChipsRemaining}";
        _chipsValue.AddThemeColorOverride("font_color",
            state.ChipsRemaining == 0 ? new Color("50e080") : new Color("ffd75e"));

        if (_currentLevel is { TimeLimit: > 0 })
        {
            _timePill.Visible = true;
            var seconds = Mathf.Max(0, Mathf.CeilToInt(_timeLeft));
            _timeValue.Text = $"time {seconds}";
            _timeValue.AddThemeColorOverride("font_color",
                seconds <= 15 ? new Color("ff6050") : new Color("a9d5e2"));
        }
        else
        {
            _timePill.Visible = false;
        }

        foreach (var (pill, label, key, letter) in _keyPills)
        {
            var count = state.GetKeyCount(key);
            pill.Visible = count > 0;
            if (count > 0)
                label.Text = key == Tile.KeyGreen || count == 1 ? letter : $"{letter}{count}";
        }
        foreach (var (pill, has) in _bootPills)
            pill.Visible = has(state);

        _hintPanel.Visible = state.OnHint && state.Hint.Length > 0;
        _hintLabel.Text = state.Hint;
    }

    /// <summary>Frame the world in the band below the top bar instead of the
    /// full window, so the bar never covers Chip. Camera2D clamps to limits
    /// *before* applying Offset, so shifting the view up by half the bar's
    /// world-space height and widening both vertical limits by the same
    /// amount yields exactly: world top edge at the bar's bottom, world
    /// bottom edge at the screen's bottom.</summary>
    private void UpdateCameraFraming()
    {
        if (_topBar == null || _camera == null) return;
        var occluded = _topBar.GetGlobalRect().End.Y; // canvas px hidden behind the bar
        var half = occluded / _zoom / 2f;             // world px, halved
        _camera.Offset = new Vector2(0, -half);
        _camera.LimitTop = Mathf.RoundToInt(-half);
        _camera.LimitBottom = Mathf.RoundToInt(GameState.Height * Board.TileSize + half);
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
        UpdateCameraFraming(); // bar occlusion in world units depends on zoom
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
                _bannerLabel.Text = WinBanner();
                _banner.Visible = true;
                GetTree().CreateTimer(2.0).Timeout += () => LoadLevel(_levelIndex + 1);
                break;
            case MoveResult.Died:
                ShowDeath(_board.State?.DeathReason ?? "");
                break;
        }
    }

    /// <summary>Record the win in progress (furthest level, best time-left
    /// for timed levels) and build the banner text.</summary>
    private string WinBanner()
    {
        _progress.FurthestLevelIndex = Mathf.Max(_progress.FurthestLevelIndex,
            Mathf.Min(_levelIndex + 1, _levels.Count - 1));

        var text = "  Level Complete!  ";
        if (_currentLevel is { TimeLimit: > 0 } level)
        {
            var left = Mathf.Max(0, Mathf.CeilToInt(_timeLeft));
            var best = _progress.BestTimeLeft.GetValueOrDefault(level.Number, -1);
            if (left > best)
            {
                _progress.BestTimeLeft[level.Number] = left;
                text += best < 0
                    ? $"\n  time left: {left}  "
                    : $"\n  time left: {left} — new best!  ";
            }
            else
            {
                text += $"\n  time left: {left} (best {best})  ";
            }
        }
        _progress.Save();
        return text;
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
