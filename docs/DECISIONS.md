# Decision log

Newest first. One entry per big decision: what, why, and what would make us revisit it.

## 2026-11-11 — Garage: side-view grid of 1x1 blocks; parts as data

- **What:** Vehicles are designed on a 12 x 6 side-view grid. Each block is one part (frame, engine, armor, fuel tank, drill). Wheels sit in the cell under a block, with a chosen suspension. All 15 parts are defined in `data/parts/parts.json`. The soft body is generated from the design: 4 points per block, shared between neighbours, with edge and diagonal beams. Every beam takes its strength from its part.
- **Strength tuning (offline simulation):** yield limits were raised (light frame 2.5%, steel 3.5%, heavy 5%) after 1% caused damage during normal driving. Now the starter car takes no damage driving, damage is light at 50 km/h into a wall and a few beams snap at 112 km/h. The light buggy can crack when it lands the cliff jump; the heavy truck shrugs off 112 km/h. Two starter cars crashing head-on never pass through each other.
- **Repair (prototype):** R in the test drive rebuilds the car from its design (free full repair + refuel). See D6 in OPEN_DECISIONS.md.
- **Revisit when:** You answer D3 (grid vs free placement) or playtests show the grid feels limiting.

## 2026-11-11 — Vehicle physics: soft body (approach B) chosen

- **What:** Vehicles use our own node-and-beam soft-body solver. Simple loose objects (crates, rocks, debris) stay on Godot's rigid-body physics. A shared `SoftPhysicsWorld` steps all soft bodies together and couples them to rigid objects with impulses (the hybrid model from the plan).
- **Why:** Spectacular, varied crashes are a core pillar; the car builder maps naturally onto points and beams; the code is ours, which helps host-run multiplayer and the destructible tile world.
- **Collision results (offline simulation):** head-on crashes at 20, 50 and 90 km/h each never let cars pass through each other, and damage scales with speed (snapped beams only at 50+ km/h). A car pushes a parked car about 13 m in 4 s. A car knocks a crate ahead without overlapping it. No unstable (NaN) results.
- **Revisit when:** B feels floaty or annoying to drive after a week of tuning, or 8 cars cannot hold 60 FPS on a Steam Deck. Then fall back to A (rigid parts).

## 2026-11-11 — Soft-body approach B built for comparison

- **What:** Node-and-beam car (18 mass points, 50 beams) solved with position-based dynamics: 8 substeps x 4 iterations at 120 Hz. Beams dent past 1.5% stretch and snap after 18% total bend; suspension snaps after 40%.
- **Why these numbers:** Tuned in an offline simulation of the same solver: normal driving causes no damage, a 34 km/h wall hit leaves light dents, 112 km/h snaps a few beams, 169 km/h wrecks the body. It never produced invalid (NaN) positions in any test.
- **Known limits:** B collides with the terrain only. Crates and car-to-car collisions need our own broad-phase if B is chosen.
- **Next:** Measure FPS for A and B with 1, 4 and 8 vehicles, judge feel, and record the choice here.

## 2026-11-11 — Physics test starts with approach A

- **What:** The first test scene uses rigid parts with groove + spring suspension and wheels that detach on hard impacts.
- **Why:** It is fast, stable and uses Godot's built-in 2D physics. Approach B (node-and-beam soft body) is built next in the same scene for comparison.
- **Revisit when:** Both approaches have been measured with 1, 4 and 8 vehicles (end of prototype week 4).

## 2026-11-11 — Engine: Godot 4 with C#

- **What:** Godot 4.6.x, .NET edition. C# for physics, world and simulation; GDScript allowed for UI and quick game logic.
- **Why:** Free with no royalties at any revenue, strong 2D tools, small builds, Linux and Steam Deck support.
- **Revisit when:** Console ports become a real plan (needs a porting partner).
