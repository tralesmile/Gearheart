using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Ore flies home: everything you mine, dig up or smash flies from where it broke into its slot on
/// the inventory bar, and the slot's number only goes up when it lands (with a pop and a tick).
/// </summary>
public partial class WorldScene
{
    private OreFlight _flight;
    private readonly TextureRect[] _slotIcons = new TextureRect[GameSession.Items.Length];
    private int _flightLogs, _valuableLogs, _landLogs;

    /// <summary>Called by BuildHud after the inventory bar, so the fliers draw over it.</summary>
    private void SetupFlyHome(CanvasLayer hud)
    {
        _flight = new OreFlight { Name = "OreFlight" };
        _flight.SlotCenter = slot => _slotIcons[slot] != null ? _slotIcons[slot].GetGlobalRect().GetCenter() : Vector2.Zero;
        _flight.OnLanded += (slot, carry, flew) =>
        {
            if (!GameSession.AutoPlay || _landLogs >= 30) return;
            _landLogs++;
            string item = GameSession.Items[slot];
            GD.Print($"[flyhome] landed {carry} {item} in slot {slot + 1} after {flew:0.00}s: bar shows {ShownCount(slot)} (have {GameSession.Count(item)}, still flying {_flight.Pending(slot)}) frame={Engine.GetFramesDrawn()}");
        };
        hud.AddChild(_flight);
    }

    /// <summary>The number on a slot: what you have, minus what is still on its way there.</summary>
    private int ShownCount(int slot) => Mathf.Max(0, GameSession.Count(GameSession.Items[slot]) - (_flight?.Pending(slot) ?? 0));

    /// <summary>Colour of the glow behind a flying icon.</summary>
    private static Color FlightColor(string item) => item switch
    {
        "Dirt" => new Color(0.75f, 0.55f, 0.35f),
        "Stone" => new Color(0.75f, 0.77f, 0.8f),
        "Iron" => new Color(1f, 0.68f, 0.55f),
        "Copper" => new Color(1f, 0.74f, 0.35f),
        "Crystal" => new Color(0.55f, 1f, 1f),
        _ => new Color(0.92f, 0.95f, 1f),
    };

    /// <summary>
    /// Sends items (already added to the session) flying from a point in the world to their slot.
    /// Dirt and stone dug by the car's drill just count up: a tunnel would bury the screen in icons.
    /// </summary>
    private void FlyHome(Vector2 worldAt, string item, int amount)
    {
        if (_flight == null || amount <= 0) return;
        if (_driving && item is "Dirt" or "Stone") return;
        _flight.Sfx = _sfx;
        Vector2 screen = GetViewport().GetCanvasTransform() * worldAt;
        int before = _flight.Launched;
        _flight.Launch(screen, item, amount, FlightColor(item));
        // Log every valuable launch (up to 40) but only the first few dirt and stone ones.
        bool valuable = item is not ("Dirt" or "Stone");
        if (GameSession.AutoPlay && _flight.Launched > before && (valuable ? _valuableLogs++ < 40 : _flightLogs++ < 24))
        {
            int slot = System.Array.IndexOf(GameSession.Items, item);
            GD.Print($"[flyhome] launched {amount} {item} as {_flight.Launched - before} icon(s) from screen ({screen.X:0},{screen.Y:0}) to slot {slot + 1} at ({_flight.SlotCenter(slot).X:0},{_flight.SlotCenter(slot).Y:0}): " +
                     $"bar shows {ShownCount(slot)} (have {GameSession.Count(item)}) frame={Engine.GetFramesDrawn()}");
        }
    }

    private static Vector2 TileCenter(int x, int y) => new((x + 0.5f) * Ts, (y + 0.5f) * Ts);

    /// <summary>True when every slot shows exactly what you have (nothing stuck in the air).</summary>
    private bool BarSettled()
    {
        for (int i = 0; i < GameSession.Items.Length; i++)
            if (ShownCount(i) != GameSession.Count(GameSession.Items[i])) return false;
        return true;
    }

    private string FlightSummary() => _flight == null ? "flight=none"
        : $"flown={_flight.Launched} landed={_flight.Landed} unitsHome={_flight.UnitsHome} inFlight={_flight.InFlight} longestFlight={_flight.LongestFlight:0.00}s barSettled={BarSettled()}";
}
