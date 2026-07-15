using System;
using System.Collections.Generic;
using ChipsCore;
using Godot;

namespace ChipsChallenge;

/// <summary>
/// Full-screen level picker overlaid on the HUD. Every level is shown with
/// its number, title, and record (done / best time-left); tapping one jumps
/// straight to it — navigation is never gated by progress. The grid is
/// rebuilt from Progress on every Open() so records are always current.
/// While visible, Main suspends all gameplay input and the level timer.
/// </summary>
public partial class LevelSelect : PanelContainer
{
    public event Action<int>? LevelChosen;

    private const int Columns = 4;
    private const int CellHeight = 118;
    private const int CellGap = 10;

    private List<LevelData> _levels = new();
    private Progress _progress = null!;
    private Label _countLabel = null!;
    private GridContainer _grid = null!;
    private ScrollContainer _scroll = null!;
    private int _currentIndex;
    private int _settleFrames;   // frames until the grid is laid out enough to scroll

    public void Setup(List<LevelData> levels, Progress progress)
    {
        _levels = levels;
        _progress = progress;

        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("14141c") });

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 0);
        AddChild(layout);

        // ---- header: title + done count | close ----
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        layout.AddChild(header);

        var pad = new Control { CustomMinimumSize = new Vector2(12, 0) };
        header.AddChild(pad);

        var title = new Label
        {
            Text = "Levels",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        header.AddChild(title);

        _countLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        _countLabel.AddThemeFontSizeOverride("font_size", 19);
        _countLabel.AddThemeColorOverride("font_color", new Color("50e080"));
        header.AddChild(_countLabel);

        var close = new Button
        {
            Text = "x",
            CustomMinimumSize = new Vector2(96, 96),
            FocusMode = FocusModeEnum.None,
        };
        close.AddThemeFontSizeOverride("font_size", 36);
        close.Pressed += Close;
        header.AddChild(close);

        // ---- scrollable level grid ----
        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        layout.AddChild(_scroll);

        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        _scroll.AddChild(margin);

        _grid = new GridContainer
        {
            Columns = Columns,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _grid.AddThemeConstantOverride("h_separation", CellGap);
        _grid.AddThemeConstantOverride("v_separation", CellGap);
        margin.AddChild(_grid);
    }

    public void Open(int currentIndex)
    {
        _currentIndex = currentIndex;
        foreach (var child in _grid.GetChildren())
            child.QueueFree();
        for (var i = 0; i < _levels.Count; i++)
            _grid.AddChild(MakeCell(i));
        _countLabel.Text = $"{_progress.Done.Count} / {_levels.Count} done";
        Visible = true;
        _settleFrames = 2; // grid needs a layout pass before ScrollVertical sticks
    }

    public void Close() => Visible = false;

    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (_settleFrames > 0 && --_settleFrames == 0)
        {
            // Center-ish the current level: two rows of context above it.
            var row = _currentIndex / Columns;
            _scroll.ScrollVertical = Mathf.Max(0, (row - 2) * (CellHeight + CellGap));
        }
        if (Input.IsActionJustPressed("ui_cancel")) Close();
    }

    private Button MakeCell(int index)
    {
        var level = _levels[index];
        var done = _progress.Done.Contains(level.Number);
        var current = index == _currentIndex;
        var accent = current ? new Color("ffd75e")
            : done ? new Color("50e080")
            : new Color("565664");

        var style = new StyleBoxFlat
        {
            BgColor = new Color(accent.R, accent.G, accent.B, done || current ? 0.13f : 0.06f),
            BorderColor = accent,
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(12);
        var pressedStyle = (StyleBoxFlat)style.Duplicate();
        pressedStyle.BgColor = new Color(accent.R, accent.G, accent.B, 0.3f);

        var button = new Button
        {
            CustomMinimumSize = new Vector2(0, CellHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
        };
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);

        var box = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 2);
        button.AddChild(box);

        var number = new Label
        {
            Text = level.Number.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        number.AddThemeFontSizeOverride("font_size", 30);
        number.AddThemeColorOverride("font_color", accent);
        box.AddChild(number);

        var name = new Label
        {
            Text = level.Title,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", new Color("9a9aa8"));
        box.AddChild(name);

        var record = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        record.AddThemeFontSizeOverride("font_size", 13);
        var best = _progress.BestTimeLeft.GetValueOrDefault(level.Number, -1);
        if (best >= 0)
        {
            record.Text = $"best {best}";
            record.AddThemeColorOverride("font_color", new Color("a9d5e2"));
        }
        else if (done)
        {
            record.Text = "done";
            record.AddThemeColorOverride("font_color", new Color("50e080"));
        }
        else
        {
            record.Text = " ";
        }
        box.AddChild(record);

        var chosen = index;
        button.Pressed += () =>
        {
            Close();
            LevelChosen?.Invoke(chosen);
        };
        return button;
    }
}
