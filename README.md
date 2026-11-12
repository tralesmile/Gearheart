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
| Space | Handbrake (hold it with D/A at a standstill for launch control) |
| Shift | Rocket boost (needs a Rocket Booster part; burns fuel fast) |
| X | Drill on / off |
| F | Get out |
| R | Repair and refuel (at the garage) |

**The loop:** mine ore by hand → open the garage → spend materials on parts (a Drill Head costs 4 Scrap and 2 Iron) → drive further and dig deeper for Iron and Copper. Scrap is shallow, Iron starts about 2 m down, Copper about 5 m down. It gets dark underground; you carry a lamp.

**Engines you can hear:** every engine part has its own sound — the Scrap 4-Cylinder buzzes and rattles, the V6 hums, the V8 burbles — and a pretend gearbox (4, 5 or 6 gears) makes the note climb, drop at each shift and climb again. The dashboard shows the gear over a rev bar that blinks red on the limiter (free-spinning wheels in the air hit it). Floor it through a shift or lift off at high revs and the exhaust pops a flame; getting in cranks the starter. The gearbox is sound and show only: it does not change how the car drives.

**Launch control:** stopped? Hold Space and D (or A): the revs climb up the rev bar and the gear box shows "L". Let go of Space when the bar reaches the green window at the top ("GO") for a **PERFECT START** (+18 km/h and an exhaust pop); a bit early is a GOOD START (+8 km/h), far too early is BOGGED DOWN. Hold on past the top and the revs bounce off the limiter and climb again, so you can wait for the next window.

**Stuck?** If you hold the throttle and the car won't move (wedged in a notch, nose against a step), the game says **STUCK!**. Tap Q, E, Q, E with the throttle held: each tap bounces the car on its springs and shoves it forward, and taps in a steady rhythm bounce it higher (ROCK IT! x2 … x5) until it's **ROCKED FREE!**

**Gate sprint:** every standing start on the garage pad is timed over 20 m, like a drag strip. The clock starts the moment the car leaves the line (the launch charge doesn't count) and a ticking SPRINT timer shows top right; at 20 m a call-out gives your time and whether it beat your best ("20 m SPRINT 2.91 s · NEW BEST! 0.18 s faster"). Your best sprint shows on the status line while you stand on the pad. A perfect start is worth a few tenths.

**Rocket Booster:** 2 Scrap, 2 Iron and 3 Copper buy a booster for the back of the car. Hold Shift (or RB / X on a gamepad) for thrust: long jumps, steep hills and spectacular crashes. Long jumps get a "BIG AIR" call-out with the airtime, and hard landings and crashes shake the screen and throw sparks.

**Buried stashes:** something glints in the rock? Dig towards it. Old Toolboxes lie shallow, Lost Supply Crates deeper, and Ancient Strongboxes (a pile of Iron and Copper) deep down or out in the far biomes. The pickaxe and the drill both open them. There is one a short walk from the garage on each side.

**On the road:** fuel cans and scrap heaps lie along the surface every 10–19 m. Drive through a can for +18 fuel (it stays put if your tank is nearly full). Plough through a scrap heap at speed to smash it for Scrap (sometimes Iron in the meadow and the north, Copper in The Wastes), or walk into it to pick it up. Both come back a couple of minutes after you take them. The fuel readout blinks LOW FUEL under 20%.

**Trips:** leave the garage grounds (about 18 m out) and a trip starts; the status line shows how far out you are and your farthest ever ("Trip 84 m (best 212 m)"), with a "NEW FARTHEST!" cheer when you pass it. Come back to the garage and a **trip report** ticks in: farthest, top speed, jumps (longest air and good landings), scrap heaps and best streak, fuel cans, stashes, haul and carnage, each with points, then the trip score and your best trip to beat (a fanfare and NEW BEST tags when you beat it). Enter or Esc closes it.

**Kickers:** a yellow sign with a ramp on it means a jump kicker ahead: a steep dirt ramp (a dune lip in The Wastes, a snow cornice in the Frozen North) with a sharp crest. Hit it fast, keep the nose level with Q/E in the air and land flat for a PERFECT LANDING. The first one is just past the garage gate on each side. Every kicker keeps your longest jump: a chequered flag stands where you came down, with the distance over it, so on the next run you can see the mark to clear ("LONG JUMP 7.4 m · NEW RECORD! (+1.2 m)"). A roof landing or nose dive is measured but doesn't count.

**Trip goals:** every trip has one bonus goal ("Go 50 m out", "Reach the Frozen North", "Smash 4 scrap heaps", "Fly 1.0 s in one jump", "Dig up a buried stash"...). It shows in gold under the status line before you leave, tracks your progress on the way, and pays 100–200 bonus points on the trip report when done. Distance and speed goals grow with your records.

**Landings:** every jump of 0.6 s or more is judged as you touch down. Land on all wheels at once with the car matching the slope (tilt with Q/E in the air) for a **PERFECT LANDING**: a burst of speed, warm streaks and a ta-DING. All wheels down without the body touching is a **CLEAN LANDING**. Land on the roof, nose or tail and the game names it (ROOF LANDING, NOSE DIVE, TAIL SLAM, BELLY FLOP). Perfect and clean landings score on the trip report's "Jumps" row.

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

Every build runs the game headless at a fixed 120 FPS and fails on any error: a bot walks, mines and places blocks in the world, then drives the car (once with the starter pickup, once tunnelling with a drill car); plus the garage, the physics lab, the Hill Climb and the old boulder test. The build also renders real screenshots with software graphics and pushes them to the `screenshots` branch. Progress lines (`[autoplay]`, `[race]`, `[mining]`, `[stunt]`, `[crash]`, `[stash]`, `[pickup]`, `[trip]`, `[ice]`, `[landing]`, `[kicker]`, `[engine]` (starter, shifts, exhaust pops; `--design=v8` and `--design=scrap4` swap the starter's engine), `[launch]` (launch-control starts and plain starts: speed and distance 0.5, 1 and 2 s after; `--launchtest` gets straight in and lets go in the green after one limiter bounce, `--launchearly` lets go far too early), `[stall]` (per-wheel numbers when a car does not move off after getting in), `[sprint]` (20 m gate sprints from the pad: time, launch grade, best before; `--launchtest` reverses back onto the pad for a second sprint), `[rock]` (stuck, every rock, rocked free; `--rocktest` digs a trench past the pad with a wall too tall to drive up, `--rockdepth=N` sets its depth in tiles, `--norock` is the control run without rocking); `--crashtest` drops the car from 15 m, `--stashtest` buries stashes under the bot's pickaxe, `--triptest` makes two out-and-back drives for the trip report, `--icetest` lays 40 m of ice by the garage and drives across it, for plain and spiked tyres; `--jumptest` plants long-jump record flags before driving: one to beat, the rest out of reach) appear in the build log. Run one yourself:

```
godot --headless --path . --fixed-fps 120 res://scenes/world.tscn --quit-after 4800 -- --fresh --autoplay
```

## Workflow

- `main` always builds and runs. New work happens on a branch and is merged through a pull request.
- Every push to `main` runs **GitHub Actions**, which exports Windows and Linux builds (see the Actions tab, then *Artifacts*).
- Big decisions go in [`docs/DECISIONS.md`](docs/DECISIONS.md).
