# Work log

Newest entry first. Each session: what was built and why, how it was verified, what is not verified, and what comes next.

## 2026-11-11, about 22:30–23:30 UTC — Rocket Booster and stunt call-outs (PR #7)

**Built**

- New part category **Booster** with one part, the **Rocket Booster** (2 Scrap, 2 Iron, 3 Copper; `data/parts/parts.json`). Mount it on the back; hold **Shift** (gamepad RB or X) to fire. Thrust 5.2 kN-equivalent along the car's nose, fading out towards about 96 km/h, burns 3 fuel/s on top of the engine. Placement matters: low boosters lift the nose, high ones push it down.
- Looks and sound: a nozzle bell on the back of the block, a flickering three-layer flame, a spark-to-smoke plume, a generated rocket roar loop (`tools/make_sounds.py`, `boost.wav`), new part sprite (`tools/make_art.py`, cell 11), "BOOST!" in the HUD.
- **Stunt call-outs** for every car: jumps longer than 0.9 s show "BIG AIR / HUGE AIR / MONSTER AIR 1.4 s" with a chime. The camera pulls back a little at speed.
- Garage lists the part under BOOSTERS with its thrust and fuel use. New test design `--design=rocket` (starter pickup + booster) and `--design=starter`.
- CI: two new runs (world with the rocket car, hill climb with the rocket car), a rocket screenshot set, and `[stunt]` lines in the log. Bot runs now pin their design; before, a run silently reused whatever design the previous run saved.

**Why this** — Brainstorm (see `docs/IDEAS.md`): the most boring minute is the long drive over rolling hills where the car just bobs along at 25–30 km/h. A booster changes the feel immediately, makes jumps and crashes bigger, lets a weak car get up a hill that stopped it, and gives copper (near the surface in The Wastes) a purpose. It is also on the owner-facing roadmap (D21 option A, "boosters").

**Verified (CI run 43121311996, green)**

- Hill Climb, same autopilot: starter pickup **18.97 s**, rocket pickup **15.25 s** (3.7 s faster); the rocket car finishes with 4 fuel left of 75, so boosting the whole way is right at the edge.
- World, rocket car (autopilot boosts whenever the nose is between -24° and +20°): 14.8 s of boost in 30 s of driving, top speed 68 km/h (starter: 52 km/h), jumps of 1.27 s, **3.21 s** and 1.11 s, damage 0. Starter car on the same route: longest jump 0.93 s.
- Screenshots (`screenshots` branch, `rocket_*.png`): flame, plume, "BOOST!" HUD and the "BIG AIR! 1.3 s" toast all show; the garage shows the booster on the grid.
- All earlier runs unchanged: starter world drive reaches tile x=2240 at 40 s as before, the driller tunnels 8.8 m deep, 18.97 s hill climb matches main.

**Not verified / known issues**

- The sound has not been listened to (no audio in CI). The roar is generated noise and may be harsh.
- The bot's rocket car ends 130 tiles *behind* the starter car in the world run: after the 3.2 s jump it lands on its roof and self-rights, and it nearly runs out of fuel (4 left). A human who boosts in bursts should do better, but this means boosting is a risk, not free speed. That is intended; tell me if it feels bad.
- Particles are simple squares; fine at speed, plain up close.
- Keyboard Shift only on the left/right Shift physical key; no remapping yet.

**Next**

1. Crash juice: screen shake, sparks and a metal crunch when beams dent or snap (small; crashes are a pillar and now happen more often).
2. Buried treasure underground (chests or car wrecks with a rare part) for the "big find" moment.
3. Surface pickups between biomes (fuel cans, scrap heaps), which also makes the booster's fuel cost a choice.
4. Snow/ice part (tracks or spiked tyres) from Frozen North iron, the next biome-tied part.
