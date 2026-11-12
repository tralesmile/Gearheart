# GEARHEART

*Dig it, build it, race it, live it.*

A 2D side-view sandbox where you mine, craft and build vehicles part by part, then race, fight and explore across several worlds. Built with Godot 4 (.NET / C#).

**Status:** Phase 1 — Prototype. You play a mechanic in a destructible tile world: walk, jump and mine by hand, collect Scrap, Iron and Copper, build and upgrade your car at the garage, then get in and drive it. A drill on the car tunnels through the ground.

**Decisions waiting on you:** [docs/OPEN_DECISIONS.md](docs/OPEN_DECISIONS.md) · **Art layout:** [docs/ART.md](docs/ART.md)

## Setup

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer.
2. Install **Godot 4.6.x .NET** from [godotengine.org/download](https://godotengine.org/download).
3. Clone this repo, open Godot, click **Import**, and select `project.godot`.
4. Press **F5** (Play). The first run restores NuGet packages and builds the C# code, which takes about a minute.

## How to play

**On foot**

| Input | Action |
| --- | --- |
| A / D | Walk (one-tile steps are climbed automatically) |
| Space / W | Jump |
| Hold left mouse | Mine the highlighted tile (within reach) |
| Right mouse | Place a block (dirt or stone) |
| 1 / 2 | Choose dirt or stone |
| F | Get in the car (when next to it) |
| G | Open the garage (when next to it) |

**Driving**

| Input | Action |
| --- | --- |
| A / D | Drive |
| Q / E | Tilt (air control, or to point the drill down) |
| Space | Handbrake |
| Shift | Rocket boost (needs a Rocket Booster part; burns fuel fast) |
| X | Drill on / off |
| F | Get out |
| R | Repair and refuel (at the garage) |

**The loop:** mine ore by hand → open the garage → spend materials on parts (a Drill Head costs 4 Scrap and 2 Iron) → drive further and dig deeper for Iron and Copper. Scrap is shallow, Iron starts about 2 m down, Copper about 5 m down. It gets dark underground; you carry a lamp.

**Rocket Booster:** 2 Scrap, 2 Iron and 3 Copper buy a booster for the back of the car. Hold Shift (or RB / X on a gamepad) for thrust: long jumps, steep hills and spectacular crashes. Long jumps get a "BIG AIR" call-out with the airtime, and hard landings and crashes shake the screen and throw sparks.

**Buried stashes:** something glints in the rock? Dig towards it. Old Toolboxes lie shallow, Lost Supply Crates deeper, and Ancient Strongboxes (a pile of Iron and Copper) deep down or out in the far biomes. The pickaxe and the drill both open them. There is one a short walk from the garage on each side.

**On the road:** fuel cans and scrap heaps lie along the surface every 10–19 m. Drive through a can for +18 fuel (it stays put if your tank is nearly full). Plough through a scrap heap at speed to smash it for Scrap (sometimes Iron in the meadow and the north, Copper in The Wastes), or walk into it to pick it up. Both come back a couple of minutes after you take them. The fuel readout blinks LOW FUEL under 20%.

**Trips:** leave the garage grounds (about 18 m out) and a trip starts; the status line shows how far out you are and your farthest ever ("Trip 84 m (best 212 m)"), with a "NEW FARTHEST!" cheer when you pass it. Come back to the garage and a **trip report** ticks in: farthest, top speed, longest air, scrap heaps and best streak, fuel cans, stashes, haul and carnage, each with points, then the trip score and your best trip to beat (a fanfare and NEW BEST tags when you beat it). Enter or Esc closes it.

**Trip goals:** every trip has one bonus goal ("Go 50 m out", "Reach the Frozen North", "Smash 4 scrap heaps", "Fly 1.0 s in one jump", "Dig up a buried stash"...). It shows in gold under the status line before you leave, tracks your progress on the way, and pays 100–200 bonus points on the trip report when done. Distance and speed goals grow with your records.

**Spiked Tyres:** 2 Scrap and 4 Iron buy studded wheels. On snow and ice they keep nearly full grip, where plain tyres spin and slide (and spray ice); on bare ground they grip a little less than standard wheels. The first time your plain tyres spin on ice the game tells you about them, and the first time the spikes bite it cheers.

**Biomes:** the garage stands in the **Home Meadow**. Drive about 95 m east to reach **The Wastes** (sand dunes, sandstone, copper close to the surface) or west to the **Frozen North** (snow, rugged hills, more caves, lots of iron, and ice patches with almost no grip, also on the hills).

The world, your materials and your car are saved automatically (`user://world.bin`, `progress.json`, `designs/current.json`). Start the game with `-- --fresh` to begin again.

## Garage

- Pick a part on the right, then **left click or drag** on the grid to place it. **Right click or drag** removes.
- Wheels go in the cell **directly under a block**. Choose their suspension (Soft or Sport) before placing.
- A design needs a cab, an engine and a wheel, and all blocks must touch.
- **Build & Drive** pays the material cost (removed parts are refunded) and returns to the world. **Hill Climb** is a separate timed race track.
- Parts live in `data/parts/parts.json`: change numbers there to rebalance without touching code.

## Automated playtests

Every build runs the game headless at a fixed 120 FPS and fails on any error: a bot walks, mines and places blocks in the world, then drives the car (once with the starter pickup, once tunnelling with a drill car); plus the garage, the physics lab, the Hill Climb and the old boulder test. The build also renders real screenshots with software graphics and pushes them to the `screenshots` branch. Progress lines (`[autoplay]`, `[race]`, `[mining]`, `[stunt]`, `[crash]`, `[stash]`, `[pickup]`, `[trip]`, `[ice]`; `--crashtest` drops the car from 15 m, `--stashtest` buries stashes under the bot's pickaxe, `--triptest` makes two out-and-back drives for the trip report, `--icetest` lays 40 m of ice by the garage and drives across it, for plain and spiked tyres) appear in the build log. Run one yourself:

```
godot --headless --path . --fixed-fps 120 res://scenes/world.tscn --quit-after 4800 -- --fresh --autoplay
```

## Workflow

- `main` always builds and runs. New work happens on a branch and is merged through a pull request.
- Every push to `main` runs **GitHub Actions**, which exports Windows and Linux builds (see the Actions tab, then *Artifacts*).
- Big decisions go in [`docs/DECISIONS.md`](docs/DECISIONS.md).
