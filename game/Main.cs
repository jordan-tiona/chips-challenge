using System.Collections.Generic;
using ChipsCore;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ChipsChallenge;

public partial class Main : Node2D
{
    private const double RepeatDelay = 0.16;   // seconds between steps while held
    private const float SwipeThreshold = 48f;  // px of drag that counts as a swipe
    private const float TapSlop = 24f;         // max px of finger travel for a tap

    private List<LevelData> _levels = new();
    private int _levelIndex;
    private bool _inputLocked;

    private Board _board = null!;
    private Camera2D _camera = null!;
    private Label _titleLabel = null!;
    private Label _chipsLabel = null!;
    private Label _hintLabel = null!;
    private PanelContainer _hintPanel = null!;
    private PanelContainer _banner = null!;
    private Label _bannerLabel = null!;

    private Direction _lastHeld = Direction.None;
    private double _cooldown;
    private Vector2? _touchAnchor;
    private Direction _touchDir = Direction.None;
    private Queue<Direction>? _autoPath;  // tap-to-move plan being executed

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

    private void BuildHud()
    {
        var hud = new CanvasLayer();
        AddChild(hud);

        // ---- top bar: prev | title + chips | next ----
        var topBar = new PanelContainer();
        topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        hud.AddChild(topBar);

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
        mid.AddChild(_titleLabel);
        mid.AddChild(_chipsLabel);
        row.AddChild(mid);

        var next = MakeNavButton(">");
        next.Pressed += () => LoadLevel(_levelIndex + 1);
        row.AddChild(next);

        // ---- bottom hint panel ----
        _hintPanel = new PanelContainer { Visible = false };
        _hintPanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _hintLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _hintLabel.AddThemeFontSizeOverride("font_size", 22);
        _hintPanel.AddChild(_hintLabel);
        hud.AddChild(_hintPanel);

        // ---- level-complete banner ----
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Ignore;
        hud.AddChild(center);
        _banner = new PanelContainer { Visible = false };
        _bannerLabel = new Label();
        _bannerLabel.AddThemeFontSizeOverride("font_size", 40);
        _banner.AddChild(_bannerLabel);
        center.AddChild(_banner);
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
        _board.LoadLevel(level);
        _titleLabel.Text = $"{level.Number}. {level.Title}";
        _banner.Visible = false;
        _inputLocked = false;
        _autoPath = null;
        _camera.Position = _board.ChipPixelCenter;
        _camera.ResetSmoothing();
        RefreshHud();
        GD.Print($"Level {level.Number}: {level.Title} (password {level.Password})");
    }

    private void RefreshHud()
    {
        var state = _board.State;
        if (state == null) return;
        _chipsLabel.Text = $"chips left: {state.ChipsRemaining}";
        _hintPanel.Visible = state.OnHint && state.Hint.Length > 0;
        _hintLabel.Text = state.Hint;
    }

    public override void _Process(double delta)
    {
        _camera.Position = _board.ChipPixelCenter;

        if (_inputLocked) return;

        var held = KeyboardDirection();
        if (held == Direction.None) held = _touchDir;

        if (held != Direction.None)
        {
            _autoPath = null; // manual input always wins over a tapped path
            if (held != _lastHeld)
            {
                _lastHeld = held;
                _cooldown = RepeatDelay;
                DoMove(held);
            }
            else
            {
                _cooldown -= delta;
                if (_cooldown <= 0)
                {
                    _cooldown += RepeatDelay;
                    DoMove(held);
                }
            }
            return;
        }

        _lastHeld = Direction.None;

        if (_autoPath is { Count: > 0 })
        {
            _cooldown -= delta;
            if (_cooldown <= 0)
            {
                _cooldown += RepeatDelay;
                if (DoMove(_autoPath.Dequeue()) == MoveResult.Blocked)
                    _autoPath = null; // world disagreed with the plan; stop
            }
        }
        else
        {
            _cooldown = 0;
        }
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
        switch (@event)
        {
            case InputEventScreenTouch touch when touch.Pressed:
                _touchAnchor = touch.Position;
                _touchDir = Direction.None;
                break;
            case InputEventScreenTouch touch:
                // A release that never became a swipe is a tap.
                if (_touchAnchor is { } start && _touchDir == Direction.None
                    && (touch.Position - start).Length() <= TapSlop)
                    HandleTap(touch.Position);
                _touchAnchor = null;
                _touchDir = Direction.None;
                break;
            case InputEventScreenDrag drag when _touchAnchor is { } anchor:
                var delta = drag.Position - anchor;
                if (delta.Length() >= SwipeThreshold)
                    _touchDir = Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y)
                        ? (delta.X > 0 ? Direction.Right : Direction.Left)
                        : (delta.Y > 0 ? Direction.Down : Direction.Up);
                else
                    _touchDir = Direction.None;
                break;
        }
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
            _cooldown = 0; // first step immediately
        }
    }

    private MoveResult DoMove(Direction dir)
    {
        var result = _board.TryMove(dir);
        RefreshHud();
        if (result != MoveResult.Won) return result;

        _inputLocked = true;
        _touchDir = Direction.None;
        _touchAnchor = null;
        _autoPath = null;
        _bannerLabel.Text = "  Level Complete!  ";
        _banner.Visible = true;
        GetTree().CreateTimer(1.5).Timeout += () => LoadLevel(_levelIndex + 1);
        return result;
    }
}
