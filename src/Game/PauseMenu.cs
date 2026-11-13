using System;
using Gearheart.Core;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Esc (Start on a gamepad) stops the world and opens this menu: Resume, three volume sliders
/// (Master, Effects, Engine), fullscreen, Save and quit, and the full key list. Esc again resumes.
/// It lives on its own canvas layer above the HUD and keeps running while the tree is paused.
/// Settings are saved to user://settings.cfg when the menu closes. Works with the mouse, the
/// keyboard (arrows, Enter) and a gamepad (d-pad, A).
/// </summary>
public partial class PauseMenu : Control
{
    private const float W = 900f, H = 540f;
    private static readonly Color Gold = new(1f, 0.82f, 0.32f), Dim = new(0.72f, 0.75f, 0.8f), Ink = new(0.95f, 0.96f, 0.98f);

    /// <summary>Asked before opening; the world says no while the trip report is up (Esc closes that first).</summary>
    public Func<bool> CanOpen;
    public event Action Opened, Closed;

    private Button _resume, _quit;
    private Button _full;
    private HSlider _master, _effects, _car;
    private AudioStreamPlayer _preview;
    private AudioStream _blip, _rev;
    private double _lastPreview;
    private float _t;
    private int _openFrame, _botStep;
    private ulong _openTick;     // process frames (they count in headless runs too, unlike frames drawn)
    private bool _botDone, _syncing;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;   // clicks do not reach the world while it is open
        Visible = false;

        _blip = GD.Load<AudioStream>("res://assets/audio/pick.wav");
        _rev = GD.Load<AudioStream>("res://assets/audio/shift.wav");
        _preview = new AudioStreamPlayer { Bus = Settings.EffectsBus, VolumeDb = -6f };
        AddChild(_preview);

        var dim = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.62f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(W, H), Size = new Vector2(W, H),
            GrowHorizontal = GrowDirection.Both, GrowVertical = GrowDirection.Both,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.1f, 0.94f),
            BorderColor = new Color(Gold, 0.55f),
            BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
            CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16,
            ShadowColor = new Color(0f, 0f, 0f, 0.45f), ShadowSize = 14,
            ContentMarginLeft = 34, ContentMarginRight = 34, ContentMarginTop = 22, ContentMarginBottom = 18,
        });
        AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 10);
        panel.AddChild(rows);
        rows.AddChild(Text("PAUSED", 36, Ink, HorizontalAlignment.Center));

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 36);
        rows.AddChild(columns);

        // Left: what you can do.
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(330f, 0f) };
        left.AddThemeConstantOverride("separation", 8);
        columns.AddChild(left);
        left.AddChild(_resume = MakeButton("Resume", Close));
        left.AddChild(Gap(6f));
        left.AddChild(Text("SOUND", 15, Gold));
        left.AddChild(SliderRow("Master", Settings.Master, out _master, v => { Settings.Master = v; Preview(_blip, Settings.EffectsBus); }));
        left.AddChild(SliderRow("Effects", Settings.Effects, out _effects, v => { Settings.Effects = v; Preview(_blip, Settings.EffectsBus); }));
        left.AddChild(SliderRow("Engine", Settings.Car, out _car, v => { Settings.Car = v; Preview(_rev, Settings.CarBus); }));
        left.AddChild(Gap(4f));
        left.AddChild(Text("SCREEN", 15, Gold));
        // A toggle that reads like the other buttons (the stock check box is dark on dark here).
        _full = MakeButton("", () => { });
        _full.ToggleMode = true;
        _full.CustomMinimumSize = new Vector2(0f, 38f);
        _full.AddThemeFontSizeOverride("font_size", 18);
        _full.AddThemeStyleboxOverride("pressed", ButtonBox(new Color(0.24f, 0.2f, 0.1f), new Color(Gold, 0.7f)));
        _full.SetPressedNoSignal(Settings.Fullscreen);
        _full.Toggled += on =>
        {
            Settings.Fullscreen = on;
            FullText();
        };
        FullText();
        left.AddChild(_full);
        left.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        left.AddChild(_quit = MakeButton("Save and quit", SaveAndQuit));

        // Right: every key, so nobody has to leave the game to look one up.
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 2);
        columns.AddChild(right);
        right.AddChild(Text("ON FOOT", 15, Gold));
        Keys(right, ("A / D", "walk"), ("Space / W", "jump"), ("Hold left mouse", "mine"), ("Right mouse", "place block (1/2: dirt/stone)"),
             ("E", "grappling hook: S lets go"), ("F", "get in the car"), ("G", "garage (at the shed)"));
        right.AddChild(Gap(8f));
        right.AddChild(Text("DRIVING", 15, Gold));
        Keys(right, ("A / D", "drive"), ("Q / E", "tilt, flips, roll back over"), ("Space", "handbrake (+ D: launch control)"),
             ("Shift", "rocket boost (Rocket Booster)"), ("W", "hop: hold to squat (Hydraulics)"), ("X", "drill on / off"),
             ("H", "horn"), ("R", "repair and refuel (at the garage)"), ("F", "get out"));

        rows.AddChild(Text("Esc / Start: resume      The game saves itself every minute and when you quit.", 14, new Color(Dim, 0.85f), HorizontalAlignment.Center));

        // Arrow keys and the d-pad walk down the left column.
        Chain(_resume, _master, _effects, _car, _full, _quit);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        bool toggle = @event is InputEventKey { Pressed: true, Echo: false } key && key.PhysicalKeycode == Key.Escape
                      || @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Start }
                      || Visible && @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.B };
        if (!toggle) return;
        if (Visible) Close();
        else if (CanOpen?.Invoke() ?? true) Open();
        else return;
        GetViewport().SetInputAsHandled();
    }

    public void Open()
    {
        if (Visible) return;
        Visible = true;
        _t = 0f;
        _openFrame = Engine.GetFramesDrawn();
        _openTick = Engine.GetProcessFrames();
        _syncing = true;   // show the saved values without previewing a sound
        _master.Value = Settings.Master * 100f;
        _effects.Value = Settings.Effects * 100f;
        _car.Value = Settings.Car * 100f;
        _full.SetPressedNoSignal(Settings.Fullscreen);
        FullText();
        _syncing = false;
        GetTree().Paused = true;
        _resume.GrabFocus();
        Opened?.Invoke();
        if (GameSession.PauseTest)
            GD.Print($"[pause] opened frame={_openFrame} paused={GetTree().Paused} focus={FocusName()} " +
                     $"master={Settings.Master:0.00} effects={Settings.Effects:0.00} engine={Settings.Car:0.00}");
    }

    public void Close()
    {
        if (!Visible) return;
        Settings.Save();
        Visible = false;
        GetTree().Paused = false;
        if (GameSession.PauseTest)
            GD.Print($"[pause] closed frame={Engine.GetFramesDrawn()} after {Engine.GetProcessFrames() - _openTick} frames paused={GetTree().Paused}");
        Closed?.Invoke();
    }

    private void FullText() => _full.Text = $"Fullscreen: {(_full.ButtonPressed ? "On" : "Off")}";

    private void SaveAndQuit()
    {
        Settings.Save();
        GameSession.SaveAll();
        GetTree().Quit();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _t += (float)delta;
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_t / 0.12f, 0f, 1f));
        if (GameSession.PauseTest) RunBot();
    }

    // ---- Bot (--pausetest) ------------------------------------------------------------------

    /// <summary>
    /// Moves the sliders, walks the focus with the arrow keys, checks the settings file round trip,
    /// then closes the menu with a real Esc key event.
    /// </summary>
    private void RunBot()
    {
        if (_botDone) return;
        ulong f = Engine.GetProcessFrames() - _openTick;
        if (_botStep == 0 && f >= 8)
        {
            _botStep = 1;
            _master.Value = 80f;
            _effects.Value = 50f;
            _car.Value = 30f;
            GD.Print($"[pause] sliders master={Settings.Master:0.00} ({Settings.BusDb("Master"):0.0} dB) effects={Settings.Effects:0.00} ({Settings.BusDb(Settings.EffectsBus):0.0} dB) " +
                     $"engine={Settings.Car:0.00} ({Settings.BusDb(Settings.CarBus):0.0} dB) buses={AudioServer.BusCount} frame={Engine.GetFramesDrawn()}");
        }
        else if (_botStep == 1 && f >= 12)
        {
            _botStep = 2;
            Press(Key.Down);
            Press(Key.Down);
            GD.Print($"[pause] two presses of Down: focus={FocusName()} frame={Engine.GetFramesDrawn()}");
        }
        else if (_botStep == 2 && f >= 20)
        {
            _botStep = 3;
            var saved = Settings.Save();
            Settings.Reload();
            GD.Print($"[pause] settings saved ({saved}) and read back from {Settings.Path}: master={Settings.Master:0.00} effects={Settings.Effects:0.00} engine={Settings.Car:0.00} frame={Engine.GetFramesDrawn()}");
        }
        else if (_botStep == 3 && f >= 30)
        {
            _botDone = true;
            GD.Print($"[pause] bot presses Esc frame={Engine.GetFramesDrawn()}");
            Press(Key.Escape);
        }
    }

    private static void Press(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }

    private string FocusName() => GetViewport().GuiGetFocusOwner() switch
    {
        null => "none",
        var c when c == _resume => "Resume",
        var c when c == _master => "Master",
        var c when c == _effects => "Effects",
        var c when c == _car => "Engine",
        var c when c == _full => "Fullscreen",
        var c when c == _quit => "Quit",
        var c => c.GetType().Name,
    };

    // ---- Building blocks --------------------------------------------------------------------

    private void Preview(AudioStream stream, string bus)
    {
        if (!Visible || _syncing) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _lastPreview < 0.12) return;
        _lastPreview = now;
        _preview.Bus = bus;
        _preview.Stream = stream;
        _preview.Play();
    }

    private static Label Text(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Control Gap(float h) => new Control { CustomMinimumSize = new Vector2(0f, h) };

    private static Button MakeButton(string text, Action pressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0f, 44f), FocusMode = FocusModeEnum.All };
        b.AddThemeFontSizeOverride("font_size", 20);
        b.AddThemeStyleboxOverride("normal", ButtonBox(new Color(0.16f, 0.18f, 0.23f), new Color(1f, 1f, 1f, 0.12f)));
        b.AddThemeStyleboxOverride("hover", ButtonBox(new Color(0.22f, 0.24f, 0.3f), new Color(Gold, 0.6f)));
        b.AddThemeStyleboxOverride("pressed", ButtonBox(new Color(0.3f, 0.25f, 0.12f), Gold));
        b.AddThemeStyleboxOverride("focus", ButtonBox(new Color(0f, 0f, 0f, 0f), Gold));
        b.AddThemeColorOverride("font_hover_color", Gold);
        b.AddThemeColorOverride("font_focus_color", Gold);
        b.Pressed += pressed;
        return b;
    }

    private static StyleBoxFlat ButtonBox(Color bg, Color border) => new()
    {
        BgColor = bg,
        BorderColor = border,
        BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
    };

    private static Control SliderRow(string name, float value, out HSlider slider, Action<float> changed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var label = Text(name, 18, Ink);
        label.CustomMinimumSize = new Vector2(76f, 0f);
        row.AddChild(label);
        var s = new HSlider
        {
            MinValue = 0, MaxValue = 100, Step = 5, Value = value * 100f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(170f, 24f), FocusMode = FocusModeEnum.All,
        };
        s.AddThemeStyleboxOverride("slider", new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.22f, 0.27f), ContentMarginTop = 3, ContentMarginBottom = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        });
        s.AddThemeStyleboxOverride("grabber_area", new StyleBoxFlat
        {
            BgColor = Gold, ContentMarginTop = 3, ContentMarginBottom = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        });
        s.AddThemeStyleboxOverride("grabber_area_highlight", new StyleBoxFlat
        {
            BgColor = Gold.Lerp(Colors.White, 0.3f), ContentMarginTop = 3, ContentMarginBottom = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        });
        s.AddThemeStyleboxOverride("focus", ButtonBox(new Color(0f, 0f, 0f, 0f), Gold));
        // With the keyboard or a gamepad the focused row's name turns gold, so you can see which slider moves.
        s.FocusEntered += () => label.AddThemeColorOverride("font_color", Gold);
        s.FocusExited += () => label.AddThemeColorOverride("font_color", Ink);
        row.AddChild(s);
        var pct = Text($"{Mathf.RoundToInt(value * 100f)}%", 16, Dim, HorizontalAlignment.Right);
        pct.CustomMinimumSize = new Vector2(48f, 0f);
        row.AddChild(pct);
        s.ValueChanged += v =>
        {
            pct.Text = $"{Mathf.RoundToInt((float)v)}%";
            changed((float)v / 100f);
        };
        slider = s;
        return row;
    }

    private static void Keys(VBoxContainer into, params (string Key, string What)[] keys)
    {
        foreach (var (k, what) in keys)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var key = Text(k, 16, Ink);
            key.CustomMinimumSize = new Vector2(150f, 0f);
            row.AddChild(key);
            row.AddChild(Text(what, 16, Dim));
            into.AddChild(row);
        }
    }

    /// <summary>Up and down move between these in order (wrapping), whatever their layout.</summary>
    private static void Chain(params Control[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            var next = items[(i + 1) % items.Length];
            var prev = items[(i + items.Length - 1) % items.Length];
            items[i].FocusNeighborBottom = items[i].GetPathTo(next);
            items[i].FocusNeighborTop = items[i].GetPathTo(prev);
            items[i].FocusNext = items[i].GetPathTo(next);
            items[i].FocusPrevious = items[i].GetPathTo(prev);
        }
    }
}
