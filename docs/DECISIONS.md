# Decision log

Newest first. One entry per big decision: what, why, and what would make us revisit it.

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
