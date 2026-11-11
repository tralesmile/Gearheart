# GEARHEART

*Dig it, build it, race it, live it.*

A 2D side-view sandbox where you mine, craft and build vehicles part by part, then race, fight and explore across several worlds. Built with Godot 4 (.NET / C#).

**Status:** Phase 1 — Prototype, feature complete. The game opens in the **garage**: build a vehicle from 15 parts on a grid, then choose **Free Drive** (explore, smash crates, mine ore with a drill) or **Hill Climb** (race the clock to the finish). Vehicle physics: soft body (approach B), chosen 2026-11-11.

**Decisions waiting on you:** [docs/OPEN_DECISIONS.md](docs/OPEN_DECISIONS.md)

## Setup

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer.
2. Install **Godot 4.6.x .NET** from [godotengine.org/download](https://godotengine.org/download).
3. Clone this repo, open Godot, click **Import**, and select `project.godot`.
4. Press **F5** (Play). The first run restores NuGet packages and builds the C# code, which takes about a minute.

## Garage

- Pick a part on the right, then **left click or drag** on the grid to place it. **Right click or drag** removes.
- Wheels go in the cell **directly under a block**. Choose their suspension (Soft or Sport) before placing.
- A design needs at least one engine and one wheel, and all blocks must touch.
- **Save / Load** to 3 slots. **Starter car** loads the default design.
- Parts live in `data/parts/parts.json`: change numbers there to rebalance without touching code.

## Modes

- **Free Drive:** rolling hills, crate stacks and ore rocks. Put a **Drill Head** on the front of your car and press its tip into rocks to mine **Scrap**, **Iron** and **Copper** (rarer further out). Materials are saved and shown in the garage.
- **Hill Climb:** the track keeps rising and gets steeper. The clock starts at the START flag and stops at FINISH, about 340 m on, with slopes up to about 40 degrees near the end. Stopping for 4 seconds (stuck or out of fuel) ends the run. Your best time is saved.

## Drive controls

| Key | Action |
| --- | --- |
| D / Right arrow | Accelerate |
| A / Left arrow | Brake / reverse |
| Q / E | Tilt the car (air control) |
| Space | Handbrake |
| R | Repair, refuel and reset the car |
| G | Back to the garage |
| V | Switch physics: B soft body / A rigid parts |
| C | Spawn an oncoming car (head-on crash test) |
| 1 / 4 / 8 | Benchmark with 1, 4 or 8 vehicles |
| Esc | Quit |

A controller also works: triggers drive, left stick tilts, A is the handbrake.

What to look for: does building speed, jumping the ramp and crashing feel fun? Compare A and B: which feels better to drive, and which crashes look better? Note the FPS with 1, 4 and 8 vehicles in each mode.

- **A (orange car):** Godot's rigid-body physics. Hard crashes rip wheels off. Collides with crates.
- **B (blue car):** our own soft-body solver. The body dents and tears, wheels can come off. Collides with the ground, other cars and crates.

## Project structure

```
src/
  Core/       session state shared between scenes
  Game/       drive scene: free drive and hill climb
  Garage/     garage screen and build grid
  Parts/      part definitions and the catalog loader
  Physics/    soft-body world: steps soft bodies, car-vs-car and car-vs-object contacts
  Vehicles/   vehicle parts, physics, damage
  World/      terrain, ore rocks and (later) the tile world
  Tests/      test scenes like the physics test
  Util/       shared helpers
scenes/       Godot scenes (.tscn)
assets/       art and audio (stored with Git LFS)
data/parts/   part definitions as data files
docs/         plan notes and the decision log
tests/        automated tests (GUT)
```

## Automated playtests

Every build runs the game headless at a fixed 120 FPS and fails on any error: the garage, the physics lab, 40 seconds of Hill Climb with the starter car, and 40 seconds of Free Drive with a drill car. Progress lines (`[autodrive]`, `[race]`, `[mining]`) appear in the build log. Run one yourself:

```
godot --headless --path . --fixed-fps 120 res://scenes/drive.tscn --quit-after 4800 -- --hillclimb --autodrive
```

## Workflow

- `main` always builds and runs. New work happens on a branch and is merged through a pull request.
- Every push to `main` runs **GitHub Actions**, which exports Windows and Linux builds (see the Actions tab, then *Artifacts*).
- Big decisions go in [`docs/DECISIONS.md`](docs/DECISIONS.md).
