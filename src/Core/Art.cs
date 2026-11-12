using Godot;

namespace Gearheart.Core;

/// <summary>The game's textures. Layouts are described in docs/ART.md.</summary>
public static class Art
{
    private static Texture2D _tiles, _player, _parts, _wheels, _garage, _decor, _pickups;

    public static Texture2D Tiles => _tiles ??= GD.Load<Texture2D>("res://assets/art/tiles.png");
    public static Texture2D Player => _player ??= GD.Load<Texture2D>("res://assets/art/player.png");
    public static Texture2D Parts => _parts ??= GD.Load<Texture2D>("res://assets/art/parts.png");
    public static Texture2D Wheels => _wheels ??= GD.Load<Texture2D>("res://assets/art/wheels.png");
    public static Texture2D Garage => _garage ??= GD.Load<Texture2D>("res://assets/art/garage.png");

    public static Texture2D Decor => _decor ??= GD.Load<Texture2D>("res://assets/art/decor.png");

    /// <summary>Roadside pickups: fuel can, then scrap heaps for the meadow, the Wastes and the Frozen North.</summary>
    public static Texture2D Pickups => _pickups ??= GD.Load<Texture2D>("res://assets/art/pickups.png");

    public const int PickupCell = 32;
    public const int DecorWidth = 48, DecorHeight = 64;

    /// <summary>Decor cells of the kicker warning sign, arrow pointing right (east) or left (west).</summary>
    public const int DecorKickerRight = 8, DecorKickerLeft = 9;

    /// <summary>Decor cells of the long-jump record flag, flying right (east) or left (west).</summary>
    public const int DecorRecordFlagRight = 10, DecorRecordFlagLeft = 11;
    public const int PartSize = 22;
    public const int WheelCell = 56;

    /// <summary>Region of a 16 px cell in tiles.png.</summary>
    public static Rect2 TileCell(int column, int row) => new(column * 16, row * 16, 16, 16);

    /// <summary>Region of a part in parts.png.</summary>
    public static Rect2 PartCell(int index) => new(index * PartSize, 0, PartSize, PartSize);

    /// <summary>Inventory icon cell for an item key.</summary>
    public static Rect2 ItemIcon(string item) => item switch
    {
        "Dirt" => TileCell(0, 6),
        "Stone" => TileCell(1, 6),
        "Scrap" => TileCell(2, 6),
        "Iron" => TileCell(3, 6),
        "Crystal" => TileCell(7, 6),
        _ => TileCell(4, 6),
    };
}
