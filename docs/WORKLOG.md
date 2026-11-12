# Work log

Newest entry first. Each session: what was built and why, how it was verified, what is not verified, and what comes next.

## 2026-11-12, about 09:20–09:50 UTC — Long-jump record flags (PR #17)

**Started by** merging PR #16 (jump kickers) left open by the last run: its final build (run 43189928768) was green, the `[kicker]` lines matched its description and the screenshots showed the signs and kickers.

**Built**

- **Long jumps** (`WorldScene.MeasureLongJump`): after a take-off from a kicker's crest, the first touchdown measures how far past the crest the car came down (car centre from the crest edge). It is told with the landing grade: "LONG JUMP 4.2 m · flag planted" the first time, "… · NEW RECORD! (+2.2 m)" with a short higher-pitched fanfare when you beat the flag, "… · flag at 9.0 m" when you don't. Jumps too short to be graded (0.25–0.6 s of air) get their own call-out. Roof landings, nose dives and tail slams are measured but never count ("6.1 m, but it doesn't count").
- **Record flags** (`src/World/JumpMarks.cs`): a chequered flag on a red-and-white pole stands on the ground where the record landed, with the distance over it (white; "RECORD 4.2 m" in gold for 4 s after a new record, the flag dropping in with a bounce and a gold ring). New art in `tools/make_art.py` (decor cells 10/11, flag flies away from the kicker; the other cells are byte-identical).
- **Saved with the world**: world save **GHW9** stores each kicker's record (old worlds regenerate). Records are per kicker, so every kicker is its own small challenge.
- **CI**: `[kicker] long jump` lines (distance, air, take-off speed, record / no record / botched), `[kicker] approach` lines when the car nears a kicker with a flag, `longJumps=jumps/records/longest` on `[autoplay]`, a new `--jumptest` run (plants a 2.0 m flag on the first kicker east and 9.0 m flags on the other seven, so it checks both beating and missing a record) headless and as screenshots, and `longjump_*` / `flag_*` screenshots.

**Why this** — Brainstorm in `docs/IDEAS.md` (tenth session): kickers give jumps, but nothing about a jump was remembered, so the next kicker was just the next bump and there was no reason to hit one again faster. A number to beat is Hill Climb Racing's whole hook, and a flag where your best was is Trials'. It was "Next" item 1 last session, small, and turns every trip past the gate into a few personal records to chase.

**Verified (CI run 43191957784, green)**

- Long jumps measured in every driving run: starter 4.2 / 4.3 / 3.6 / 3.9 m (25–30 km/h, all "flag planted"); left run 4.4 and 4.5 m; Spiked Tyres left run 3.2 m and **7.6 m with a perfect landing**; rocket 5.8 and 7.0 m at 35 km/h, and a tail slam off the 187 m kicker measured 2.1 m "botched (does not count)" with records unchanged; trip test 3.2 and 8.0 m (39 km/h).
- Jump test: approach lines for the 2.0 m and 9.0 m flags; "4.2 m … **NEW RECORD (was 2.0 m)**" on the first kicker, then "no record (best 9.0 m)" on the next three (4.3, 3.6, 3.9 m). `longJumps=4/1/4.3m` at 40 s.
- No gameplay change: 40 s positions identical to main (starter 2189.5, drill 1473.5/97.2, left 603.2, spiked 300.1, rocket 2155.9), roll-overs the same, trip reports 259 and 443 as before, Hill Climb 18.97 s / 15.25 s. No errors.
- Screenshots: `longjump_jump_181.png` ("LONG JUMP 4.2 m / NEW RECORD! (+2.2 m)", gold "RECORD 4.2 m" flag with its ring on the meadow back slope), `longjump_jump_263.png` ("LONG JUMP 4.3 m / flag at 9.0 m" with the white "9.0 m" flag in the dunes), `longjump_rocket_256.png` (gold "RECORD 7.0 m" flag in The Wastes).

**Not verified / known issues**

- Fun is my judgement. At the bot's speeds jumps are 3–8 m; a player with a run-up and the booster should do much more, so flags will spread out.
- The `flag_*` approach screenshots are taken too early: the flag is still off-screen to the right in them (the flags do show in the `longjump_*` shots). Harmless; the screenshot offset could be later.
- When a scrap heap sits just past a kicker's landing, its SMASH call-out can show first and the long-jump call-out replaces it a moment later (seen in `longjump_jump_257/263`). Both are readable, but it is busy.
- The distance is measured car centre to crest, so long cars score slightly more (see DECISIONS).
- No sound check possible; the record fanfare reuses `record` at a higher pitch.

**Next**

1. Trip goal "Beat a kicker record" (S) — sends trips at the flags (IDEAS #58).
2. Perfect chain: "PERFECT x3" stacking the kick (S, #46).
3. Slow-motion beat at the peak of a big jump or a new record (S, #59).
4. A Wastes part: big-bore copper engine or Sand Paddles (S–M, #41), and engine sound per engine part (#54).
5. Glow caves or a lava seam to make digging deeper exciting (M, #62/#56).

## 2026-11-12, about 08:20–09:20 UTC — Jump kickers on the road (PR #16)

**Built**

- **Kickers from world generation** (`TileWorld.PlaceKickers` / `BuildKicker`, `Kicker` struct): a ramp built into the surface facing away from the garage: it eases in (one tile over three columns, like a normal hill), then climbs one tile every two columns (about 27°, steeper than any natural slope) to a sharp crest 1.0–1.75 m above the run-up, then a back slope of one tile every three columns that runs all the way down to the ground (so the drive home climbs it like any hill and never meets a wall). Meadow kickers are 1.0–1.5 m, Wastes and Frozen North 1.25–1.75 m. The first on each side sits just past the gate; then one every 26–42 m along the road. They only go where the run-up is level and the ground under the face is flat or falling, so the crest always stands out. On this seed: 8 kickers (E 57/107/144/187/242 m, W 74/145/292 m).
- **Material by biome**: a grass-topped dirt mound in the meadow, a sand dune lip in The Wastes, a snow-capped cornice in the north (never ice). Everything under a kicker is generated as if it were not there (caves, ore, stashes unchanged).
- **Warning sign** (`tools/make_art.py`, decor cells 8 and 9): a yellow board with a ramp-and-arrow icon and hazard stripes on a post, on the run-up four tiles before the face, mirrored for the west side. No trees or bushes on a kicker.
- **One-time hint**: "KICKER AHEAD! hit it fast · Q/E in the air · land flat" when you drive toward a kicker the first time (only if the screen is free and you are at least 4 m before it).
- Pickups are moved off kicker ramps (to just past the back slope).
- **Save**: world save bumped to **GHW8** (the kicker list is saved); old worlds regenerate.
- **CI**: `[kicker]` lines (world list, take-off with speed and pitch, which landing grade followed, homeward crossings), `kickers=jumps/launches` on `[autoplay]` lines, and `kicker_*` screenshots around the first two take-offs. The ice test flattens kickers on its strip (`TileWorld.RemoveKickers`) so its numbers stay comparable.

**Why this** — Brainstorm in `docs/IDEAS.md` (ninth session): the landing grades from last session are a skill with nowhere to practise it; jumps only came from random bumps. The meadow road just outside the gate, driven twice on every trip, was the flattest and dullest stretch. A kicker you can see coming (sign, steep face) is what Hill Climb Racing and Trials build their tracks around. It was "Next" item 2 last session, small, and felt in the first minute.

**Verified (CI runs 43183491598, 43185258953, 43186897219, 43188320845, all green)**

- Run 1 (level ground required for the whole face): only 2 kickers in the whole world, and screenshots showed a sheer wall at the end of the back slope where the dunes fell away. Run 2 (no level requirement, back slope runs down to the ground): 14 kickers, but on rising ground the face merged into the hill (no crest) and the starter car reared up on it and rolled over three times (`world_180.png`); 40 s position x=1684 against 2282 on main. Run 3: ease-in face, only on flat or falling ground (8 kickers); I also tried a bot that levels the car in the air with Q/E — it over-rotated, 0 perfect landings in all runs and changed the ice numbers, so it was removed. Run 4 is the final code.
- Run 4 take-offs: starter car 4 kickers at 25–30 km/h, 2 judged jumps (0.64 s, 0.77 s, rough); left run 2 take-offs, 1 clean (0.62 s); Spiked Tyres left run 2 take-offs, **1 PERFECT LANDING off a 1.25 m snow kicker** (0.79 s); rocket 4/4 kickers jumped at 28–40 km/h (0.61–0.98 s; 2 rough, 2 tail slams); trip test 2 take-offs + 2 homeward crossings at 15 and 26 km/h (the drive home gets over them).
- Nothing got stuck: 40 s positions vs main — starter 2190 (main 2282, −23 m, the cost of four jumps), left 603 (560), Spiked 300 (268), rocket 2156 (2156). Roll-overs the same as main (0/2/1/1). Drill car identical to main (1473.5, 97.2). Ice strip identical (plain 7.0 s, spiked 4.7 s). Hill Climb 18.97 s and 15.25 s (unchanged). Trip reports 259 and 443 (new best). No errors.
- Screenshots: `kicker_world_169.png` (sign on the run-up, car on the face), `kicker_rocket_249.png` (dune kicker with crest and long back slope, car in the air), `kicker_left_363.png` (snow cornice and the KICKER AHEAD hint), `kicker_left_192.png` from run 2 (meadow kicker seen from the far side).

**Not verified / known issues**

- Fun is my judgement. At the bot's speeds (25–40 km/h) a kicker gives about 0.6–1.0 s of air, similar to the best natural bumps; the value is that the jump is visible and reliable. With a booster or a run-up at 50+ km/h it should be much bigger, but the bot did not test that.
- The starter car rears up on the 27° face if you hit it slowly; a player can hold the nose down with Q/E. Whether that feels like a fair challenge or annoying needs you (D1).
- The bot does not use Q/E in the air, so most of its kicker landings are rough. A real player should get far more clean and perfect ones.
- Kicker spacing on this seed is uneven (57 → 107 m east), because spots on rising ground are skipped.
- I cannot hear anything; no new sounds this session.

**Next**

1. Long-jump mark past each kicker with a per-kicker distance record ("LONG JUMP 9.4 m, best 11.2 m") (small) — turns each kicker into a mini challenge.
2. Perfect chain: "PERFECT x3" stacking the kick (small).
3. A Wastes part: Sand Paddles or a big-bore copper engine (small–medium).
4. Lava seam deep underground with rich ore behind it (medium; first underground danger).
5. Engine sound per engine part (small).

## 2026-11-12, about 07:20–08:10 UTC — Landing grades and the perfect-landing kick (PR #15, polish session)

**Built**

- **Landing judge** (`src/Game/LandingJudge.cs`): every jump of 0.6 s or more is graded after touchdown. PERFECT = all wheels down within 0.12 s of the first, car within 15° of the ground at touchdown, no body hit, no damage (called 0.25 s after touchdown). CLEAN = all wheels down within 0.5 s, no body hit, under 2 damage. Otherwise rough (only the old BIG AIR cheer for 0.9 s+).
- **Botched landings get named** (`WorldScene.OnCarCrash`): a body hit while still in the air after a real jump is ROOF LANDING, NOSE DIVE, TAIL SLAM or BELLY FLOP with the air time. Lying on the roof no longer counts as air (it could give a false BIG AIR before).
- **Perfect-landing kick** (`SoftVehicle.Kick`): +12 to +21 km/h by air time, a near-instant jolt along the way the car rolls, wheel spin scaled with it, and a short engine "overdrive" so it may pass its top speed while the kick fades. Warm speed streaks for 0.8 s, call-out "PERFECT LANDING! / 0.6 s air +12 km/h", and a new `land` sound (thump, whoosh, ta-DING; `tools/make_sounds.py`, own random stream, all older sounds byte-identical).
- **Trip report**: the "Longest air" row is now "Jumps" ("0.7 s, 1 perfect + 1 clean"): air points + 30 per perfect + 10 per clean landing. Card size unchanged. No save change.
- **CI**: `[landing]` lines (grade, air, wheel timing, mismatch, speed, kick, running counts, "kick result"), `landings=perfect/clean/rough/flops` on `[autoplay]` lines, `landings=` on `[trip] report`, and `land_perfect_*` / `land_flop_*` screenshots.

**Why this** — Brainstorm in `docs/IDEAS.md` (eighth session): three feature sessions since the last polish pass, so this was a feel session. The bot flies 0.6–1.75 s off the hills in almost every run, but landing on your wheels or your roof ended the same way. The touchdown, the peak of every jump, was the dullest second. Landing grades + the speed kick were ideas #7 and #27, small, felt in the first minute, and give the Q/E air control a reason to exist.

**Verified (CI runs 43175769499, 43176889287, 43177736391, 43178851104, all green)**

- Grades across the bot runs (run 4): starter 1 perfect / 4 rough; left run 2 roof landings (both caught as ROOF LANDING with pitch −156° and 149°); rocket 2 clean + 2 roof landings; trip test 2 perfect + 1 roof landing; spiked-ice run 1 perfect; 15 m crash drop lands flat but with 16 damage → rough (run 1 called it clean; fixed by the damage rule). The bot does not aim its landings, so most are rough (mismatch 20–60°), which is what a careless player should get.
- **Kick A/B**: the starter run is identical up to the first perfect landing (tile x=2035, 37 km/h). Without the kick (main): x=2112 two seconds later, 2232 at 40 s. Instant 9 km/h kick (run 1): 2137 / 2276. Kick spread over 0.3 s (runs 2–3): 2123–2132 / 2251–2256. Final near-instant 12 km/h kick (run 4): 37 → 46–47 km/h, x=2142 / **2282: 7 m ahead after 2 s, 13 m by 40 s**. The 0.3 s spread was weaker, so it was dropped; the overdrive alone (run 3) did not change much.
- Screenshots: `land_perfect_world_342.png` ("PERFECT LANDING! 0.6 s air +12 km/h" in The Wastes), `land_flop_rocket_135.png` and `land_flop_trip_46.png` ("ROOF LANDING! 0.8 s of air", car on its roof); `land_flop_left_131.png` shows the roof landing call-out correctly waiting behind a fresh TRIP GOAL call-out. `trip_566.png` (run 1) shows the "Jumps 0.7 s, 1 perfect + 1 clean +68" row fitting the card.
- Unchanged: Hill Climb 18.97 s and 15.25 s, no errors in any run.

**Not verified / known issues**

- Fun is my judgement. Whether perfect landings feel earned or random, and whether the kick is too strong, needs you to play (D1). The thresholds (15°, 0.12 s) are first guesses.
- The trip test's first trip is shorter now (39 m instead of 85 m): the kicks and an early roof landing changed the bot's timed drive. Not a bug, but the trip numbers are not comparable with older logs.
- The warm speed streaks after a kick only show above 35 km/h, so at landing speeds they are faint; the call-out and sound carry it. I cannot listen to the `land` sound; I checked its waveform and spectrum only.
- Clean/perfect landings mid-judgement when you get out of the car are dropped (by design).

**Next**

1. Perfect chain: "PERFECT x3" for landings in a row, stacking the kick (small) — direct follow-up.
2. Jump kickers from world generation: dirt ramps on the road, dune lips, snow cornices (small) — jumps on purpose.
3. A Wastes part: Sand Paddles or a big-bore engine from copper (small–medium).
4. Frozen lake with a kicker in the Frozen North (small).
5. Goal kind "Land 2 perfect landings" (small; shifts the bot's goal sequence).

## 2026-11-12, about 06:20–07:30 UTC — Spiked Tyres and real ice (PR #14)

**Built**

- **Spiked Tyres** (`data/parts/parts.json`, `wheel_spiked`): 2 Scrap + 4 Iron, radius 20, mass 1.2, grip 1.0 (standard wheel 1.1) and a new `frozenGrip` of 0.9: on snow and ice the ground's grip never drops below that (plain tyres get 0.75 on snow, 0.22 on ice). A choice, not a strict upgrade. The garage shows "Snow/ice grip 0.9".
- **Physics** (`SoftVehicle.UpdateWheel`): the grip floor is applied when `ISurfaceInfo.FrozenAt` says the ground is snow or ice. Each wheel now remembers its leftover slip, whether it is on snow/ice and whether its studs are biting, exposed as `WheelContacts` for effects.
- **Ice spray** (`src/Game/TyreSpray.cs`): plain tyres that spin or slide on snow/ice throw a dense fan of pale ice chips; studs throw crisp white chips behind the tyre as they bite. Chips have a dark rim so they read against snow and sky.
- **Hints**: "ICE! Your tyres can't grip / Spiked Tyres bite here (garage)" the first time plain tyres keep spinning on ice; "SPIKES BITE!" the first time studs grip on ice. Both wait until the current call-out (for example TRIP GOAL) has been read instead of being lost.
- **Real ice** (`TileWorld.Generate`): ice patches in the Frozen North no longer need a flat column, so they run over the hills (fewer, longer patches: noise threshold 0.1 → 0.25). World save bumped to **GHW7**; old worlds regenerate.
- **Art**: a studded tyre with a blue-steel rim in `wheels.png` cell 3 (`tools/make_art.py`; the other sheets are byte-identical).
- **CI**: `--icetest` lays 40 m of ice (two tiles deep) just east of the garage and drives across at full throttle, logging `[ice]` lines; it runs headless and as screenshots for the starter car and the new `--design=spiked`. One more headless run drives left into the Frozen North on Spiked Tyres. `ice=`, `slip=`, `bite=` seconds are on every `[autoplay]` line.

**Why this** — Brainstorm in `docs/IDEAS.md`: in `left_330.png` the north is "the same road painted white"; the ice was single tiles between ramp steps and the car drove the same everywhere, so no part was ever a reason to go somewhere. A biome-tied part that changes how the car drives was "Next" item 1 last session and overdue on the roadmap, and the trip goals now send players north.

**Verified (CI runs 43168529697, 43168607017, 43169925613, 43170758076, all green)**

- Ice strip, 40 m, same start: **plain tyres 7.0 s (avg 20 km/h, top 31 km/h, 2.7 s sliding, 357 ice chips)**; **Spiked Tyres 4.7 s (avg 30 km/h, top 46 km/h, 0.0 s sliding, 1.2 s biting, 80 chips)**. On the plain car the speed falls from 28 to 14–16 km/h on the ice; on spikes it holds 26–29 km/h. No damage in either.
- "SPIKES BITE!" shows 0.3 s after the spiked car reaches the ice (`[ice] hint shown` at frame 18). In run 2 the cheers/hints were swallowed by the TRIP GOAL call-out (seen in the screenshots), which is why they now wait. The plain-tyre "ICE! Your tyres can't grip / Spiked Tyres bite here (garage)" hint did not fire in run 3 (its slip counter forgot too fast on bumpy ice); after the fix it shows 0.8 s onto the ice (frame 23, visible in `iceplain_27.png`).
- Left run into the Frozen North, starter car: x=876 at 30 s → 560 at 40 s, 0.5 s on ice, 0.9 s sliding; it does not get stuck (the run before this change reached 583). Same run on Spiked Tyres: 649 → 368 in the same 10 s, 0 s sliding, 3.1 s biting (it also got further overall because it did not flip in the meadow this time).
- Screenshots `iceplain_18/27.png` (thick spray at both wheels) and `icespiked_18/27.png` (studded wheels, white chips flying back) look right; `left_390.png` shows the north with snow and no single-tile ice specks.
- Everything else unchanged: trip reports 436 and 377, Hill Climb as before, all other bot runs green.

**Not verified / known issues**

- Ice in the real Frozen North is still sparse near its border (0.5 s on ice in the bot's 10 s there); a set-piece frozen lake would make the part shine more (IDEAS #40).
- Fun is my judgement: whether ice is a fun obstacle or a chore without spikes needs you to play (D1). Spike numbers are a first guess.
- No sound for studs or sliding (I cannot listen to check it).

**Next**

1. Frozen lake with a kicker jump in the Frozen North (small) — a set piece where tyres matter most.
2. A Wastes part: Sand Paddles or a big-bore engine from copper (small–medium), so each biome has its own answer.
3. Garage: highlight parts you can afford now / "need 1 Iron" (small).
4. Goal streak multiplier (small).
5. Smashable props per biome (cacti, snowmen) feeding the streak (small).

## 2026-11-12, about 05:20–06:00 UTC — Trip goals (PR #13)

**Built**

- **One goal per trip** (`src/Game/TripGoal.cs`): ten kinds — go N m out, reach The Wastes, reach the Frozen North, smash N scrap heaps, a SMASH xN streak, N s of air in one jump, hit N km/h, grab N fuel cans, find N Copper, dig up a buried stash. The first trip ever is always "Go 50 m out"; later ones are picked from the trip number (never the same kind twice in a row), with targets that grow with your records (distance: best × 1.15 + 10 m; speed: just past your best; heaps 3 → 8; air 1.0 s, or 1.6 s with a booster). Bonus 100–200 points by difficulty.
- **At the gate**: a gold "Next trip goal: Reach the Frozen North" line under the status line while you are at the garage, so you can choose your direction before leaving. Leaving the grounds pops a "TRIP GOAL" call-out with a click. On the way the line reads "GOAL  Reach the Frozen North  61/95 m"; when it is done: "GOAL DONE!" call-out, a short fanfare (`record` at a higher pitch), and the line turns green and flashes. Stunt call-outs wait 1.4 s so a fresh goal call-out can be read.
- **Report**: a "Trip goal" row with a green DONE tag and the bonus, or a grey MISSED tag with how close you got. Card widened to 680×520 for the extra row.
- **CI**: `goal=` on every `[trip] start` line, `[trip] goal done:` lines, goal fields on `[trip] report`, DONE/MISSED on report rows, and `goal_start_*` / `goal_done_*` screenshots. No save format change (nothing new is saved).

**Why this** — Brainstorm in `docs/IDEAS.md`: the trip report gave every trip a score, but every trip is the same trip (hold D east, smash, come back). Nothing pushes you west, underground or into the air. It was "Next" item 1 in the last worklog and the top-ranked open idea, and a player meets it on their first trip.

**Verified (CI runs 43161562839 and 43162432585, both green)**

- Trip test: trip 1 goal "Go 50 m out" done 4.1 s into the trip at 50 m; report 1 = 316 (same rows as before) + 120 goal = **436**, row "Trip goal: Go 50 m out -> +120 DONE". Trip 2 goal "Reach the Frozen North" (the bot drives east): report 2 row "Reach the Frozen North (0/95 m) -> +0 MISSED", score 377 against best 436, so trip 2 is no longer a new best (it was before the bonus) — correct behaviour. Trip 3's goal on the status line beside the card: "Next trip goal: Hit 55 km/h" (best 48 km/h; it read 50 before the round-up fix).
- World runs: starter car done at 50 m after 4.4 s; left run done at −50 m after 11.0 s (the west side counts); rocket after 7.2 s; crash test after 4.4 s; drill car (tunnelling) never gets 50 m out in 40 s, so not done (expected).
- Screenshots: `goal_done_trip_71.png` (GOAL DONE! call-out, green "GOAL DONE Go 50 m out +120" line), `goal_start_trip_274.png` (TRIP GOAL call-out, "GOAL Reach the Frozen North 0/95 m"), `trip_245.png` (DONE row, score 436, "Next trip goal: Reach the Frozen North"), `trip_611.png` (MISSED row).
- Run 1 showed two problems, fixed in the second commit: the MISSED row's text was cut off ("(0/95"), now drawn a size smaller; and "BIG AIR" replaced the TRIP GOAL call-out within half a second in two runs, now stunt call-outs wait. The speed target now rounds up so it is always above your best. Run 2: `goal_start_world_128.png` now shows "TRIP GOAL / Go 50 m out" while the car is in the air (BIG AIR waited), and `trip_611.png` shows the whole "Reach the Frozen North (0/95 m)" with the MISSED tag. All other numbers unchanged: Hill Climb 18.97 s and 15.25 s, trip scores 436 and 377.

**Not verified / known issues**

- Fun is my judgement: whether goals feel like a purpose or a chore needs you to play (D1).
- Goals are fixed per trip number; there is no reroll. A trip too short to report keeps the same goal for the next attempt.
- "Find N Copper" counts Copper gained anywhere on the trip (by hand, drill, heaps or stashes).
- The goal does not show in the Hill Climb scene (trips are world-only).

**Next**

1. Snow/ice part (spiked tyres or tracks) from Frozen North iron (medium) — a new part that changes how the car drives; overdue on the roadmap, and "Reach the Frozen North" goals now send players there.
2. Goal streak multiplier for consecutive goals (small).
3. Smashable props per biome (cacti, snowmen, fences) feeding the streak (small).
4. Landing grades and a perfect-landing speed kick (small).
5. Tow home when out of fuel far away (small).

## 2026-11-12, about 04:20–05:00 UTC — Trip report card (PR #12)

**Built**

- **Trips** (`src/Game/Trip.cs`, `WorldScene.UpdateTrip`): a trip starts when you (on foot or in the car) go more than 18 m from the middle of the garage pad and ends when you come back within 8 m (not deep underground). While out, the status line reads "Trip 84 m (best 212 m)", and passing your farthest point ever gives a "NEW FARTHEST!" call-out. A new trip does not start while the report card is still showing.
- **Trip report card** (`src/Game/TripCard.cs`, drawn in code like the dashboard): pops in at the garage, rows tick in one by one with a rising `tally` click (farthest + side + depth, top speed, longest air, scrap heaps + best streak, fuel cans, stashes, haul, carnage), each with its points; then the trip score counts up; then "Your first trip! Now beat it.", "Best trip 560 · 148 to beat it" or, on a new best, a gold pulsing border, "NEW BEST TRIP! old best 316" and a brassy `record` fanfare. Stats that beat their own record get a gold NEW BEST tag. Enter or Esc skips the count / closes it; it fades by itself after 10 s. Toasts are hidden while it shows.
- **Score** (see `docs/DECISIONS.md`): 2/m, 1 per km/h, 4 per 0.1 s of air, 10 per heap + 15 per streak step, 5 per can, 100 per stash, haul 2/5/8, 5 per crash or roll-over. Best trip saved in `progress.json` (`bestTrip`); world save unchanged, no version bump.
- **Sounds:** `tally.wav` and `record.wav` from `tools/make_sounds.py` (no random stream used; all older sounds checked byte-identical).
- **CI:** `--triptest` bot (into the car at once, out east 9 s and back, out 15 s and back, closes card 1 with Enter after 5 s), `[trip]` lines in every world run, `trip_*.png` screenshots (8, 30 and 60 frames after each report).

**Why this** — Brainstorm in `docs/IDEAS.md`: the drive out is busy now (heaps, cans, streaks, dashboard), but coming back is a silent dead end and everything the trip did is forgotten. It has been the top-ranked open idea and the first "Next" item for two sessions. A score with a best to beat is the cheapest strong "one more trip" hook (Hill Climb Racing's end screen) and it rewards every play style, including crashing. The last session was polish, so a feature was due.

**Verified (CI runs 43155164037 and 43156140600, both green)**

- Trip test, report 1: 85 m east, 18.9 s, top 48 km/h, air 0.9 s, 2 heaps (streak x2), 1 can, +6 Scrap, 2 crashes → score 316, "first trip". Report 2: passed the old farthest (85 m) at t=34.2 s with the cheer, reached The Wastes at 112 m, 51 km/h, 1.0 s air, 1 heap, 3 cans, 8 crashes and 3 roll-overs → **399, newBest=True, records=[farthest, top_speed]**. Same numbers in the headless smoke run and the 10 fps screenshot run.
- Screenshots `trip_245.png` (card 1 complete, "Your first trip! Now beat it.") and `trip_611.png` (card 2 with two NEW BEST tags and "NEW BEST TRIP! old best 316" in gold, status line "Trip … (best 112 m)") look right at 1280×720; the card does not cover the dashboard or the inventory bar.
- Nothing else changed: starter drive still ends at tile x=2231.7 at 40 s (10 heaps, streak x5), left run reaches x=583 in the Frozen North, drill car 8.8 m deep, Hill Climb 18.97 s and 15.25 s.
- That run showed the bot coasting 22 m past the garage after report 2 and starting a new trip while the card was up ("Trip 22 m" behind the card). Fixed in the second commit (no new trip while the card shows). Run 2: no trip starts after report 2; the bot closes card 1 with Enter 5 s after it appears, at 26 m out, and trip 2 starts at once (t=26.9 s). Report 2 is now 377 (best before 316, newBest=True, records=[farthest]; top speed 46 km/h is lower because the first seconds of the drive out happened while card 1 was up). `trip_223.png` shows the rows sliding in one by one with the time as 0:18; `trip_275.png` shows the card gone and the status line "Trip 30 m (best 85 m)".

**Not verified / known issues**

- Not listened to: the tally clicks and the record fanfare are tuned by reading the numbers only.
- Fun of the scoring is my judgement; the weights are a first guess. Carnage scoring 55 on trip 2 (8 crashes) is a lot next to 10 for a heap; watch whether it rewards driving badly.
- Haul only counts Scrap/Iron/Copper gained during the trip; dirt and stone are ignored on purpose.
- Pickups taken in the seconds while the card is still showing on the way out do not count for the next trip (the trip has not started yet). Press Enter to close the card first.
- The card is only in the world, not in the Hill Climb scene.

**Next**

1. Trip goal at the gate: one bonus objective per trip ("smash 8 heaps", "reach The Wastes", "2 s air") worth double on the report (small) — gives each trip a purpose.
2. Snow/ice part (tracks or spiked tyres) from Frozen North iron (medium) — a new part that changes how the car drives; overdue on the roadmap.
3. Smashable props per biome (cacti, snowmen, fences) feeding the streak (small).
4. Landing grades and a perfect-landing speed kick (small).
5. Tow home when out of fuel far away (small).

## 2026-11-12, about 03:15–04:15 UTC — Polish: dashboard, speed lines, smash streaks (PR #11)

**Built** (a polish-and-feel session)

- **Dashboard** (`src/Game/Dashboard.cs`), bottom-left while driving, drawn in code: speedometer dial (0–120 km/h, red from 100) with a needle that chases the speed and wobbles a hair when fast (orange while boosting), a big km/h number, a segmented fuel gauge (green, amber under 50%, blinking red "LOW FUEL" under 20%) and a damage meter (40 = badly bent). The top-right status line now only shows "BOOST!" and the biome.
- **Speed lines** (`src/Game/SpeedLines.cs`): streaks across the screen, kept away from the car's height, from 35 km/h of sideways speed and full at 70 km/h, warm-tinted while boosting. Falling straight down makes none.
- **Wind** (`wind.wav`, new in `tools/make_sounds.py` with its own random stream; every older sound byte-identical): a hissy, gusting loop from 30 km/h, loud and higher-pitched near 75 km/h.
- **Smash streaks**: heaps smashed within 2.5 s of each other chain into "SMASH x2", "SMASH x3"…; the crunch rises in pitch, the spray and the shake grow, and from x3 each heap pays +1 combo Scrap. A newer smash call-out replaces the previous one. A smash on the ground now costs the car 10% of its speed (`SoftVehicle.Slow`), so heaps have weight; in the air it does not (slowing a flying car ruined its jump in the first run).
- **Bigger scrap heaps** (`tools/make_art.py`): the heap fills most of its 32 px cell (a door panel and a bent rod stick up), about 28 px tall instead of 18; hit box 30×24. Only `pickups.png` changed.
- **Self-righting fix**: the first CI run showed the starter car stuck on its roof for 20 s in a dip on the way to the Frozen North (pitch 172, speed 0), even though self-righting was on. The roll push now grows the longer it is stuck (2.5 → 8) and every 1.5 s the car gets a small hop. A pre-existing bug, made visible because heap slowdowns changed the bot's line.
- **Stunts**: time spent lying on the roof no longer counts as air (it gave "MONSTER AIR 2.6 s, took off at 1 km/h" after a roll-over).
- **CI**: `[speed]` lines (first time the streaks pass 0.6) and `speed_<run>_<frame>.png` screenshots; `[stunt] upside down / back on its wheels` lines; `bestStreak`, `dash`, `lines`, `peakLines`, `wind`, `rollOvers` in every `[autoplay]` line.

**Why this** — Brainstorm in `docs/IDEAS.md`: in the screenshots, speed was a small grey number in a line of text; 90 km/h with the booster looked like 20 km/h. Heaps were tiny next to the fuel can and smashing one did not touch the car. After three feature sessions, a feel pass on driving (what a player does most in the first minute) was due, and the last worklog's "Next" put exactly this polish first, plus the smash streak.

**Verified (CI runs 43148428946, 43149476107, 43150236362 and the docs run on the PR, all green)**

- Run 2 numbers: starter car smashed 10 heaps in 40 s, best streak x5, three combo payouts; rocket car best streak x4; left run x2. Ground smashes cost 10% (e.g. 32 → 28 km/h); air smashes none (28 → 28).
- Speed lines reach 0.88 (left run, 70 km/h) and 0.79 (rocket) in run 2, 0.82 and 0.76 in run 3 after switching to sideways speed (the 15 m crash-test drop no longer triggers them); `[speed] streaks at 0.73 at 68 km/h, wind 0.83, boosting=True`. Wind level follows speed (0.14 at 36 km/h, 0.8+ at 66–70 km/h).
- Self-righting: the left run now rolls back twice ("back on its wheels at x=1162") and reaches the Frozen North (tile x=583 at 40 s, 7 heaps, 3 cans); the rocket car rolls back once.
- Screenshots on the `screenshots` branch: `pickup_world_178.png` (dashboard at 29 km/h, "SMASH! +3 Scrap"), `rocket_230.png` (orange needle while boosting, the new bigger heap on the road), `speed_rocket_112.png` and `speed_left_115.png` (streaks at 54 and 67 km/h; faint in run 2, brighter and clearly visible in run 3). In run 3 the roll-overs no longer produce a false "MONSTER AIR".
- Unchanged: Hill Climb 18.97 s (starter) and 15.25 s (rocket); drill car 8.8 m deep with Scrap 22 / Iron 3 / Copper 1. The starter world drive now ends at tile x≈2232 instead of 2240 (heaps slow it down; expected).

**Not verified / known issues**

- Not listened to: the wind loop and the rising crunch pitch are tuned by reading the numbers only.
- Speed lines are judged from 10 fps still frames; in motion they may look too busy or too faint. Please play.
- The dashboard and speed lines are only in the world, not in the Hill Climb / Free Drive test scenes.
- Damage meter scale (40 = full) is a guess; damage still rarely happens.
- Combo Scrap makes the economy a bit richer again (+3–4 Scrap per 40 s drive).

**Next**

1. Trip summary card at the garage (distance, top speed, best air, heaps, best streak, stashes, crashes) with a best trip to beat (small) — the strongest "one more trip" hook left.
2. Smashable props per biome (cacti, snowmen, fences) feeding the streak (small).
3. Landing grades and a perfect-landing speed kick (small).
4. Biome welcome banner the first time you arrive (small).
5. Snow/ice part (tracks or spiked tyres) from Frozen North iron (medium).

## 2026-11-12, about 02:30–03:00 UTC — Roadside pickups (PR #10)

**Built**

- **Fuel cans and scrap heaps on the surface** (`src/World/RoadsidePickups.cs`): one every 10–19 m outwards from the garage on both sides, starting about 17 m out (current world: 16 cans, 26 heaps). Fuel can: drive through it for +18 fuel, glug-and-ding sound (`fuel.wav`), gold spray, "+N Fuel"; it is left lying if fewer than 6 fuel fit in the tank. On foot, the first can you touch explains "drive through it". Scrap heap: hit it faster than 15 km/h for a **SMASH** (junk sprays forward, high-pitched crunch, small screen kick, "SMASH! +3 Scrap"); slower, or walking into it, just picks it up. Loot is 1–3 Scrap, plus a chance of one Iron (meadow 20%, Frozen North 35%) or Copper (The Wastes 35%), fixed per spot. Fuel cans pulse with a soft halo and twinkle so they can be spotted at speed.
- **Restocking:** taken pickups come back after 150 s, only when you are more than about 22 m away. The list lives for the game session, not in the save, so a garage visit does not restock them (world save format unchanged, no version bump needed).
- **LOW FUEL** warning: under 20% the fuel readout says LOW FUEL and blinks red.
- **Art:** `pickups.png` (fuel can, three heap variants: meadow grey, rusty for The Wastes, snow-capped for the Frozen North) from `tools/make_art.py`; all other PNGs byte-identical. **Sound:** `fuel.wav` from `tools/make_sounds.py`; old sounds byte-identical.
- **CI:** `[pickup]` lines in the smoke logs and pickup counts in every `[autoplay]` line; the screenshot step copies frames around the first three pickups of the world, left and rocket runs (`pickup_*.png`).

**Why this** — Brainstorm in `docs/IDEAS.md`: the screenshots of the drive show a pretty landscape and nothing to do but hold D. "The most boring minute is the drive between biomes" was written in two brainstorms in a row and never addressed, while digging and crashing both got their moments. Fuel never mattered either (75 → 58, no way to earn it back, no warning). Hill Climb Racing keeps every second of driving busy with cans and coins; that is the cheapest big fix and a player feels it within the first minute of driving. The last session was a feature (stashes); this is a feature that is mostly feel, and the next should be a polish session.

**Verified (CI runs 43142244906 and 43142868331, both green)**

- Starter car, 30 s to The Wastes: took 3 fuel cans (e.g. 60 → 75) and smashed 11 heaps at 17–51 km/h: Scrap 0 → 27 and Copper 0 → 3. Frozen North run: 4 cans, 7 heaps smashed, Scrap 15, Iron 3. Rocket car: 4 cans (one took it from 50 to 68 in the first run), 6 smashed, 1 picked up at 10 km/h; it ends with 61 fuel instead of 4 before. Drill car tunnels underground and takes none (expected).
- The first run (40% cans, +fuel whenever there was any room) showed cans topping up 2–3 fuel at a time, so fuel never dropped: now 30% cans and a can waits until 6 fuel fit.
- Screenshots on the `screenshots` branch: `pickup_world_*.png` show the fuel can with its halo lying on a hill, the "SMASH! +4 Scrap" call-out with flying junk, and the "+3 Fuel" gold spray; `pickup_left_121.png` shows a heap on the meadow surface.
- Nothing else changed: starter drive still ends at tile x=2240.4 at 40 s, drill car 8.8 m deep with Scrap 22 / Iron 3 / Copper 1, Hill Climb 18.97 s and 15.25 s.

**Not verified / known issues**

- Not listened to (no audio in CI); the fuel glug and the pitched-up crunch are tuned by reading the code only.
- Scrap heaps are small on screen at driving zoom (about 18 px tall); cans stand out, heaps less. Consider bigger heap art in the polish session.
- Economy: one 30 s drive to The Wastes now pays about 27 Scrap and 3 Copper, enough for a Rocket Booster with a bit of iron. That makes early progress fast; good for the prototype, but tune `HeapLoot` after you play.
- Smashing a heap does not slow the car (it is not a physics object). It may feel weightless; a small speed loss could be added.
- Restarting the game restocks every pickup at once.
- `frame=` in `[pickup]` lines is 0 in headless runs (only the screenshot runs count frames), same as `[crash]`.

**Next**

1. Polish session: speed lines and wind above 60 km/h, bigger and more readable scrap heaps, a little speed loss and wobble when smashing a heap, check the HUD at 1280×720.
2. Trip summary card at the garage (distance, best air, heaps smashed, cans, stashes, crashes) with a best trip to beat (small).
3. Smash streak: heaps within a few seconds chain into "SMASH x3" (small).
4. Landing grades ("CLEAN LANDING" / "BELLY FLOP") (small).
5. Snow/ice part (tracks or spiked tyres) from Frozen North iron.

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
