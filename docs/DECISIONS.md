# Decision log

Newest first. One entry per big decision: what, why, and what would make us revisit it.

## 2026-11-12 — Launch control: a timing window, not a reaction test

- **What:** at a standstill (under about 3 km/h, a wheel on the ground), holding the handbrake and the throttle arms launch control. A charge climbs from idle to the top in 1.1 s and is shown on the rev bar (the gear box reads "L", then a green "GO"). Letting go of the handbrake with the throttle still down grades the start by the charge: 0.75–0.97 **PERFECT START** (+18 km/h shove along the body, exhaust pop, cheer), 0.45–0.75 **GOOD START** (+8 km/h), lower **BOGGED DOWN** (nothing extra). Holding past the top bounces off the limiter back to 0.6 and climbs again, so a missed window is retried by holding on, not by starting over. Letting go of the throttle first cancels it.
- **Why:** the first second of every trip was the dullest; a rising bar with a window is readable at a glance and rewards rhythm, not reflexes (the window is about 0.24 s wide and comes round every 0.44 s while you hold). The shove uses the same overdrive trick as a perfect landing, so the engine keeps pushing past its usual top for a moment.
- **Measured (CI):** starter V6 on the pad, 2 s after letting go: perfect 19.0 m / 42 km/h, bogged 14.1 m / 35 km/h, plain start 14.4 m / 39 km/h; 0.5 s after: 32 vs 14–16 km/h.
- **Revisit when:** you have played (D1): the window width (`LaunchPerfectFrom/To`), the shove (`LaunchPerfectKick`) and the rise time are constants at the top of `SoftVehicle`. If perfect starts feel mandatory, shrink the shove; if nobody finds the feature, add a first-time toast at the garage.

## 2026-11-12 — Found: the car physics loses small forces far from x = 0 (float precision)

- **What:** node positions are stored as absolute world pixels in 32-bit floats. The garage stands at about x = 20,600 px, where a float can only step 0.002 px; the Verlet integrator keeps velocity as the difference of two positions, so any change smaller than about 1.9 px/s per substep is rounded away. Reproduced offline with a Python port of the solver: at x = 20,500 the Scrap 4-Cylinder pickup sits at −8.9 px/s with full throttle and the V8 pickup is stuck at 82 px/s (4.6 km/h); at x = 500 both pull away normally. The CI log matches (Scrap 4: −8 px/s creep, wheels grounded, no slip; V8: 6 km/h for over 2 s), and in the flat Free Drive scene near x = 0 the Scrap 4 pickup reaches 20 km/h in 2 s.
- **Consequence:** every car's handling in the world is subtly off from what the formulas say, and it gets coarser further east (above x = 32,768 the step doubles). Parked cars creep by about 8 px/s.
- **Planned fix:** keep each car's nodes relative to a local origin (a multiple of 1,024 px, moved as the car drives) and add it only when talking to the world (terrain, drawing, crashes). Not done in PR #20; see the work log.

## 2026-11-12 — Gears are for the ears only

- **What:** The car has a pretend gearbox (`src/Vehicles/Gearbox.cs`) that drives the engine sound and the dashboard's gear and rev bar. It never changes torque or top speed. Gears and revs follow the car's road speed (plus a little for real wheelspin), not the wheel spin, and shifts are at least 0.6 s apart.
- **Why:** Real gear physics would change every car's handling and every balance number, and the soft-body tyres slip so much that wheel spin sits near its top almost all the time (gears picked from it hunted 31 times in 40 s in CI). The fun of gears here is the sound of the revs climbing and dropping, which needs none of that.
- **Revisit when:** A part should change how the car pulls (a turbo, a gearbox part, launch control), or the owner wants manual shifting.

## 2026-11-12 — The Deep: a slate layer with glow crystals, and what crystals are for

- **What:** below `DeepDepth` = 48 rows (12 m) under the natural ground, give or take 6 rows by noise, stone becomes **Slate** (hardness 36 vs 28, drops Stone) and caves open wider (cave threshold −0.08). After the caves are dug, slate tiles touching a cave grow **glow crystals** where a noise field passes 0.28 (inside the rock only above 0.62, as rare seams). Crystals drop the new **Crystal** material (hardness 30). The first slate row per column is saved with the world (save **GHWA**).
- **Look:** crystals are redrawn above the underground darkness at full brightness, with an additive cyan halo that breathes and the odd sparkle; crystals still buried glow at about a third, so a seam shows through the rock. Ore inside slate is drawn cooler and darker so its stone background does not glare. A "THE DEEP" title card the first time you reach it in a session; "THE DEEP" on the status line while there.
- **Crystal use:** the **Crystal Drill** (3 Iron + 4 Crystal, drill power 150 vs 60, cyan cone). A part made from the Deep that gets you into the Deep faster is the smallest loop that makes the new material matter. Crystals also count on the trip report's haul (worth as much as Copper).
- **Depth record:** the deepest point reached (player or car, metres below the surface line of that column) is kept in progress.json across worlds; "NEW DEPTH RECORD" is called out at 5 m and then every 2.5 m past the last call-out, and the status line shows "Depth 9 m (best 14 m)" for the player and the car.
- **Why:** six sessions in a row improved the surface drive; underground was still one grey rock at every depth. Terraria's and Minecraft's best underground moments are breaking into a new, different-looking layer.
- **Revisit when:** you have played (D1): if crystals are too common (about 8,500 tiles on this seed, a hundred in view in a deep cavern) raise the 0.28 threshold; if the Deep is too far down for players on foot, lower `DeepDepth`. Next steps for the Deep are in the work log.

## 2026-11-12 — Long-jump records per kicker

- **What:** a jump is measured from the kicker's crest edge to the car's centre at the first touchdown (only if the car took off within 1.2 s of crossing the crest, flew at least 0.25 s and came down at least 1.5 m out). The longest landed jump per kicker is saved with the world (GHW9) and marked by a chequered flag on the ground with the distance over it. Roof landings, nose dives, tail slams and belly flops are measured and told ("6.1 m, but it doesn't count") but never set a record. The call-out rides on the landing grade's call-out instead of adding a second one.
- **Why per kicker, saved with the world:** each kicker is a different jump, so one global "longest jump" would only ever be beaten on the biggest kicker; a record per kicker gives a small challenge every 30–40 m. Records belong to the kicker's place, so they live in the world file, and a new world starts with no flags.
- **Why centre-to-crest:** simple and consistent between cars; it slightly favours long cars (their centre sits further back at take-off and lands further forward). Fine for personal records, not for comparing cars.
- **Revisit when:** jumps are compared between players or cars (then measure the rear wheel), or if the flags clutter the road once every kicker has one.

## 2026-11-12 — Jump kickers: shape and placement

- **What:** world generation raises the surface line into kickers facing away from the garage: an ease-in (one tile over three columns), then one tile every two columns (about 27°) to a crest 4–7 tiles (1.0–1.75 m) above the run-up, then a back slope of one tile every three columns down to wherever the ground is. First kicker about 23 m out on each side (shifted up to 15 m to find a good spot), then one every 26–42 m. A spot needs six level columns of run-up and ground that does not rise under the face or just past the crest. The fill below a kicker uses the natural depth, so caves, ore and stashes do not move. World save GHW8 stores the kicker list.
- **Why this shape:** a steeper face than any hill makes the take-off obvious and gives air at normal speeds; the ease-in stops the car slamming into a corner (run 2 without it: the starter car reared up and rolled); the long back slope means the drive home is an ordinary climb and the outbound landing is on a down slope. Kickers on rising ground had no crest (the car just climbed a wall), so those spots are skipped.
- **Rejected for now:** a bot that levels the car in the air (it over-rotated and made landings worse); kickers in both directions (the return trip would face a 27° wall or need a symmetric pyramid that throws the car nose-up).
- **Revisit when:** you have played (D1): if kickers feel too small, raise the rise or let players hit them faster (longer run-ups); if the starter car rears up too easily, soften the face to 1 tile per 3 columns plus a lip.

## 2026-11-12 — Landing grades and the perfect-landing kick

- **What:** after every jump of 0.6 s or more (`LandingJudge`), the landing is graded. PERFECT: all attached wheels down within 0.12 s of the first, the car within 15° of the ground under its wheels at touchdown, no body hit, under 0.5 damage; verdict 0.25 s after touchdown. CLEAN: all wheels down within 0.5 s, no body hit, under 2 damage. Otherwise rough (no call-out except the old BIG AIR cheer for jumps over 0.9 s). A body hit while still in the air after 0.6 s+ is a named botched landing: ROOF LANDING (pitch over 100°), NOSE DIVE / TAIL SLAM (leading end down or up by 25°+ with not all wheels down), BELLY FLOP otherwise.
- **Kick:** a perfect landing adds 210 px/s + 80 px/s per second of air beyond 0.6 s, capped at 2 s extra (about 12 to 21 km/h), applied within 0.05 s along the way the car is rolling (a 0.3 s spread was tried and carried the car less far), with wheel spin scaled up so the tyres do not brake it away, and an "overdrive": the engine may spin past its normal top speed by the kick, fading over about 1.5 s, so the engine keeps pushing if the kick takes the car past its top speed. Measured by A/B in the starter bot run (identical until the first perfect landing at tile x=2035): 2 s later the car is 19–25 tiles (5–6 m) further on than with no kick. Warm speed streaks for 0.8 s and a new `land` sound.
- **Score:** 30 per perfect, 10 per clean landing, folded into the trip report's air row (renamed "Jumps") so the card keeps its size. Botched landings already score as carnage (crashes).
- **Also fixed:** lying on the roof after a botched landing no longer counts as air time (before, a car rolling back off its roof could get a false BIG AIR).
- **Why:** jumps happen constantly on the generated hills, but what you did in the air did not matter. This gives Q/E air control a purpose and turns the touchdown into a skill moment you feel in the car (Hill Climb Racing, Trials). No save change.
- **Revisit when:** you have played (D1): if perfect landings feel random, widen the angle (15°) or the spread (0.12 s); if the kick feels like a free booster, shrink it.

## 2026-11-12 — Spiked Tyres: a grip floor on snow and ice, not a better tyre

- **What:** Wheels can have `frozenGrip` in `parts.json`. On snow or ice (asked through `ISurfaceInfo.FrozenAt`), the ground's grip multiplier is raised to at least that value. Spiked Tyres: radius 20, grip 1.0 (standard 1.1), frozenGrip 0.9 (plain: snow 0.75, ice 0.22), mass 1.2, cost 2 Scrap + 4 Iron.
- **Why:** The first biome-tied part should change how the car drives *in one place* rather than be a strict upgrade, so the garage choice means something: spikes win on the Frozen North, standard wheels on bare ground.
- **Ice over the hills:** ice patches used to need a flat column, which on the rugged northern hills meant single tiles that no tyre noticed. Now any column in a patch is ice (noise threshold raised 0.1 → 0.25 so there are fewer, longer patches). World save bumped to GHW7, so old worlds regenerate. In the bot's left run the starter car still crosses into the north at about the same pace (no getting stuck), so plain tyres are slowed, not blocked.
- **Revisit when:** players say the north is a chore without spikes, or spikes feel compulsory.

## 2026-11-12 — Trip goals: a flat bonus, picked by trip number

- **What:** each trip gets one goal from ten kinds (distance, reach The Wastes, reach the Frozen North, smash N heaps, SMASH xN streak, air time, top speed, fuel cans, find Copper, dig up a stash). The first trip ever is always "Go 50 m out". Later goals are picked from the trip number with a fixed hash (never the same kind twice in a row), so a given trip always gets the same goal and the bot runs are repeatable. Distance and speed targets sit just past your records (best × 1.15 + 10 m; best + 4 km/h); heaps grow from 3 to 8 with the trip count; air is 1.0 s, or 1.6 s with a Rocket Booster. A done goal adds a flat bonus row to the report (100–200 points by difficulty); a missed one shows how close you got and scores 0.
- **Why a flat bonus, not "double points":** doubling the whole score would make the best trip mostly a matter of goal luck, and a goal like "grab 3 cans" would be worth more on a long trip than a short one. A flat bonus is about a third of a typical trip score: worth chasing, not the whole game.
- **Choices:** the goal is shown before you leave ("Next trip goal" under the status line), so you can choose your direction at the gate. Trips too short to report do not use up the goal (the next attempt has the same one). No reroll yet. Save formats unchanged (no new saved fields).
- **Revisit when:** you have played (D1): if goals feel like chores, add a reroll or a choice of two; if they feel good, add a goal streak multiplier (idea 35).

## 2026-11-12 — Trip report and trip score

- **What:** a trip is the time between leaving the garage grounds (18 m from the middle of the pad, on foot or driving) and coming back (within 8 m, less than 3 m underground). Trips with something to show (20 m out, a heap, a stash or any haul) end with a report card and a score: 2 per metre of farthest distance, 1 per km/h of top speed, 4 per tenth of a second of the longest air, 10 per heap plus 15 per streak step above x1, 5 per fuel can, 100 per stash, haul 2/5/8 per Scrap/Iron/Copper, and 5 per crash or roll-over ("carnage", because crashes are a pillar, not a failure). The best score and the best of each stat are saved in `progress.json` (`bestTrip`); no world format change.
- **Why:** coming home was a silent dead end. A number to beat after every trip is the cheapest strong "one more trip" hook (Hill Climb Racing's end screen). Scoring every kind of fun keeps all play styles worth it: a careful miner and a stunt driver both score.
- **Choices:** a new trip does not start while the card is showing (coasting past the garage would start one at once). Enter or Esc close it, not A/D or Space, so driving keys never skip it by accident. The score is points only; it pays nothing yet (paying out waits for D19, what materials are for).
- **Revisit when:** you have played a few trips (D1): if the score feels arbitrary, weight it towards distance; if carnage points feel wrong, drop them.

## 2026-11-12 — Driving HUD is a dashboard; heaps have weight; self-righting escalates

- **What:** Speed, fuel and damage moved from the top-right text line to a drawn dashboard (dial, gauges) at the bottom-left while driving; the top line keeps the biome and BOOST. Smashing a scrap heap on the ground costs 10% speed; chains within 2.5 s give "SMASH xN" and from x3 +1 Scrap each. Speed lines from 35 km/h sideways speed, wind sound from 30 km/h. Self-righting pushes harder the longer the car stays on its roof, with a small hop every 1.5 s.
- **Why:** speed has to be seen and heard to be felt; a smash that does not touch the car feels weightless; a car stuck on its roof with no way out is the worst minute the game can have.
- **Revisit when:** you play it — if the dashboard covers too much of the screen, or the slowdown makes heaps feel like a penalty, these are single constants (`HeapKeep`, `StreakWindow`, `SpeedLines.StartKmh`).

## 2026-11-12 — Roadside pickups: not saved, restock out of sight

- **What:** Fuel cans and scrap heaps are placed on the surface outward from the garage on both sides, one every 40–76 tiles (10–19 m) starting about 17 m out; about 30% are fuel cans. The current world has about 42 of them. A can gives +18 fuel and is only taken if at least 6 fuel fits in the tank (so it is never wasted). A heap gives 1–3 Scrap plus a chance of one ore by biome (Iron 20% in the meadow, Copper 35% in The Wastes, Iron 35% in the Frozen North), fixed per spot. Hitting a heap faster than 15 km/h is a "SMASH" (junk spray, crunch, small shake); slower, or on foot, it is just picked up. Pickups are positioned from the world's surface but are not part of the world file: the list lives in memory for the game session (so a garage visit does not restock them) and a taken pickup comes back after 150 s, only when the player is more than about 22 m away.
- **Why:** The drive between biomes was the most boring minute in two brainstorms running, and fuel never mattered. Something every few seconds to aim for, and a way to earn fuel back, makes the drive a small game and gives the booster's thirst a counterweight. Not saving them keeps the save format unchanged and makes every trip stocked.
- **Known trade-off:** restarting the game restocks the road immediately. That is a tiny exploit; fine for the prototype.
- **Revisit when:** scrap from driving outpaces mining by too much (tune `HeapLoot`), fuel is still never tight (fewer cans, or a smaller starter tank), or a crafting economy (D19) arrives.

## 2026-11-12 — Buried stashes: placement, loot and glints

- **What:** 53 stashes in the current world (one every 34–63 tiles, 9–16 m, plus one shallow toolbox 15–18 m to each side of the garage so a new player sees a glint in the first minute) are buried 2–28 m deep, never floating in a cave. The current seed gives 20 toolboxes, 14 supply crates and 19 strongboxes. Three kinds by depth: Old Toolbox (under 6.5 m: 5–8 Scrap, 1–2 Iron), Lost Supply Crate (6.5–15 m: 3–5 Scrap, 3–5 Iron, 1–2 Copper), Ancient Strongbox (deeper: 4–6 of each). Outside the meadow a stash is one kind richer 35% of the time, which rewards the drive out. Loot is fixed per stash (seeded by its position). They are tiles, so both the pickaxe and the drill dig them up. A twinkle over each stash within about 12 m of you is drawn on its own canvas layer, so the underground darkness never hides it.
- **Why:** Tunnelling was steady income with no moments and no direction. A visible glint gives a goal ("dig there"), the jackpot gives the moment; a strongbox pays more than half a Rocket Booster. Two or more shallow stashes are reachable by hand, so on-foot players find them too.
- **Save:** world format bumped to GHW6; old worlds regenerate (materials and the car are kept). `progress.json` gains `StashesFound` (old files load with 0).
- **Revisit when:** players report stashes are too easy (glint range: `StashGlints.FadeTo`) or too rare/rich (spacing and loot in `TileWorld.PlaceStashes` / `StashLoot`), or crafting tiers arrive (then strongboxes are the natural place for rare blueprints).

## 2026-11-12 — Crash feedback: how a "hit" is measured

- **What:** A hit is a body node slamming into the ground faster than about 13 km/h, or a landing after at least 0.3 s in the air faster than about 21 km/h (measured into the ground under the wheels). Each hit is reported once, with its dents, snapped beams and lost wheels, and drives screen shake, sparks, dust, sounds and call-outs (`CrashFx`).
- **Why this way:** measuring the change of the car's velocity was tried on paper first and rejected: driving through a dip at speed changes velocity as much as a real landing, so the screen would shake on every hill. The contact-depth and air-time measures only fire on real impacts. The first CI run showed 18 hits in 40 s of calm driving with a 13 km/h landing threshold; 21 km/h brought it to 7, all gentle.
- **Revisit when:** playtesters say shake is too much or too little (tune `ImpactThreshold`, `LandingThreshold` in `SoftVehicle` and `MaxShake` in `CrashFx`), or car-to-car combat arrives (then car contacts need the same reporting). A settings toggle for screen shake belongs in the settings menu when it exists.

## 2026-11-11 (evening) — Rocket Booster, first biome-tied part

- **What:** A new part category, Booster. The Rocket Booster (2 Scrap, 2 Iron, 3 Copper) goes on the back of the car; hold Shift (gamepad RB or X) for thrust along the car's nose direction. It costs 3 fuel per second on top of the engine, and the thrust fades out towards about 96 km/h (1700 px/s) so it cannot run away.
- **How the push works:** the force is spread over the whole car by mass, so it never stretches beams (no damage from boosting itself), plus the real turning effect of where the booster sits (mounted low it lifts the nose, mounted high it pushes the nose down). This keeps the soft body stable and still makes placement matter in the garage.
- **Why this part first:** of the default roadmap (biome parts, boosters, tracks), it changes how the car feels within seconds, makes the big moments (jumps, crashes, climbing a hill that stopped you) bigger, and gives copper, which sits near the surface in The Wastes, a use.
- **Stunt call-outs:** every jump longer than 0.9 s shows "BIG AIR / HUGE AIR / MONSTER AIR" with the airtime. Cheap, and turns the hills into a toy for every car.
- **Camera** pulls back a little above about 28 km/h so jumps stay in view.
- **Bot runs now pin their design** (`--design=starter`): before, a run inherited whatever design the previous run had saved, so "starter car" results could silently be another car.
- **Revisit when:** the owner finds boosting too strong or too thirsty (numbers are in `data/parts/parts.json`: `thrust`, `boostSpeed`, `boostFuelUse`).

## 2026-11-11 — Biomes, graphics pass, first sounds

- **Biomes by distance from the garage:** Home Meadow in the middle (380 tiles each way), The Wastes to the east (sand over sandstone, long dunes, copper from 9 tiles down), Frozen North to the west (snow, taller hills, more caves, richer iron, flat stretches of ice). Sky and far hills change colour with the biome.
- **Ice** has 22% grip for tyres and makes walking slippery; snow 75%, sand 85%.
- **Graphics:** trees, bushes, flowers, cacti, pines and rocks on untouched surface tiles; shaded edges where tiles meet open space; debris puffs when tiles break; the truck has a headlight, tail light, fenders, wheel dust and exhaust smoke.
- **Sound:** generated placeholder effects (engine and drill loops that follow speed and throttle, pickaxe, break, place, pickup).
- **Drill fix:** the bite's bottom is now the level the wheels stand on, so the tunnel floor stays flat when the car noses up a slope; tilting the nose down more than 14 degrees digs downward. In the playtest the drill car no longer wedges: it tunnels 8.8 m deep in 30 s and brings back stone, scrap, iron and copper.
- **Housekeeping:** build downloads are kept 5 days so storage does not fill up.

## 2026-11-11 — Direction change: on-foot character in a tile world

- **Why:** Owner feedback on the first prototype: it should be a game where you control a person who mines, builds a car and drives it, and it must look like a game. The car-only prototype with flat shapes missed that.
- **What:** The main scene is now a destructible tile world (640 x 192 tiles of 16 px, about 160 x 48 m; saved to disk). The player walks, jumps, mines tiles with a pickaxe and places blocks. The garage is a building in the world; parts cost mined materials (the starter pickup is free, removed parts are refunded). F gets in and out of the car. A Drill Head on the car chews tiles around its tip, so the car can tunnel. Underground is dark, with a lamp on the player or car.
- **Art:** placeholder pixel art generated by `tools/make_art.py` (tiles, character, car parts, wheels, garage). Car blocks are textured quads that bend with the soft body. Layout in `docs/ART.md` so a real pack or artist can replace files one by one.
- **Ramps:** a 16 px step stops a 20 px wheel dead (found in simulation: the car flipped or stalled on every step). Every exposed one-tile step is now smoothed into a ramp of rise 16 over run 32, worked out from neighbouring tiles, so dug terrain reshapes itself. Natural terrain changes height at most once every 3 tiles (about 18 degrees), which the starter V6 can climb.
- **Car look and handling:** wheels moved to the ends (wheelbase 110 px) and suspension shortened so wheels tuck under the body (soft: 14 px, stiffness 0.06). In simulation: no flips on generated hills, 5.5 m drop without damage.
- **Seeing the game:** CI now renders screenshots with software graphics and pushes them to the `screenshots` branch, so changes can be checked visually.
- **Kept:** soft-body physics, part data, garage editor, Hill Climb race, physics lab.

## 2026-11-11 — Race and mining for the prototype

- **Hill Climb:** one track, about 340 m from START to FINISH, slopes up to about 41 degrees near the end. In simulation the starter car finishes in about 21 s, a light buggy and a heavy truck in about 26–27 s, so design choices change the time. The clock runs between the flags; 4 seconds below 0.5 m/s (stuck or out of fuel) ends the run. Best time per track is saved in `user://progress.json`.
- **Mining:** ore is placed as loose boulders (Scrap, Iron, Copper; rarer further out), 46–60 px radius, heavy and slow to roll. Any car can push them; a Drill Head (its tip and its front corners) grinds them down, and they break at 30% health and pay out. Digging into the ground itself waits for the tile world in the vertical slice, because the prototype ground is a height line.
- **Changes found by the automated playtests** (each was a real problem in the game, not just in the test):
  - Small half-buried rocks were axle-high: wheels could not climb them and the drill could not reach them. → Big loose boulders.
  - Small round rocks acted as conveyor belts under spinning wheels. → Heavier boulders with strong rolling resistance.
  - Ramming a boulder at full throttle flipped the car backwards (strong engine, short wheelbase, drill force above the axles). → On-screen hint while drilling (ease off, press E); the autopilot does the same.
  - Soft cars could not push crates or boulders with a steady force (contact velocity was measured after the push-out). → Fixed in the contact solver: pushing now transmits force.
  - A wheel in the air took half the engine torque. → Torque goes only to grounded wheels (locked differential).
  - Crate stacks bulldozed into dips blocked the path. → Crates smash when hit faster than about 11 km/h.
  - The car fell off the world after the hill-climb finish. → End wall on every track.
- **Playtest result after the fixes:** in 40 s the drill car drives 141 m, smashes the crates, clears the ramp and mines 8 boulders (12 Scrap, 6 Iron, 1 Copper). The starter car finishes the hill climb in about 21 s.
- **Why:** answers the prototype question (is building and driving fun?) with the least code that will be thrown away.
- **Automated playtests:** CI drives both modes headless every build, so a broken race or mining loop shows up immediately.

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
