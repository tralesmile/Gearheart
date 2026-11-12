using System.Linq;
using Gearheart.Core;
using Gearheart.Parts;
using Gearheart.Vehicles;
using Godot;

namespace Gearheart.Garage;

/// <summary>
/// The garage: build a vehicle on a grid from parts, check its stats, save it to a slot,
/// and take it for a test drive. Built in code so it stays easy to change during the prototype.
/// </summary>
public partial class GarageScreen : Control
{
    private static readonly PartCategory[] PaletteOrder =
    {
        PartCategory.Frame, PartCategory.Cab, PartCategory.Engine, PartCategory.Wheel, PartCategory.Armor,
        PartCategory.FuelTank, PartCategory.Drill, PartCategory.Booster, PartCategory.Turbo,
    };

    private VehicleDesign _design;
    private GarageGrid _grid;
    private LineEdit _nameEdit;
    private Label _statsLabel;
    private Label _partInfo;
    private Label _errors;
    private Label _status;
    private Button _driveButton;
    private Button _raceButton;
    private Label _progressLabel;
    private Label _costLabel;
    private OptionButton _suspension;
    private OptionButton _slot;
    private Button _eraser;
    private readonly ButtonGroup _paletteGroup = new();

    public override void _Ready()
    {
        _design = GameSession.CurrentDesign.Clone();   // edits only count once built
        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = new Color(0.16f, 0.18f, 0.22f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        AddChild(margin);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 24);
        margin.AddChild(columns);

        columns.AddChild(BuildLeftColumn());
        columns.AddChild(BuildRightColumn());

        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key && key.PhysicalKeycode == Key.Escape)
            GetTree().ChangeSceneToFile(GameSession.WorldScene);
    }

    // ---- Layout ----------------------------------------------------------------------------

    private Control BuildLeftColumn()
    {
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 12);

        var title = new HBoxContainer();
        title.AddChild(Heading("GARAGE", 28));
        _nameEdit = new LineEdit { Text = _design.Name, CustomMinimumSize = new Vector2(260f, 0f), PlaceholderText = "Vehicle name" };
        _nameEdit.TextChanged += text => _design.Name = text;
        title.AddChild(Spacer());
        title.AddChild(_nameEdit);
        left.AddChild(title);

        _grid = new GarageGrid { Design = _design };
        _grid.Changed += Refresh;
        var gridCenter = new CenterContainer();
        gridCenter.AddChild(_grid);
        left.AddChild(gridCenter);

        left.AddChild(new Label
        {
            Text = "Left click / drag: place part     Right click / drag: remove     Wheels go in the cell under a block\nParts cost materials: mine Scrap, Iron and Copper on foot, then add a Drill Head to tunnel with the car     Esc: back",
            Modulate = new Color(1f, 1f, 1f, 0.7f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _errors = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _errors.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.4f));
        left.AddChild(_errors);

        _costLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        left.AddChild(_costLabel);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);

        _driveButton = new Button { Text = "  BUILD & DRIVE  " };
        _driveButton.AddThemeFontSizeOverride("font_size", 20);
        _driveButton.Pressed += () => Drive(DriveMode.FreeDrive);
        buttons.AddChild(_driveButton);

        _raceButton = new Button { Text = "  HILL CLIMB  " };
        _raceButton.AddThemeFontSizeOverride("font_size", 20);
        _raceButton.Pressed += () => Drive(DriveMode.HillClimb);
        buttons.AddChild(_raceButton);

        AddButton(buttons, "Starter car", () => ReplaceDesign(VehicleDesign.Default(), "Loaded the starter car."));
        AddButton(buttons, "Clear", () =>
        {
            _design.Clear();
            Refresh();
        });

        buttons.AddChild(Spacer());
        _slot = new OptionButton();
        for (int i = 1; i <= 3; i++) _slot.AddItem($"Slot {i}", i);
        buttons.AddChild(_slot);
        AddButton(buttons, "Save", Save);
        AddButton(buttons, "Load", Load);
        left.AddChild(buttons);

        _status = new Label { Modulate = new Color(0.6f, 1f, 0.6f) };
        left.AddChild(_status);

        var info = new HBoxContainer();
        info.AddThemeConstantOverride("separation", 48);
        _statsLabel = new Label();
        info.AddChild(_statsLabel);
        _progressLabel = new Label();
        info.AddChild(_progressLabel);
        left.AddChild(info);
        return left;
    }

    private Control BuildRightColumn()
    {
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(400f, 0f) };
        right.AddThemeConstantOverride("separation", 8);

        right.AddChild(Heading("PARTS", 22));

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 230f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var palette = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(palette);
        right.AddChild(scroll);

        Button first = null;
        foreach (var category in PaletteOrder)
        {
            var parts = PartCatalog.InCategory(category).ToList();
            if (parts.Count == 0) continue;

            palette.AddChild(new Label { Text = CategoryName(category), Modulate = new Color(1f, 1f, 1f, 0.6f) });
            foreach (var part in parts)
            {
                var button = new Button
                {
                    Text = $"{part.Name}   ({CostText(part)})",
                    ToggleMode = true,
                    ButtonGroup = _paletteGroup,
                    Alignment = HorizontalAlignment.Left,
                };
                button.AddThemeColorOverride("font_color", part.GodotColor.Lightened(0.35f));
                string id = part.Id;
                button.Pressed += () => SelectPart(id);
                palette.AddChild(button);
                first ??= button;
                if (part.Id == _grid.SelectedPart) button.ButtonPressed = true;
            }
        }

        _eraser = new Button { Text = "[X]  Eraser", ToggleMode = true, ButtonGroup = _paletteGroup, Alignment = HorizontalAlignment.Left };
        _eraser.Pressed += () =>
        {
            _grid.Eraser = true;
            _partInfo.Text = "Eraser: click blocks or wheels to remove them.";
        };
        palette.AddChild(_eraser);

        var suspensionRow = new HBoxContainer();
        suspensionRow.AddChild(new Label { Text = "Suspension for new wheels:" });
        _suspension = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var suspensions = PartCatalog.InCategory(PartCategory.Suspension).ToList();
        for (int i = 0; i < suspensions.Count; i++)
        {
            _suspension.AddItem(suspensions[i].Name, i);
            _suspension.SetItemMetadata(i, suspensions[i].Id);
        }
        _suspension.ItemSelected += index =>
        {
            _grid.SelectedSuspension = _suspension.GetItemMetadata((int)index).AsString();
            var part = PartCatalog.Get(_grid.SelectedSuspension);
            if (part != null) _partInfo.Text = $"{part.Name}\n{part.Description}";
        };
        suspensionRow.AddChild(_suspension);
        right.AddChild(suspensionRow);

        _partInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0f, 96f) };
        right.AddChild(_partInfo);


        SelectPart(_grid.SelectedPart);
        return right;
    }

    // ---- Actions ---------------------------------------------------------------------------

    private void SelectPart(string id)
    {
        _grid.Eraser = false;
        _grid.SelectedPart = id;
        var part = PartCatalog.Get(id);
        if (part == null) return;
        _partInfo.Text = $"{part.Name}\n{part.Description}\n{PartStats(part)}";
    }

    private void Drive(DriveMode mode)
    {
        if (_design.Validate().Count > 0) return;
        _design.Name = _nameEdit.Text;
        if (!GameSession.Build(_design))
        {
            _status.Text = "Not enough materials. Go mining!";
            return;
        }
        GameSession.Mode = mode;
        GetTree().ChangeSceneToFile(mode == DriveMode.HillClimb ? GameSession.DriveScene : GameSession.WorldScene);
    }

    private void Save()
    {
        int slot = _slot.GetSelectedId();
        _status.Text = _design.Save(VehicleDesign.SlotPath(slot)) ? $"Saved \"{_design.Name}\" to slot {slot}." : "Could not save.";
    }

    private void Load()
    {
        int slot = _slot.GetSelectedId();
        var loaded = VehicleDesign.Load(VehicleDesign.SlotPath(slot));
        if (loaded == null)
        {
            _status.Text = $"Slot {slot} is empty.";
            return;
        }
        ReplaceDesign(loaded, $"Loaded \"{loaded.Name}\" from slot {slot}.");
    }

    private void ReplaceDesign(VehicleDesign design, string message)
    {
        _design = design;
        _grid.Design = design;
        _nameEdit.Text = design.Name;
        _status.Text = message;
        _grid.QueueRedraw();
        Refresh();
    }

    private void Refresh()
    {
        var stats = _design.Stats();
        _statsLabel.Text =
            $"Mass              {stats.Mass:0.0} t\n" +
            $"Engine torque     {stats.Torque / 1000f:0} kNm\n" +
            $"Power to weight   {stats.PowerToWeight:0.0}\n" +
            $"Top speed (est.)  {stats.TopSpeedKmh:0} km/h\n" +
            $"Fuel              {stats.FuelCapacity:0}\n" +
            $"Blocks / wheels   {stats.Blocks} / {stats.Wheels}";

        var errors = _design.Validate();
        _errors.Text = errors.Count == 0 ? "" : "Can't drive yet:\n- " + string.Join("\n- ", errors);
        var delta = GameSession.BuildDelta(_design);
        bool affordable = GameSession.CanAfford(delta);
        var lines = delta.Where(kv => kv.Value != 0)
            .Select(kv => kv.Value > 0 ? $"{kv.Key} {kv.Value} (have {GameSession.Count(kv.Key)})" : $"{kv.Key} +{-kv.Value} back");
        string costText = string.Join("   ", lines);
        _costLabel.Text = costText.Length == 0 ? "Cost: nothing to pay" : "Cost: " + costText;
        _costLabel.AddThemeColorOverride("font_color", affordable ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.5f, 0.45f));

        _driveButton.Disabled = errors.Count > 0 || !affordable;
        _raceButton.Disabled = errors.Count > 0 || !affordable;

        var best = GameSession.BestTime(GameSession.HillClimbTrack);
        _progressLabel.Text =
            $"Materials: {GameSession.MaterialsText()}\n" +
            $"Hill Climb best: {(best != null ? $"{GameSession.FormatTime(best.Seconds)} ({best.Vehicle})" : "no time yet")}";
    }

    // ---- Helpers ---------------------------------------------------------------------------

    private static string PartStats(PartDefinition p) => p.Category switch
    {
        PartCategory.Engine => $"Mass {p.Mass} t   Torque {p.Torque / 1000f:0} kNm   Max spin {p.MaxSpin} rad/s   Fuel use {p.FuelUse}/s",
        PartCategory.Wheel => $"Mass {p.Mass} t   Radius {p.Radius}   Grip {p.Grip}" + (p.FrozenGrip > 0f ? $"   Snow/ice grip {p.FrozenGrip}" : ""),
        PartCategory.FuelTank => $"Mass {p.Mass} t   Fuel +{p.FuelCapacity}   Strength {Strength(p)}",
        PartCategory.Turbo => $"Mass {p.Mass} t   +{p.TurboTorque * 100f:0}% torque, +{p.TurboSpin * 100f:0}% top spin at full boost   Spools in {p.SpoolTime:0.0} s",
        PartCategory.Booster => $"Mass {p.Mass} t   Thrust {p.Thrust / 1000f:0.#} kN   Fuel use {p.BoostFuelUse}/s (Shift)",
        _ => $"Mass {p.Mass} t   Strength {Strength(p)}",
    };

    private static string CostText(PartDefinition p) =>
        p.Cost.Count == 0 ? "free" : string.Join(", ", p.Cost.Select(kv => $"{kv.Value} {kv.Key}"));

    private static string Strength(PartDefinition p) => p.BreakLimit switch
    {
        >= 0.4f => "very high",
        >= 0.25f => "high",
        >= 0.15f => "medium",
        _ => "low",
    };

    private static string CategoryName(PartCategory category) => category switch
    {
        PartCategory.Frame => "FRAMES",
        PartCategory.Cab => "CAB",
        PartCategory.Engine => "ENGINES",
        PartCategory.Wheel => "WHEELS",
        PartCategory.Armor => "ARMOR",
        PartCategory.FuelTank => "FUEL",
        PartCategory.Drill => "TOOLS",
        PartCategory.Booster => "BOOSTERS",
        PartCategory.Turbo => "TURBO",
        _ => category.ToString().ToUpperInvariant(),
    };

    private static Label Heading(string text, int size)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static Control Spacer() => new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    private static void AddButton(Container parent, string text, System.Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += () => action();
        parent.AddChild(button);
    }
}
