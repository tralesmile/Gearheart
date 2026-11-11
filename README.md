# GEARHEART

*Dig it, build it, race it, live it.*

A 2D side-view sandbox where you mine, craft and build vehicles part by part, then race, fight and explore across several worlds. Built with Godot 4 (.NET / C#).

**Status:** Phase 1 — Prototype. The game opens in the **garage**: build a vehicle from 15 parts on a grid, then press **Test Drive**. Vehicle physics: soft body (approach B), chosen 2026-11-11.

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

## Test drive controls

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
  Garage/     garage screen and build grid
  Parts/      part definitions and the catalog loader
  Physics/    soft-body world: steps soft bodies, car-vs-car and car-vs-object contacts
  Vehicles/   vehicle parts, physics, damage
  World/      terrain and (later) the tile world
  Tests/      test scenes like the physics test
  Util/       shared helpers
scenes/       Godot scenes (.tscn)
assets/       art and audio (stored with Git LFS)
data/parts/   part definitions as data files
docs/         plan notes and the decision log
tests/        automated tests (GUT)
```

## Workflow

- `main` always builds and runs. New work happens on a branch and is merged through a pull request.
- Every push to `main` runs **GitHub Actions**, which exports Windows and Linux builds (see the Actions tab, then *Artifacts*).
- Big decisions go in [`docs/DECISIONS.md`](docs/DECISIONS.md).
