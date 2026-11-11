using System;
using Gearheart.Core;
using Gearheart.Parts;
using Gearheart.Vehicles;
using Godot;

namespace Gearheart.Garage;

/// <summary>
/// The side-view build grid. Left click (or drag) places the selected part,
/// right click (or drag) removes. Wheels go in the cell under a block.
/// </summary>
public partial class GarageGrid : Control
{
    public const int CellPixels = 48;

    public VehicleDesign Design { get; set; }
    public string SelectedPart { get; set; } = "frame_steel";
    public string SelectedSuspension { get; set; } = "susp_soft";
    public bool Eraser { get; set; }

    /// <summary>Raised after every change to the design.</summary>
    public event Action Changed;

    private Vector2I _hover = new(-1, -1);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(VehicleDesign.GridWidth, VehicleDesign.GridHeight) * CellPixels;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
            {
                var cell = CellAt(motion.Position);
                if (cell != _hover)
                {
                    _hover = cell;
                    QueueRedraw();
                }
                if ((motion.ButtonMask & MouseButtonMask.Left) != 0) Apply(cell, remove: false);
                else if ((motion.ButtonMask & MouseButtonMask.Right) != 0) Apply(cell, remove: true);
                break;
            }
            case InputEventMouseButton { Pressed: true } button:
            {
                var cell = CellAt(button.Position);
                if (button.ButtonIndex == MouseButton.Left) Apply(cell, remove: false);
                else if (button.ButtonIndex == MouseButton.Right) Apply(cell, remove: true);
                AcceptEvent();
                break;
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit)
        {
            _hover = new Vector2I(-1, -1);
            QueueRedraw();
        }
    }

    private static Vector2I CellAt(Vector2 position) =>
        new(Mathf.FloorToInt(position.X / CellPixels), Mathf.FloorToInt(position.Y / CellPixels));

    private void Apply(Vector2I cell, bool remove)
    {
        if (Design == null || !VehicleDesign.InGrid(cell.X, cell.Y)) return;

        if (remove || Eraser)
        {
            if (Design.BlockAt(cell.X, cell.Y) == null && Design.WheelAt(cell.X, cell.Y) == null) return;
            Design.Remove(cell.X, cell.Y);
        }
        else
        {
            var part = PartCatalog.Get(SelectedPart);
            if (part == null) return;

            if (part.Category == PartCategory.Wheel)
            {
                var existing = Design.WheelAt(cell.X, cell.Y);
                if (existing != null && existing.Wheel == part.Id && existing.Suspension == SelectedSuspension) return;
                Design.SetWheel(cell.X, cell.Y, part.Id, SelectedSuspension);
            }
            else if (part.IsBlock)
            {
                if (Design.BlockAt(cell.X, cell.Y)?.Part == part.Id) return;
                Design.SetBlock(cell.X, cell.Y, part.Id);
            }
            else
            {
                return;
            }
        }

        QueueRedraw();
        Changed?.Invoke();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var gridLine = new Color(1f, 1f, 1f, 0.08f);
        var background = new Color(0.1f, 0.12f, 0.15f);
        DrawRect(new Rect2(Vector2.Zero, Size), background);

        // Ground line under the grid for orientation.
        for (int x = 0; x <= VehicleDesign.GridWidth; x++)
            DrawLine(new Vector2(x * CellPixels, 0f), new Vector2(x * CellPixels, VehicleDesign.GridHeight * CellPixels), gridLine);
        for (int y = 0; y <= VehicleDesign.GridHeight; y++)
            DrawLine(new Vector2(0f, y * CellPixels), new Vector2(VehicleDesign.GridWidth * CellPixels, y * CellPixels), gridLine);

        if (Design == null) return;

        foreach (var block in Design.Blocks)
        {
            var part = PartCatalog.Get(block.Part);
            if (part == null) continue;
            var rect = new Rect2(block.X * CellPixels + 1, block.Y * CellPixels + 1, CellPixels - 2, CellPixels - 2);
            DrawTextureRectRegion(Art.Parts, rect, Art.PartCell(part.Sprite));
        }

        foreach (var wheel in Design.Wheels)
        {
            var part = PartCatalog.Get(wheel.Wheel);
            var suspension = PartCatalog.Get(wheel.Suspension);
            if (part == null) continue;

            float scale = CellPixels / SoftVehicle.CellSize;
            Vector2 center = new Vector2(wheel.X + 0.5f, wheel.Y + 0.5f) * CellPixels;
            float size = Art.WheelCell * scale;
            bool supported = Design.BlockAt(wheel.X, wheel.Y - 1) != null;

            DrawLine(center, center - new Vector2(0f, CellPixels * 0.5f), suspension?.GodotColor ?? Colors.White, 4f);
            DrawTextureRectRegion(Art.Wheels, new Rect2(center - new Vector2(size, size) / 2f, new Vector2(size, size)),
                new Rect2(part.Sprite * Art.WheelCell, 0, Art.WheelCell, Art.WheelCell),
                supported ? Colors.White : new Color(1f, 0.4f, 0.4f));
        }

        if (VehicleDesign.InGrid(_hover.X, _hover.Y))
        {
            var rect = new Rect2(_hover.X * CellPixels, _hover.Y * CellPixels, CellPixels, CellPixels);
            DrawRect(rect, Eraser ? new Color(1f, 0.3f, 0.3f, 0.8f) : new Color(1f, 1f, 1f, 0.6f), false, 2f);
        }
    }
}
