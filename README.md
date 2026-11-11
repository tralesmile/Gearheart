# GEARHEART

*Dig it, build it, race it, live it.*

A 2D side-view sandbox where you mine, craft and build vehicles part by part, then race, fight and explore across several worlds. Built with Godot 4 (.NET / C#).

**Status:** Phase 1 — Prototype. Current build: physics test (approach A, rigid parts + breakable joints).

## Setup

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer.
2. Install **Godot 4.6.x .NET** from [godotengine.org/download](https://godotengine.org/download).
3. Clone this repo, open Godot, click **Import**, and select `project.godot`.
4. Press **F5** (Play). The first run restores NuGet packages and builds the C# code, which takes about a minute.

## Physics test controls

| Key | Action |
| --- | --- |
| D / Right arrow | Accelerate |
| A / Left arrow | Brake / reverse |
| Q / E | Tilt the car (air control) |
| Space | Handbrake |
| R | Reset the car |
| 1 / 4 / 8 | Benchmark with 1, 4 or 8 vehicles |
| Esc | Quit |

A controller also works: triggers drive, left stick tilts, A is the handbrake.

What to look for: does building speed, jumping the ramp and crashing feel fun? Hard crashes rip wheels off. Note the FPS with 1, 4 and 8 vehicles.

## Project structure

```
src/
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
