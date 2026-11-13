using System;
using System.Collections.Generic;
using System.Linq;
using Gearheart.Core;
using Gearheart.Parts;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The "next part" tracker: a card above the inventory bar shows the upgrade you are closest to
/// (its icon and "Iron 2/4, Copper 0/3"), counting with the bar as ore lands. When the last ore
/// lands it turns gold with a call-out and a chime, so a full bar becomes a reason to head home.
/// </summary>
public partial class WorldScene
{
    private NextPartChip _nextChip;
    private PartGoal _partGoal;
    private readonly HashSet<string> _readyAnnounced = new();
    private string _readyPending;      // a ready call-out waiting for the current one to be read
    private bool _nextPartPrimed;      // the first look at the bar never announces (a save that is ready already)
    private float _nextPartCheck;
    private string _nextLogKey = "";
    private int _nextLogs, _partsReady;
    private string _firstReady = "";

    /// <summary>Called by BuildHud, before the ore fliers, so the icons fly over the card.</summary>
    private void SetupNextPart(CanvasLayer hud)
    {
        _nextChip = new NextPartChip { Name = "NextPart" };
        hud.AddChild(_nextChip);
    }

    /// <summary>What the bar shows for an item (ore still in the air does not count yet).</summary>
    private int ShownItem(string item)
    {
        int slot = Array.IndexOf(GameSession.Items, item);
        return slot >= 0 ? ShownCount(slot) : GameSession.Count(item);
    }

    private void UpdateNextPart(float dt)
    {
        if (_nextChip == null) return;
        _nextChip.Visible = !_card.Showing;

        // A few times a second is plenty; but every frame while ore is landing, so the moment it
        // turns ready is the moment the last icon lands.
        _nextPartCheck -= dt;
        bool landing = _flight != null && _flight.InFlight > 0;
        if (_nextPartCheck <= 0f || landing)
        {
            _nextPartCheck = 0.25f;
            var goals = NextPart.Goals(GameSession.CurrentDesign, ShownItem);
            _partGoal = NextPart.Pick(goals, _partGoal?.Part.Id);

            foreach (var g in goals.Where(x => !x.Ready)) _readyAnnounced.Remove(g.Part.Id);
            foreach (var g in goals.Where(x => x.Ready && !_readyAnnounced.Contains(x.Part.Id)).ToList())
            {
                _readyAnnounced.Add(g.Part.Id);
                if (!_nextPartPrimed) continue;
                _readyPending = g.Part.Id;
                _partsReady++;
                if (_firstReady == "") _firstReady = g.Part.Name;
                // The tracker jumps to the part that just became ready.
                if (_partGoal == null || !_partGoal.Ready || _partGoal.Part.Id != g.Part.Id) _partGoal = g;
                if (GameSession.AutoPlay)
                    GD.Print($"[nextpart] READY {g.Part.Name}: needs {NeedText(g)}, bar shows {HaveText(g)} frame={Engine.GetFramesDrawn()}");
            }
            _nextPartPrimed = true;
            LogNextPart();
        }

        _nextChip.Set(_partGoal, ShownItem, NearGarage());

        // The call-out waits for a fresh stunt or vein call-out to be read first.
        if (_readyPending != null && _toastTime < 1.2f && !_card.Showing)
        {
            var part = PartCatalog.Get(_readyPending);
            _readyPending = null;
            if (part != null && _partGoal?.Part.Id == part.Id)
            {
                Toast($"PART READY: {part.Name}!\n{(NearGarage() ? "press G to build it" : "head to the garage to build it")}");
                _toastTime = 2.6f;
                _sfx.Play("ready", -6f, 0.5f, 1f);
                _nextChip.Flash();
            }
        }
    }

    private static string NeedText(PartGoal g) => string.Join(" ", g.Need.Select(kv => $"{kv.Key} {kv.Value}"));

    private string HaveText(PartGoal g) => string.Join(" ", g.Need.Select(kv => $"{kv.Key} {ShownItem(kv.Key)}"));

    /// <summary>Logs the tracker when its part or progress changes (the first 30 changes).</summary>
    private void LogNextPart()
    {
        if (!GameSession.AutoPlay || _partGoal == null) return;
        string key = $"{_partGoal.Part.Id}:{_partGoal.Progress:0.00}";
        if (key == _nextLogKey || _nextLogs >= 30) return;
        _nextLogKey = key;
        _nextLogs++;
        GD.Print($"[nextpart] showing {_partGoal.Part.Name} (tier {_partGoal.Part.GoalTier}) needs {NeedText(_partGoal)}, " +
                 $"bar shows {HaveText(_partGoal)}, progress {_partGoal.Progress:0.00}{(_partGoal.Ready ? " READY" : "")} frame={Engine.GetFramesDrawn()}");
    }

    private string NextPartSummary() =>
        _partGoal == null ? "nextPart=none" : $"nextPart={_partGoal.Part.Id}:{_partGoal.Progress:0.00}{(_partGoal.Ready ? ":READY" : "")} partsReady={_partsReady}{(_firstReady != "" ? $" firstReady=\"{_firstReady}\"" : "")}";
}
