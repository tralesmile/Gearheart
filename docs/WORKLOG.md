# Work log

Newest entry first. Each session: what was built and why, how it was verified, what is not verified, and what comes next.

## 2026-11-12, about 01:15–02:10 UTC — Buried stashes (PR #9)

**Built**

- **Three new tiles** (`Tile.StashSmall/Mid/Big`): Old Toolbox (shallow; 5–8 Scrap, 1–2 Iron), Lost Supply Crate (6.5–15 m; 3–5 Scrap, 3–5 Iron, 1–2 Copper), Ancient Strongbox (deeper; 4–6 Scrap, Iron and Copper). World generation buries one every 9–16 m, 2–28 m deep, never floating in a cave; the far biomes are richer (35% chance of the next kind up). Plus one guaranteed shallow toolbox 15–18 m to each side of the garage. Loot is fixed per stash. Both the pickaxe and the drill open them.
- **Glints** (`src/World/StashGlints.cs`): every stash within about 12 m twinkles (a faint steady glow plus a star flash every ~2.7 s, gold for strongboxes). It is drawn on its own canvas layer, so it shows through the underground darkness. The first time you come near one: "Something glints in the rock...".
- **The big find**: a two-line call-out ("ANCIENT STRONGBOX! / +4 Scrap +4 Iron +5 Copper"), a new `treasure` fanfare (rising arpeggio with shimmer, `tools/make_sounds.py`), a gold coin fountain, a small screen shake, and "Stashes found N" on the on-foot status line (saved in `progress.json`).
- **Art**: row 7 of `tiles.png` (`tools/make_art.py`, own random stream; rows 0–6 checked byte-identical).
- **Save**: world format GHW5 → GHW6, so old worlds regenerate.
- **CI**: `--stashtest` bot flag (buries a toolbox and a strongbox under the bot's pickaxe, and a crate nearby to show the glint), a smoke run and a screenshot run (`stash_*.png`), `[stash]` lines in every world run. Also fixed CI: the movie frames in `shots/` were inside the Godot project, so every run re-imported them, the **game builds shipped thousands of screenshot PNGs**, and the export flooded the job log so the playtest lines could not be read. A `.gdignore` fixes all three; the run went from about 9 to 5 minutes.

**Why this** — Brainstorm in `docs/IDEAS.md`: looking at the drill screenshots, underground is one flat grey and digging is a steady trickle of "+2 Scrap" with no moments and no direction. Buried treasure was ranked next after crash juice and is the "big find" moment from the brief. A glint you can see in the dark gives a goal ("dig over there"), and the jackpot gives the moment. Last session was polish, so this one is a feature.

**Verified (CI runs 43134745097, 43136021517, both green; final run on the PR)**

- World has 51 stashes from the regular placement (18 toolboxes, 14 crates, 19 strongboxes, 2.0–25.8 m deep) before the two near-garage toolboxes were added.
- Stash test: the bot's pickaxe opened the toolbox (+8 Scrap +1 Iron) and the strongbox (+4 Scrap +4 Iron +5 Copper); inventory went to Scrap 12, Iron 5, Copper 5, "Stashes found 2".
- Screenshots `stash_32/40.png`: stash tiles and glints show, the "Something glints in the rock..." hint fires; `stash_50.png`: "ANCIENT STRONGBOX!" call-out with the loot line and the gold fountain.
- Nothing else changed: starter drive still ends at tile x=2240.4 at 40 s, drill car 8.8 m deep with Scrap 22 / Iron 3 / Copper 1, Hill Climb 18.97 s and 15.25 s.

**Not verified / known issues**

- The fanfare has not been listened to (no audio in CI).
- The glint in the dark could not be seen in a screenshot yet (the test stash is near the surface in daylight). The layer is outside the darkness by construction, but please check underground.
- Stash tiles always use a dark stone background, which looks like a pocket in dirt too; fine for now.
- 19 strongboxes hold about 95 Copper in total. That may make copper too easy once a player learns to dig deep; tune after playing.
- The bot's drill car does not pass through a stash in its 40 s run, so drill-opening is verified only by code path (same `TileBroken` event as the pickaxe).

**Next**

1. Metal detector beep that speeds up near a stash (small; doubles the tension of digging).
2. Trip summary card back at the garage: distance, best air, ore, stashes, crashes (small).
3. Landing grades ("CLEAN LANDING" / "BELLY FLOP") (small).
4. Surface pickups between biomes (fuel cans, scrap heaps) (small).
5. Snow/ice part (tracks or spiked tyres) from Frozen North iron.

## 2026-11-12, about 00:20–01:30 UTC — Crash juice (PR #8)

**Built** (a polish-and-feel session)

- **Hit detection** in `SoftVehicle`: a hard hit is either a body node slamming into the ground (its approach speed, read from how deep it sank in the first solver pass) or a landing after more than 0.3 s in the air (falling speed into the ground under the wheels, so driving over crests never counts). Thresholds: 13 km/h for the body, 21 km/h for wheel landings. Dents, snapped beams and lost wheels from the next 4 ticks join the same hit, which is reported once through a new `Crashed` event (`src/Vehicles/CrashEvent.cs`). Drill tips are ignored, so tunnelling does not count as crashing.
- **`CrashFx`** (`src/Game/CrashFx.cs`), used in the world and in Hill Climb / Free Drive: screen shake (trauma model, shake grows with the square, up to 22 screen px), a dust burst in the colour of the ground it hit, a spray of sparks when metal hits the ground or a beam snaps, flying bolts on a snap, and sounds that get louder and lower the harder the hit. Call-outs: "CRASH!" for a hard body slam, "WRECKED!" for 3+ snapped beams, "LOST A WHEEL!"; they never overwrite a fresh stunt call-out.
- **Sounds** (`tools/make_sounds.py`, own random stream so the old sounds are byte-identical): `thud` (landing boom), `crunch` (grinding metal slam), `snap` (crack and ping). `Sfx.Play` takes an optional pitch.
- **HUD fix:** the driving status line ("... Damage 0") was cut off at the right edge of a 1280-wide window. Letting it grow to the left made it overlap the controls hint (seen in run 43130090741's screenshots), so it now sits on its own second row at the top right.
- **Bot / CI:** new `--crashtest` flag (drops the starter car from about 15 m with the bot at the wheel), a new smoke run and screenshot run for it, `[crash]` lines in every log, and the workflow copies the screenshot frames of the first crashes (`crash_<run>_<frame>.png`).

**Why this** — In the brainstorm (`docs/IDEAS.md`), the most disappointing moment in the game was a 3-second "MONSTER AIR" jump ending with the car on its roof in silence: no shake, no sound, damage 0. Crashes are a design pillar, the booster made them more common, and the last session put crash juice first in "Next". It is also time for a polish session after two feature sessions.

**Verified (CI runs 43127771135 and 43128785061, both green)**

- The first run showed wheel landings fired 18 hits in 40 s of normal driving (any 0.3 s hop). After raising the wheel-landing threshold to 21 km/h, the second run has: starter car **7 hits in 40 s** (hardest 35 km/h, shake 0.10–0.25, all on the wheels), rocket car **13 hits** including a 53 km/h landing (shake 0.45) and the roof landing after the 3.2 s jump as a **39 km/h body hit with shake 0.63 and "CRASH!"**, drill car 1 hit (tunnelling causes none).
- Crash test: the 15 m drop lands at **74 km/h, 1 beam snapped, 11.3 dent, shake 0.82**; the HUD shows damage 16 afterwards.
- Screenshots on the `screenshots` branch: `crash_rocket_132/133.png` (roof landing: "CRASH!", sparks and dust), `crash_crash_19/20.png` (the drop: big spark and dust spray).
- Nothing else changed: starter world drive still ends at tile x=2240.4 at 40 s, drill car 8.8 m deep, Hill Climb 18.97 s (starter) and 15.25 s (rocket), all identical to main.

**Not verified / known issues**

- Not listened to (no audio in CI) and the shake cannot be seen in still frames. Both are tuned by numbers only; they may feel too strong or too weak. Please play and say.
- Car-to-car and car-to-crate hits are not detected (only hits against the ground).
- Hitting a wall at speed with only the wheels touching it counts as a landing only if the car was airborne; a head-on wheel hit on flat ground makes no effect.
- Damage still barely happens: a 15 m drop costs 1 beam. That is a balance question for later (idea 7, "damage that bites").

**Next**

1. Landing grades and a trip score ("CLEAN LANDING" vs "BELLY FLOP"), building on the new hit detection (small).
2. Buried treasure underground for the "big find" moment (small–medium).
3. Surface pickups (fuel cans, scrap heaps) between biomes, which also gives booster fuel a trade-off (small).
4. Snow/ice part (tracks or spiked tyres) from Frozen North iron.

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
