# Work log

Newest entry first. Each session: what was built and why, how it was verified, what is not verified, and what comes next.

## 2026-11-12, about 16:20–17:35 UTC — Air cam (PR #25, feel session)

**Started by** checking for unfinished work: no open pull requests; main's build after PR #24 (run 43257455503) was running and finished green. The branches `polish/dashboard-speed-smash` and `polish/engine-gearbox` belong to old merged PRs (left alone).

**Built — air cam** (`src/Game/AirCam.cs`, `WorldScene._Process`, `SoftVehicle.CenterVelocity`, bot log in `WorldScene.AirCam.cs`)

- **Look-ahead on the road**: the camera aims 0.22 s of travel ahead of the car (at most 220 px, about 3.4 m), from a smoothed velocity so bumps don't wobble it. The camera now eases itself (AirCam, rate 7/s, same feel as before on foot) instead of the Camera2D's built-in smoothing, and aims an extra speed / 7 ahead to cancel its own trailing lag, so at speed the car really sits behind centre with the road ahead on screen.
- **Big jumps**: once the car is off the ground, a ballistic arc (gravity only, from the tyres, against tiles and kicker ramps) predicts where it comes down. If the whole flight is predicted at 0.7 s or more, the camera eases toward the midpoint of car and landing and zooms out until both fit (never below 0.85), and a soft dark **shadow** sits on the ground where you will land. On touchdown it eases back in. The car is clamped to stay on screen whatever happens.
- **Bot log / CI**: `[aircam] big air …` (predicted landing, flight time, zoom), `[aircam] touchdown …` (how far the first and last guesses were off, widest zoom, frames with the car off screen), `[aircam] driving …` (look-ahead and where the car sits on screen); `aircam_*` screenshots from the flip, rocket and long-jump runs.

**Why this** — Brainstorm in `docs/IDEAS.md` (seventeenth session). In `flip_mid_47.png` and `kicker_trip_77.png` the camera sits dead on the car: on every jump half the screen is the road behind you and the landing is off the edge, and since flips you spin in the air without seeing where you come down. Air cam was #1 on the last Next list, and the previous session was a feature, so this one is feel and clarity — something a player notices in the first ten seconds of driving.

**How it got there (CI runs 43258815704, 43261486743, 43263783109, all green)**

1. Run 1: framing worked (rocket jumps eased out to 0.87–0.98 zoom with car and landing in view, car never off screen), but the landing guess was 1–2 m long (probed from 26 px under the body centre; the tyres touch first), the shadow was too faint to see, and at 93 km/h the Camera2D's smoothing lag (about 300 px) ate the whole look-ahead: the car sat 51 px *ahead* of centre.
2. Run 2: probe at the tyres (44 px), a darker shadow, a lag correction — the shadow now reads clearly (`aircam_flip_69.png`), but the correction did not match Godot's smoothing, and the Deep test's teleport left the car off screen for 22 frames.
3. Run 3: AirCam eases the camera itself, so the correction is exact, and snaps on a teleport.

**Verified (run 43263783109)**

- **53 big-air touchdowns** across all world runs: the last guess was off by 1.3 m on average, 36 of 53 within 1 m (the misses are bounces off kicker lips and heaps); **0 frames with the car off screen** (run 2: 22, all from the Deep test's teleport). Rocket jumps ease out to 0.87–0.98 zoom; the starter's 0.7–1 s kicker hops ease out only a little (1.45 → 1.30–1.40).
- **Look-ahead**: starter at 39 km/h sits 165 px left of centre driving right (zoom 1.46); 20–26 km/h 100–145 px. The rocket car sits only 20–68 px behind centre at 71–100 km/h on the ground, because it is nearly always just back from a jump while the air framing eases out.
- **Screenshots**: `aircam_rocket_146.png` and `aircam_flip_69.png` (car upper left mid-jump, landing slope and shadow in view on the right), `world_130.png` (starter at 34 km/h left of centre, the hill ahead in view).
- **Gameplay unchanged** (the camera is not read by physics or bots): 40 s positions identical to main — starter 2209.3, left 465.7, spiked 424.9, Crystal Drill 1480.1, rocket 2552.7; Hill Climb 19.18 s / 14.08 s. No errors.

**Not verified / known issues**

- Feel is my judgement: whether the look-ahead is pleasant or seasick, and whether the zoom-out on big jumps feels cinematic or distracting, needs you to play (D1). Headless runs at 120 fps; at 60 fps the easing is the same (it is time-based).
- The prediction ignores the booster and air drag, so with Shift held the shadow creeps forward during the jump (first guesses up to 9 m short on rocket jumps, last guesses within about 2 m).
- The shadow is a flat ellipse even on a slope; it can sit slightly in the hill.
- The drive scene (Hill Climb, `drive.tscn`) still uses the old fixed camera.
- **A crash at exit, once**: run 43266117147 (the docs-only commit, code identical to the green run 3) failed the smoke test because Godot segfaulted *after* the Scrap 4-Cylinder launch-control run had finished its 900 frames ("Leaked unsafe reference" for the world generator's FastNoiseLite/RandomNumberGenerator, then a segmentation fault at shutdown). The game itself ran normally. I re-ran the job and the smoke test passed (attempt 2), so it is a rare shutdown flake, not the air cam. If it comes back, the likely fix is to free the world generator's noise objects on exit (or ignore a crash after `--quit-after`), worth a look next session.

**Next**

1. Start-light gantry (S, IDEAS #82) or the wind-assist kicker (S, #104) so the starter car gets a flip chance.
2. Trick goals (S, #96) / Gyro part (S, #94); the landing target ring (S, #101) now that the shadow lets you aim.
3. Crystal Lamp (S, #64), surface lift (S–M, #68), then the first underground danger (#65 / #56, M).

## 2026-11-12, about 15:20–16:25 UTC — Merged rock it free (PR #23), then flip tricks (PR #24)

**Started by** checking for unfinished work: PR #23 (rock it free) was open with its last run (43244969181) still going; it finished green at 15:24 with a playtest log matching the PR (2-tile trench out in 4.7 s with 12 rocks, Crystal Drill car's wedge free in 1.2 s, 40 s positions unchanged, no errors), so I squash-merged it (d231b47).

**Built — flip tricks** (`src/Game/WorldScene.Flips.cs`, air handling in `SoftVehicle`)

- **Rotation counted in the air**: every physics frame with no wheel down, the change in pitch is added up. Each full turn plays a rising tick and says "FULL TURN! now land it".
- **Landed flips**: when the landing judge rules on a jump that turned at least 300° (a full turn less 60°, for slopes), it is a **BACKFLIP** (the end you drive towards went up and over) or **FRONTFLIP**, DOUBLE / TRIPLE / 4x …, with the `record` fanfare, +8 km/h per turn (up to three) on top of any perfect-landing kick, and the call-out "BACKFLIP!  +8 km/h" over the landing or long-jump line. Flips score 60 points per turn on the trip report's Jumps row ("1.4 s, 1 flip + 2 perfect").
- **Crashed flips don't count**: a roof / nose / tail landing, or wheels first with the body slamming down more than 35° off the slope, adds "BACKFLIP, but it doesn't count" to the crash call-out.
- **Air handling**: with every wheel off the ground, Q/E rotate 1.5x as hard, ramping to 2.3x after holding the same key 0.15–0.45 s, and spin in the direction you hold is not bled by the in-air spin damping until 690°/s. Released spin is damped as before. See `docs/DECISIONS.md`.
- **CI**: `--fliptest` (straight into the car, east, backflip off kicker 1, frontflip off kicker 2, …; no boost during a flip; a 0.1 s trace of every attempt), runs for the rocket car (40 s) and the starter (30 s), `[flip]` lines now printed by the smoke test, `flips=back/front/crashed maxAirRot=` on `[autoplay]`, `flip_mid_*`, `flip_turn_*`, `flip_landed_*` screenshots.

**Why this** — Brainstorm in `docs/IDEAS.md` (sixteenth session). Kickers are everywhere and the car looks great in the air, but Q/E only ever levelled it: there was nothing bold to do mid-jump. Flips are the heart of Hill Climb Racing's and Trials' best moments, they make every kicker a risk-or-play-safe choice ("one more try"), and #48 was #1 on the last Next list. The previous session was feel, so this one is a feature you feel in the first minute.

**How it got there (CI runs 43248750220, 43251266808, 43253236223, 43255068732, all green)**

1. Run 1: no full turns at all; the flip bot took kickers at only 38–40 km/h (I cut the booster on every bump) and `[flip]` lines were not printed by the smoke test.
2. Run 2: kickers at 54–68 km/h and 1.4–1.65 s of air, but still no flips. The screenshots showed the car rotating, so I added a 0.1 s trace.
3. Run 3 (trace): spin topped out at 350–400°/s however long Q/E was held — the in-air spin damping (`AirSpinDamping`, 2.9/s) caps it — and two near-flips (−316°, 334°) were lost because a wheel touching and bouncing off re-set the stored rotation before the landing was judged.
4. Run 4: held spin no longer damped (up to 690°/s), a bounce adds to the jump's rotation.

**Verified (run 43255068732)**

- **Rocket car, `--fliptest`: 3 flips landed in 40 s** — BACKFLIP −333° in 0.89 s (rough landing), FRONTFLIP 335° in 1.50 s (**perfect landing**, 13 + 8 km/h kick), FRONTFLIP 371° in 1.82 s (rough); one attempt cut short by an early touchdown, one ended on the roof at −196° (no flip, correct). Peak spin 513–556°/s.
- **Starter car**: no flips — its kicker jumps last 0.36–0.65 s; the frontflip attempts ended on the roof. Flips are a big-jump trick (rocket, or a fast run-up); the starter can only half-flip.
- **Screenshots**: `flip_mid_47.png` (upside down mid-backflip), `flip_landed_59.png` ("BACKFLIP! +8 km/h / LONG JUMP 5.2 m · flag planted"), `flip_landed_104.png` ("FRONTFLIP! +24 km/h / LONG JUMP 18.7 m"), `flip_turn_149.png` ("FULL TURN! now land it").
- **Other playtests**: no errors; rock it free identical to main (2-tile trench out in 4.7 s, 12 rocks; controls still stuck); Hill Climb 19.18 s / 14.08 s (main 19.19 / 14.11); sprints unchanged (2.99 s plain). The bots' own anti-flip tilt in the air is stronger now, so 40 s positions moved a little: starter 2209.3 (main 2179.0), left 465.7 (512.7), spiked 424.9 (375.1), Crystal Drill 1480.1, rocket 2552.7; trip reports 387 / 395 (main 414 / 379). Both drill cars now rock themselves free of their wedges (plain drill: free after 1.0 s, 2 rocks).

**Not verified / known issues**

- Fun and feel are my judgement. Whether holding a key to spin and tapping to line up feels natural, and whether ordinary landings got twitchy, needs you to play (D1).
- The flips row on the trip report is not in a screenshot (the trip test bot does not flip). The "FULL TURN!" tick only fires when one stretch of air passes 360°; a flip completed across a bounce counts but gets no tick.
- The starter car cannot flip off the current kickers; a player with a fast run-up might half-flip and land on the roof more often than before (the stronger air rotation makes over-rotating easier).
- Frontflips and backflips are scored the same.

**Next**

1. Air cam (S, IDEAS #97): ease the camera out on long jumps so you see where you land — matters more now that flips spin you round.
2. Trick goals (S, #96) and the trick combo (S, #98) to give flips a reason on the trip report; or the Gyro part (S, #94).
3. Start-light gantry (S, #82), Crystal Lamp (S, #64), surface lift (S–M, #68), then the first underground danger (#65 / #56, M).

## 2026-11-12, about 14:20–15:40 UTC — Rock it free (PR #23, feel session)

**Started by** checking for unfinished work: no open pull requests, main green (run 43234068638 after PR #22). The branch `polish/engine-gearbox` is PR #19's, merged long ago (left alone).

**Built** (`SoftVehicle` "Rock it free" block, `WorldScene.OnStuck/OnRock/OnRockedFree`, `CrashFx.Dust`, `creak.wav`)

- **STUCK**: holding the throttle (no handbrake, not drilling, not on the roof, a wheel down) for 1 s without moving faster than about 1 km/h. A "STUCK! Rock it free: tap Q, E, Q, E" call-out, a low thud, and the hint line swaps the launch-control tip for "STUCK! Tap Q, E, Q, E to rock it free (keep the throttle on)".
- **Rocks**: while stuck, ground tilt is 2.5x stronger, and each fresh Q or E tap with a wheel down gives a hop (110 px/s, +40 per chain step, up to 270), a shove the way the throttle points (3–8 km/h) and a twist the way you tapped. Alternating taps within 0.9 s build a chain up to x5 ("ROCK IT! x3 / keep the throttle on"). A tap in the air waits up to 0.4 s and fires on touchdown. Each rock plays a spring creak (pitch rises with the chain), puffs dust at the wheels and nudges the camera.
- **ROCKED FREE!**: 1.5 m sideways from the stuck point while rolling: call-out with rocks and seconds, the landing ta-DING, warm speed streaks. Letting go of everything for 1.5 s (or flipping, parking) ends it quietly.
- **Bot**: when stuck it taps E, Q, E, Q every 0.3 s with the throttle held for up to 4 s, then falls back to backing up (`AutoRock`, off with `--norock`).
- **CI**: `--rocktest` digs a trench past the pad (`--rockdepth=N` tiles, default 3, 20 tiles long) and drives at it; runs at 2 tiles, 2 tiles without rocking (control), 3 tiles, 3 tiles without rocking; `[rock]` lines; `rock_*` screenshots; `stuck= rocks= rockedFree= longestStuck=` on `[autoplay]`.

**Why this** — Brainstorm in `docs/IDEAS.md` (fifteenth session). `drill_280.png` showed the drill car wedged in a cave notch at 0 km/h with the hint line cheerfully offering a PERFECT START. Being stuck was the most boring minute of the game: nothing to do but get out and walk. It was #1 on the last Next list, four sessions in a row were features or a physics fix, so a feel session was due, and rocking a stuck car is a little struggle you win (Spintires, MudRunner).

**Verified (CI runs 43237750998, 43240115433, 43242512714, all green)**

- **Run 1** (taps stacked in the air): 3-tile trench out in 2.3 s with 8 rocks — but the rocket car, stuck in a dip at the end of its run, rocked 13 times and **flew 13 m up**. Taps in the air must not stack.
- **Run 2** (rocks only with a wheel down): no more flying; but taps made in the air were lost, so the chain kept breaking (3-tile trench: never out forwards).
- **Run 3** (air taps wait for touchdown): **2-tile trench: out 4.7 s after getting stuck, 12 rocks** (`rock_free_97.png`: "ROCKED FREE! 12 rocks · 4.7 s"); **control without rocking: still in the trench after 20 s** (it backs up and charges the wall three times). 3-tile wall (0.75 m): out of reach with or without rocking. The Crystal Drill car's wedge on the bot route: **free after 1.2 s, 3 rocks**, dug to 9.6 m by 40 s. The plain drill car's V-notch: 15 rocks, not free (run 2 freed it with 10 rocks); damage 62 vs 48 before (the hops knock the drill about). Rocket car's dip at 36 s: 8 rocks, not free, stays on the ground.
- **Screenshots**: `rock_stuck_51.png` (hint line and call-out), `rock_chain_57.png` (ROCK IT! x3, dust at the wheels), `rock_free_97.png`.
- **Nothing else changed**: 40 s positions identical where no car got stuck (starter 2179.0, left 512.7, spiked 375.1), trip reports 414/379, Hill Climb 19.19 s / 14.11 s. No errors.

**Not verified / known issues**

- Fun is my judgement; the bot taps at a fixed 0.3 s, a player will find their own rhythm. Whether the bounce looks floaty at x5, and whether it can be used to climb things it should not, needs you to play (D1).
- The creak is tuned by numbers and a spectrogram only (resonances at 640/1130/1720 Hz).
- The plain drill car's notch on the bot route is still a trap in some runs (rocking freed it once in three).
- Rocking works with the throttle either way, but only forwards-into-a-wall is tested.

**Next**

1. Flip tricks (S, IDEAS #48) now that tilt is full strength — BACKFLIP! when you land it.
2. Start-light gantry over the pad (S, #82), or the Hydraulics "Bounce Kit" part (S–M, #89): the rock pulse as a part you own.
3. Crystal Lamp (S, #64), surface lift (S–M, #68), then the first underground danger (#65 / #56, M).

## 2026-11-12, about 13:20–14:15 UTC — Finished the physics fix (PR #21), then the gate sprint (PR #22)

**Started by** checking for unfinished work: PR #21 (car nodes relative to a local origin) was open, its build green (run 43225624030), and the run before me was still finishing it (it pushed its work-log entry at 13:31 and left the PR open over one question: why the drill cars now get stuck in caves). I picked it up: one bot change and one diagnostic line, two runs (43228327979, 43229946586, green), and the answer — the wedges are a V-shaped notch and a one-tile step, both wheels grounded with spin 0.0–0.1 at full throttle and no body contact, the same tyre physics as before on a different route. The fix also turned out to have restored the sideways half of Q/E tilt near the garage (one substep of tilt moves a node about 0.0005 px; a float at x ≈ 22,000 steps 0.002 px). Details are in PR #21's entry below. Squash-merged as a7129f3. (Its squash message carried a "[skip ci]" from a docs commit, so main did not build on its own; PR #22's run builds main plus the sprint.)

**Built — gate sprint** (`src/Game/GateSprint.cs`, `WorldScene.UpdateSprint/FinishSprint`): every standing start on the garage pad is timed over 20 m, drag-strip style. The clock starts the last moment the car stood still on the pad (so the launch charge does not count) and stops 20 m sideways from there, either direction; stopping on the pad again or 12 s without finishing cancels it. While it runs the status line ticks "SPRINT 1.84 s"; at 20 m a call-out says "20 m SPRINT 2.32 s · a time to beat" / "NEW BEST! 0.26 s faster" / "best 2.06 s (+0.12)", with the `record` fanfare and warm streaks on a new best. If the trip starts during the sprint, the trip goal joins the same call-out ("…\nTRIP GOAL: Go 50 m out") instead of overwriting it. Best time and count are saved in progress.json (`SprintBest`, `Sprints`) and the best shows on the status line while you stand on the pad. CI: `[sprint]` lines, `sprints=`/`sprintBest=` on `[autoplay]`, `--launchtest` now reverses back onto the pad after the first sprint and launches again (run 2700 frames), `sprint_*` screenshots.

**Why this** — Brainstorm in `docs/IDEAS.md` (fourteenth session). Launch control made the first second of a trip a skill, but nothing remembered it: the "+18 km/h" fades and there is no reason to try again. Need for Speed's drag races and Hill Climb Racing hang their hook on a number to beat; this gives one in three seconds without leaving the garage. It was #2 on the last Next list (#1 was the physics fix, now merged).

**Verified (CI run 43231910550, green)**

- **Launch control is worth tenths**: starter V6, perfect start **2.32 s**, bogged start 2.99 s, plain start 2.99–3.00 s; the second perfect launch (after reversing onto the pad) **2.06 s → newBest=True, best_before=2.32**. The trip test's second departure: 2.80 s, NEW BEST over 3.00.
- Other cars, plain start: rocket 1.76 s (booster), V8 2.77 s, Scrap 4 3.85 s (2.66 s with a perfect launch), drill 6.38 s, Crystal Drill 4.62 s; on the ice strip 3.21 s plain tyres, 3.05 s spiked.
- **Screenshot** `sprint_43.png`: "20 m SPRINT 2.32 s · a time to beat" with "TRIP GOAL: Go 50 m out" under it, in one call-out.
- **Nothing else changed**: 40 s positions identical to PR #21's run (starter 2179.0, drill 1414.5, Crystal Drill 1474.0, left 512.7, spiked 375.1, rocket 2533.8), trip reports 414/379, Hill Climb 19.19 s / 14.11 s. No errors.

**Not verified / known issues**

- Fun is my judgement; please race yourself off the pad and say whether 20 m is the right length (D1).
- The ticking "SPRINT 1.84 s" and the "sprint best" text on the status line are not in any kept screenshot (the kept frames are before the start and after the finish, and every CI run starts with a fresh save).
- A car dropped onto the pad (the crash test) or one that rolls to a stop on the pad on its way home also starts a sprint when it drives off. That seems fine (any standing start is a sprint), but it means a trip home that ends with a stop on the pad and a drive-off counts.
- The drill cars still wedge in a notch and against a one-tile step on the bot's route (see PR #21): a car in that spot cannot drive or reverse out.

**Next**

1. Rock it free (S, IDEAS #84): when the throttle is held and the car does not move, let Q/E rock it harder and say so — the wedge the bot keeps finding.
2. Start-light gantry over the pad (S, #82) — the sprint's ritual; or flip tricks (S, #48) now that tilt works fully.
3. Crystal Lamp (S, #64), surface lift (S–M, #68), then the first underground danger (#65 / #56, M).

## 2026-11-12, about 14:00–14:45 UTC — Car physics relative to a local origin (PR #21, left open)

**Built** — Each car keeps its node positions relative to its own origin (`SoftVehicle.Origin`, a multiple of 1,024 px along x, moved by `Recenter()` when the car drives more than 1.5 steps away; the shift is exact). The world only sees world coordinates: terrain, grip and mining go through `ShiftedTerrain` (adds the origin to every point asked about), and `CenterPosition`, `Bounds()`, `ContainsPoint()`, `DrillTips`, `WheelContacts` and crash points add it back; car-to-car and car-to-loose-object contacts in `SoftPhysicsWorld` convert between frames. The car node itself sits at its origin, so drawing uses the node positions as they are (a private `ToLocal` that returns its argument).

**Why** — PR #20 found the cause of the Next list's #1 (the Scrap 4 pickup cannot leave the pad): 32-bit positions at x ≈ 20,600 px lose every velocity change under about 1.9 px/s per substep (see `docs/DECISIONS.md`). That is a trap in the garage and quietly bends every car's handling in the world.

**Verified (CI run 43225624030, green)**

- **Scrap 4 pickup drives**: 2 s after getting in 6.5 m at 23 km/h (before: 0.2 m, 0 km/h). With a perfect launch 14.3 m at 29 km/h (before 10.7 m).
- **V8 pickup starts properly**: 12.3 m at 33 km/h after 2 s (before: stuck at 6 km/h, 3.3 m).
- **Parked cars stay put**: 1288.0 → 1287.9 tiles over 10 s (before: 4.5 tiles of creep, left or right depending on the engine).
- **Changes elsewhere** (expected, physics is now what the formulas say): the starter V6 starts slower (10.8 m / 33 km/h after 2 s, before 14.4 m / 39 km/h — the rounding had been helping it) but ends the 40 s drive at about the same place (2179.0 vs 2189.5); rocket car further (2533.8 vs 2155.9); left run 512.7 vs 603.2 (further west), spiked 375.1 vs 300.1; trip reports 414 and 379 (before 259 and 443; the second trip did not beat the first); Hill Climb 19.19 s (was 18.97 s) and rocket 14.11 s (was 15.25 s). Launch control still grades and shoves (perfect 16.8 m vs plain 10.8 m after 2 s). No errors.

**Not verified / known issues — why it is left open**

- **The drill car got wedged**: tunnelling into the hill east of the garage it nosed into a cave, snapped a front suspension beam while standing still (dent 41, damage 48, `drill_280.png`) and sat at 2.5 m deep for the last 14 s (before: 8.8 m, no damage). The Crystal Drill car also dug less deep (6.6 m vs 10.9 m, damage 5). It may be the bot arriving at the cave at a different moment (the start is slower now, and the dig phase is timed), or a real change in how drilling behaves. One run cannot tell, and I had used this session's runs, so I did not merge.
- Handling feel changed everywhere (slower standing starts with the V6). That may need a small torque retune once you have driven it.

**Picked up and merged by the next session (2026-11-12, about 13:25–13:55 UTC)** — The drill question is answered: the wedges are places, not a drilling fault. A stall report on every stuck/wedged bot (`[autoplay] stuck` / `wedged while digging`, CI run 43229946586, green) shows no body node touching the ground and both wheels grounded with spin 0.0–0.1 at throttle 0.6–1.0: the plain drill car sits in a V-shaped notch (wheel normals (0.45,−0.89) and (−0.45,−0.89)) and the Crystal Drill car has its rear wheel against a one-tile step (normal (−1.00,−0.02)). The tyres never break loose, so neither forwards nor reverse moves the car — the same physics as before, the route is just different now that the start and the tilt behave as written. The tilt really did change: at x ≈ 22,000 px a float steps 0.002 px and one substep of Q/E tilt moves a node about 0.0005 px, so the sideways part of air control was rounded away near the garage until this fix. Drilling itself is as fast as before (Crystal Drill car 1353 → 1472 tiles in 12 s). The bot now eases off the dig tilt before 30° and tries to back out when wedged (it cannot, as above). Getting stuck in a notch is a real player problem too, so IDEAS #84 ("rock it free") is next in line. Every other number in this run is identical to the run above.

**Next (for the session that picks this up)**

1. Check the drill question: run the drill car two or three times with different start delays (or compare `--design=driller` in Free Drive near x = 0, where nothing changed), and look at `drill_*` screenshots. If drilling is fine, merge PR #21.
2. If standing starts feel sluggish, raise the V6 torque a little rather than bring back the rounding.
3. Then: gate sprint (IDEAS #76), garage "too weak" warning (#77).

## 2026-11-12, about 12:40–14:00 UTC — Launch control, and why the Scrap 4 pickup will not move (PR #20)

**Started by** checking for unfinished work: PR #19 (engine sounds and gearbox) was open with a green build (run 43215472593) and a playtest log matching its description (40 s positions unchanged, starter 4 up / 0 down shifts), so I squash-merged it.

**Built**

- **Launch control** (`SoftVehicle.UpdateLaunch`, constants at the top of `SoftVehicle`): at a standstill, hold Space + throttle and a launch charge climbs from idle to the top in 1.1 s. Let go of Space with the throttle still down: 0.75–0.97 is a **PERFECT START** (+18 km/h shove along the body, an exhaust pop, ta-DING, warm speed streaks), 0.45–0.75 a **GOOD START** (+8 km/h), lower **BOGGED DOWN**. Past the top the revs bounce off the limiter back to 0.6 and climb again, so you can wait for the next window. The shove works from a standstill (the old perfect-landing kick needed the car already rolling) and spins the tyres up with it.
- **Dashboard**: while armed the rev bar shows the charge, the top segments are green with a bracket beside them, and the gear box reads "L", then a green "GO" while you are in the window. The engine revs follow the charge (pitch climbs, limiter stutter on the bounce).
- **Hint line**: "Hold Space + D, let go in the green: PERFECT START" while the car is stopped.
- **CI**: `[launch]` lines (grade, charge, bounces, kick; speed and distance 0.5/1/2 s after every start, plain starts too), `starts=perfect/good/bogged` on `[autoplay]`, runs `--launchtest` (gets straight in, rides the limiter once, lets go at 0.86) for the starter and the Scrap 4 pickup, `--launchearly` (lets go at 0.30), a screenshot run with `launch_*` frames. `[stall]` lines (per-wheel spin, slip, hub speed, ground normal, body nodes touching the ground) whenever a car does not move off after getting in, and two Free Drive runs (Scrap 4, V8) on the flat test track.

**Why this** — Brainstorm in `docs/IDEAS.md` (thirteenth session). The first second of every trip is the dullest: press D, the pickup creeps off the pad. Need for Speed and Mario Kart turn that second into a skill moment, and PR #19's revs and limiter were exactly what it needed (#70 was the top pick). The Next list's #1 (Scrap 4 cannot move the pickup) is about the same second, so I put diagnostics for it in the same runs.

**Verified (CI runs 43218896309 and 43221442547, both green)**

- **Launch control works**: `[launch] Perfect start: charge 0.87 bounces=1 kick 18 km/h revs 0.90`. Starter V6 on the pad, 2 s after letting go: **perfect 19.0 m at 42 km/h**, bogged (`charge 0.30`) 14.1 m at 35 km/h, plain start 14.4 m at 39 km/h; 0.5 s after: 32 km/h vs 14–16. The bounce happened once before the release (bounces=1), as planned.
- **Screenshots**: `launch_18.png` (dashboard: green "GO" box, bracket beside the rev bar, bar filled into the green, hint line at the top), `launch_21.png` ("PERFECT START! +18 km/h", exhaust puff behind the car, 23 km/h in 2nd).
- **Nothing else changed**: 40 s positions identical to main in both runs (starter 2189.5, drill 1473.5/97.2, Crystal Drill 1475.0/105.5, left 603.2, spiked 300.1, rocket 2155.9), trip reports 259 and 443, Hill Climb 18.97 s / 15.25 s. No errors.
- **Scrap 4 stall — cause found, not fixed**: on the pad the Scrap 4 pickup sits at −8 px/s with full throttle, both wheels grounded, spin −0.3, slip 0, no body node touching the ground. In Free Drive (flat track near x = 0) the same car reaches 20 km/h in 2 s. A Python port of the solver (`/tmp` only, not committed) reproduces it exactly with 32-bit positions at x = 20,500 (Scrap 4 −8.9 px/s, V8 stuck at 82 px/s) and not at x = 500. The garage is at x ≈ 20,600 px, where a float steps 0.002 px, and the Verlet velocity (position minus previous position) loses any change under about 1.9 px/s per substep. Details in `docs/DECISIONS.md`. With a perfect launch the Scrap 4 pickup does get going (10.7 m in 2 s, 23 km/h).

**Not verified / known issues**

- Fun is my judgement. Whether the window (about 0.24 s wide) is too generous, and whether +18 km/h is too strong, needs you to play (D1).
- Launch control is only exercised by the bot from the garage pad, facing right. Reversing launches (hold A) use the same code but are not tested.
- The float-precision problem affects every car everywhere in the world (coarser further east). The fix (car nodes relative to a local origin) is next.

**Next**

1. Fix the float precision: car nodes relative to a local origin (M–S); then re-check every playtest number, since handling may change.
2. Gate sprint: a timed dash from the pad to the gate with a best time (S, IDEAS #76) — gives launch control a number to beat.
3. Garage "too weak" warning (S, #77), Crystal Lamp (S, #64), surface lift (S–M, #68).

## 2026-11-12, about 11:25–12:35 UTC — Engine feel: a sound per engine, gear shifts, exhaust pops (PR #19, polish session)

**Started by** checking for unfinished work: no open pull requests; the last main build (after PR #18) was running and later green. The branch `polish/dashboard-speed-smash` still exists but was merged long ago as PR #11 (left alone).

**Built**

- **A sound per engine** (`tools/make_sounds.py`, own random stream; every older sound byte-identical, checked by md5): `engine_e4.wav` for the Scrap 4-Cylinder (64 Hz buzzy, square-ish pulses with uneven firings and a tappet rattle), the old `engine.wav` stays the V6, `engine_v8.wav` for the V8 (40 Hz uneven "cross-plane" lope, deep rumble). Each firing pulse rings under the next two, so the loops have no click (loop seam jump 0.010 / 0.007, centroid 1.5 kHz buzzy vs 0.4 kHz deep). Engine parts carry `gears` and `sound` in `data/parts/parts.json` (4 / 5 / 6 gears).
- **A pretend gearbox** (`src/Vehicles/Gearbox.cs`): sound and dashboard only, it never touches the physics. Revs follow the road speed in the current gear (plus a little for real wheelspin), climb, drop at a shift (0.16 s dip) and climb again. Gears are picked from the smoothed road speed with at least 0.6 s between shifts; gear k tops out at 0.85·(k/n)^0.8 of the engine's top speed (V6: 16, 27, 38, 48, 57 km/h). In the air the gear holds and the revs climb towards the limiter with the throttle open (and sag off it). The limiter stutter (pitch and volume chop every 70 ms) only happens in the air or flat out in top gear.
- **Exhaust pops**: a flame-and-smoke puff out of the rear block on every upshift at more than half throttle and when lifting off above 70% revs (at most every 0.6 s), with a new `pop.wav`; a `shift.wav` clunk on every shift (quieter on downshifts).
- **Starter**: getting in plays `start.wav` (starter cranking, the engine catches, revs once, settles) pitched per engine, and the engine loop fades in after the catch.
- **Dashboard**: a gear number (flashes on a change, red on the limiter) over a 10-segment rev bar (green, amber, red; the red tip blinks on the limiter). The panel grew from 300 to 336 px.
- **CI**: `[engine]` lines (start with engine, gears and loop; the first 14 shifts with speed, revs and pitch; the first 6 pops), `gear= revs= enginePitch= shifts= topGear= limiter= pops=` on `[autoplay]`, two short runs with the starter's engine swapped (`--design=v8`, `--design=scrap4`), and `shift_world_*` / `pop_world_*` screenshots.

**Why this** — Brainstorm in `docs/IDEAS.md` (twelfth session): three feature sessions in a row, so a feel session was due. The game looks busy now but sounded like one sound: every car, from a scrap four to a V8, played one 55 Hz loop whose pitch slid up with speed. Driving is what a player does most in the first minute, and in Need for Speed and Hill Climb Racing the climb and drop of a gear change is what makes speed feel like effort. Buying a V8 should be heard. Ideas #34/#54 were the top polish items.

**Verified (CI runs 43209812792, 43211773012, 43213219136 and the final run on the PR, all green)**

- **No gameplay change**: 40 s positions identical to main in all three runs (starter 2189.5, drill 1473.5/97.2, Crystal Drill 1475.0/105.5, left 603.2, spiked 300.1, rocket 2155.9), trip reports 259 and 443, Hill Climb 18.97 s / 15.25 s. No errors.
- **Run 1 (gears from wheel spin)**: the starter hunted between 4th and 5th all the way: 17 up / 14 down in 40 s. The wheels in this physics spin near their top almost all the time (the tyres slip; the car cruises at about half that), and every bump changed it.
- **Run 2 (gears from smoothed road speed, revs from wheel spin)**: no hunting (2/0) but stuck in 3rd with the revs on the limiter: limiter in a third of all driving log lines.
- **Run 3 (revs and gears from road speed, shorter ratios)**: the starter goes 1→5 at 21/40/42/53 km/h and then holds: **4 up / 0 down in 40 s**; rocket car 7/4 (downshifts after jumps and slow climbs); drill cars change down to 1st while tunnelling (revs 0.26 at 3 km/h). V8 swap: 6 gears, `engine_v8` loop, 2 upshifts in its 10 s drive. Scrap 4 swap: 4 gears, `engine_e4` loop. Pops: 3–8 per 40 s drive. Limiter still in 62 of 186 driving log lines (mostly a moment before each upshift and in the air), so the final change keeps the stutter to the air and top gear only.
- **Screenshots** (`screenshots` branch): `shift_world_108.png` (gear "2" with the rev bar dropped into the amber after the shift, orange exhaust puff behind the car), `pop_world_114.png` (gear "3" in red on the limiter just before the shift, the puff), `shift_world_128.png`.

**Not verified / known issues**

- **I cannot hear anything.** The sounds are checked by numbers (spectrum peaks at 64 / 55 / 40 Hz, loop seams, no clipping) and waveform plots only. Whether the V8 sounds like a V8 or a motorboat, and whether the shift clunk is too loud, needs your ears (D1).
- **The Scrap 4-Cylinder cannot move the starter pickup**: in the `--design=scrap4` run the car sits at 0 km/h for 10 s with full throttle (wheels barely turn). Not caused by this change (no physics touched), but it means a player who swaps in the cheaper engine gets a car that does not drive. Next session should look at it (torque 26000 vs the V6's 48000).
- The exhaust puff is small at 10 fps screenshots and drifts back quickly; it may be too subtle in play.
- The Hill Climb / Free Drive test scenes still have no engine sound at all (they never had one).
- Speeds printed on shift lines are the body's total speed (vertical included), so some look odd (an upshift "at 14 km/h" right after a landing).

**Next**

1. Fix the Scrap 4-Cylinder so a car built with it drives (S) — a trap in the garage right now.
2. Launch control at a standstill: handbrake + throttle, release for a PERFECT START (S, IDEAS #70) — uses the new revs.
3. Crystal Lamp part (S, #64) and the surface lift (S–M, #68) for the Deep.
4. Engine cough and smoke at high damage (S, #28) — cheap now that each engine has its own sound.
5. Perfect chain "PERFECT x3" (S, #46) and the kicker-record trip goal (S, #58).

## 2026-11-12, about 10:20–11:15 UTC — The Deep: glow crystal caverns, Crystal Drill, depth record (PR #18)

**Started by** checking for unfinished work: no open pull requests, last main build green.

**Built**

- **The Deep** (`TileWorld.Generate`, `GrowCrystals`): about 12 m under the natural ground (48 rows, ±6 by noise; 12.5 m under the garage) stone turns into dark blue **Slate** (harder: 36 vs 28) and caves open wider. Slate walls of the caverns grow clusters of **glow crystals**; rare crystal seams sit inside the rock. World save **GHWA** (the first slate row per column is saved; old worlds regenerate).
- **Crystal glow** (`src/World/CrystalGlow.cs`): crystals are redrawn above the underground darkness at full brightness with a breathing additive cyan halo and the odd sparkle; buried crystals glow at a third so a seam shows through the rock. Ore inside slate is tinted cool and dark so its grey background no longer glares.
- **Crystal** material: new HUD slot and icon, "+1 Crystal (n)" call-out (at 4: "enough for a Crystal Drill (garage)"), cyan dust puff, counts on the trip report's haul like Copper.
- **Crystal Drill** (`data/parts/parts.json`, `drill_crystal`): 3 Iron + 4 Crystal, drill power 150 (plain drill 60), dark housing with a crystal ring and a cyan cone (`tools/make_art.py`, parts cell 12; `SoftVehicle` draws the cone cyan for power over 100).
- **Depth record** (`GameSession.DeepestDig`, saved in progress.json): "NEW DEPTH RECORD / 9 m down" at 5 m and every 2.5 m past the last call-out; the status line shows "Depth 9 m (best 14 m)" on foot *and* in the car, plus "THE DEEP" while in slate; a "THE DEEP" title card with a low fanfare the first time you get there in a session.
- **Art** (`tools/make_art.py`): tiles.png grows to 10 rows: slate (4), crystals on slate (4), slate back wall (2), clear crystal overlays (4), crystal icon. All older cells are byte-identical (checked).
- **CI**: `[deep]` lines (world counts, entering the Deep, depth records, each crystal mined, glow counts), a `--deeptest` run (puts the player on the floor of the nearest real crystal cavern with room to stand and digs out every crystal in reach) headless and as screenshots, `--design=crystal` (driller with the Crystal Drill) headless and as screenshots, `deep_*`, `deep_crystal_*` and `crystaldrill_*` screenshots, and `deepest=`/`crystalsMined=` on `[autoplay]` lines.

**Why this** — Brainstorm in `docs/IDEAS.md` (eleventh session): six sessions in a row made the surface drive better, and the drive now has records, goals, jumps and pickups. Mining, half of the core loop, had not changed since stashes: one grey rock at every depth, so the most boring minute was digging down. Terraria's and Minecraft's best underground moments are breaking into a layer that looks different and pays differently. Glow caves (#62) were in the "best open picks" and the roadmap's "deep underground" direction; a crystal part closes the loop (the Deep gives you a faster way into the Deep).

**Verified (CI runs 43200975199 and 43203187495, both green)**

- World: 149,823 slate tiles and 8,497 crystals on this seed; slate starts 12.5 m under the garage.
- Deep test: player placed in a cavern at tile (1279,185), 30 m down right under the garage; THE DEEP card shown on arrival; **13 crystals mined in 12 s** on foot (all in reach, then it ran out of reachable ones and stood still — a bot limit); 96–127 crystals glowing within ±52×32 tiles, darkness 1.00.
- Drill cars, same route: plain drill ends at row 97.2 (8.8 m down) at 40 s; **Crystal Drill at row 105.5 (10.9 m down)**, +2 m in the bot's 10 s nose-down dig; depth record call-outs at 5.0/7.6 m (plain) and 5.1/7.7/10.2 m (crystal).
- Nothing else changed: 40 s positions identical to main (starter 2189.5, drill 1473.5/97.2, left 603.2, spiked 300.1, rocket 2155.9), trip reports 259 and 443, ice strip 7.0 s / 4.7 s, Hill Climb 18.97 s / 15.25 s. No errors (only the known "resources still in use at exit" notice).
- Screenshots: `deep_6.png` (THE DEEP card, a cavern lit by cyan crystal clusters, HUD with the crystal slot), `deep_crystal_11.png` ("+1 Crystal (1)" with cyan dust), `crystaldrill_390.png` (cyan cone drill tunnelling; the glowing caverns of the Deep visible below the tunnel — a nice teaser for a player digging down). Run 2 checked the cooler ore tint in slate.

**Not verified / known issues**

- Fun is my judgement. Whether the Deep feels like a discovery, whether crystals are too common (a hundred in view in a deep cavern) and whether 12 m is too deep on foot needs you to play (D1).
- The garage was not driven by the bot: the Crystal Drill was tested as a preset design (`--design=crystal`), not built from mined crystals. Its garage row comes from the same JSON as every other part.
- Crystals all point up, even on cavern ceilings. Hanging crystals would need a flipped tile variant.
- Getting back up from 12 m+ on foot is a long climb (you can place dirt blocks to build steps). That may be the next most boring minute (IDEAS #68).
- No new sounds; the THE DEEP card and depth records reuse the `record` fanfare at other pitches. I cannot listen to check them.

**Next**

1. Crystal Lamp part: a headlight cone from crystals (S, IDEAS #64) — a second use for crystals and makes the dark something you equip against.
2. Surface lift / rope block to ride a shaft back up (S–M, #68).
3. Deep critters or a lava seam below the Deep (M, #65 / #56) — the first underground danger.
4. Perfect chain "PERFECT x3" (S, #46) and the kicker-record trip goal (S, #58).
5. Engine sound per engine part (S, #54).

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
