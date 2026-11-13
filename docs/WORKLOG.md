# Work log

Newest entry first. Each session: what was built and why, how it was verified, what is not verified, and what comes next.

## 2026-11-13, 09:23–09:38 UTC — waited on PR #40, stopped (another session is driving it)

Found PR #40 open; run 43398965672 finished green at 09:25 but its `[bats]` lines show no burst (player 4.0 m from roost 25, which is 3.4 m above the cavern floor, beyond the 3.2 m foot reach). Another session pushed bd70612 at 09:26 and started run 43401407653, still running after 10 minutes, so I changed nothing else and stopped. Note for whoever finishes it: with "only on screen" plus a 3.5 m round reach, a colony over a cavern floor more than about 3 m down can never see a walker (the foot view is only about ±2.3 m tall), so in tall caverns only digging or a honk sets them off. An ellipse (3.2–3.5 m sideways, about 4.8 m up) and bats that swoop down to the startler's height for the first 1.5 s would fix that; I have that change written but did not push it to avoid colliding.

## 2026-11-13, about 04:20–05:30 UTC — The way home: home arrow and the garage lit at night (PR #39)

**Started by** checking for unfinished work: PR #37 (pause menu) was open, its fix run 43363343073 green and `pause_57.png` right (PAUSED panel, sliders at 80/50/30%, "Fullscreen: Off" toggle, gold focused slider name). My squash-merge was refused because the session that opened it pushed a worklog commit at that moment; it merged the PR itself at about 04:50 (9df1c95). I started this branch on top of #37's branch and merged main into it after, so the diff is only this change.

**Built — the way home** (`src/Game/HomeArrow.cs`, `src/Game/GarageBeacon.cs`, `src/Game/WorldScene.Home.cs`, `--lowfuel` in `GameSession`)

- **Home arrow**: whenever the garage door is off screen and more than 10 m away (18 m on foot), an arrow sits at the screen edge on the line from the screen's middle toward the door, with a house badge and the distance. Pale normally; **gold, nudging, "PART READY"** when the next-part card is ready; **orange, blinking, "LOW FUEL"** under a quarter tank (low fuel wins). Fades in and out, pops when its mode changes, hidden while the trip report is up. Kept clear of the hint lines at the top and the bar, card and dashboard at the bottom.
- **The garage at night**: from a little after sunset (night 0.15 → full at 0.65) a lamp post beside the door lights the pad (a real `PointLight2D` plus a halo and a light cone), the shed's inside glows warm (a second light and a wash over the inside), and a searchlight drum on the roof sweeps a beam across the sky (±55 degrees, an 18 s sweep). The glow is drawn additively on the glint layer, so the night tint doesn't dim it.
- No save or world change, no new sound.
- **CI**: `[home]` lines (arrow on / hidden and why, mode, metres to the door, direction east/west, tip position; garage lights on / off with the clock); `homeArrow=Nx/secs ready=… lowFuel=… maxDist=… wrongWay=… beacon=…` on `[autoplay]`. New smoke run `--lowfuel` (30% fuel); a 330-frame screenshot run with it; `home_*.png` from the world, trip, low-fuel and night runs; `beacon_*.png` from the start of the 22:00 run and the dawn run.

**Why this** — Brainstorm in `docs/IDEAS.md` (twenty-ninth session). It was first on the last two sessions' "Next" lists (#162, #174). Looking at `world_230.png`: 100 m out in The Wastes, the card is gold and says "head to the garage", but nothing says which way, and the garage left the screen 90 m ago. Every trip is an out-and-back, so the drive home is the most repeated minute; it should feel like a run home with a prize, not "back there somewhere". The last session was polish (pause menu), so this was a small feature on the driving side that every player meets in the first minute.

**CI runs**: 43366378778 (green), 43368784467 (green; after merging main and three small fixes from the screenshots: the arrow's glow read as a solid gold disc, the searchlight's hot spot was a white blob and its beam faint, and on foot the arrow showed while mining 13 m from the door), and one for this log.

**Verified (run 43366378778)**

- **Every world run**: the arrow appears 10 m out and **always points the right way** (`wrongWay=False` in all 35 world scenarios): west (178–180 degrees) on every eastbound drive, east (0–2 degrees) on the Frozen North drives, straight up (-95 degrees, top edge) in the Deep test 31 m under the garage. The tip sits at the left edge x=46 (right edge x=1234) at the car's height.
- **Ready mode**: the walk bot's Drill Head turns ready at 89 m out → `arrow on: Ready`, then "Ready 126 m … 229 m" (19.2 s gold of 28.7 s shown). Crystal Drill car: ready at 43 m.
- **Low fuel** (`--lowfuel`, 21/75 at the start): Plain at 10 m, **LowFuel at 32 m (fuel 19)**, back to Plain at 47 m after a fuel can (35), Ready, LowFuel again at 167 m (fuel 19), Ready after the next can; 2.5 s of LowFuel in all.
- **Trip test**: shown on both legs out and home ("Ready 81 m … 36 m" on the way back), hidden when the report comes up at 11 m (`report up`), back afterwards.
- **Garage lights**: on at 19:04 (night 0.40) in the dusk run, at 22:00 in the night run, `beacon=1.00` at night and 0.00 by day.
- **Screenshots**: `home_world_228.png` (gold arrow at the left edge, "PART READY · 102 m", the READY card and the PART READY call-out), `home_lowfuel_140.png` (orange arrow "LOW FUEL · 34 m", the fuel gauge at 18/75), `home_lowfuel_158.png` (pale arrow "50 m" after a can), `home_nightleft_143.png` (night, arrow at the right edge "27 m", the lamp's pool of light on the hill behind), `beacon_5.png` / `beacon_dawn_5.png` (the garage at 22:03 and 04:17: lamp post with a warm halo, the pad and the shed lit orange, the searchlight drum on the roof, its beam going up).
- **Nothing else changed**: every scenario's last `[autoplay]` car position is identical to main's run 43365682635 (35 of 35; the new low-fuel run ends where the starter does, 2209.3). No errors.
- **Run 2 (43368784467)**: the same numbers and the same final positions in every scenario; the hook and vein runs (mining 13–15 m from the door) no longer show the arrow, the Deep test (31 m under) still does. `home_world_228.png` now shows a clean gold arrowhead with a faint glow beside the PART READY · 102 m badge; `beacon_dawn_5.png` the smaller searchlight hot spot and the beam rising past the top of the screen.

**Not verified / known issues**

- Fun is my judgement (D1): whether a pale arrow that is always there is calm help or clutter, and whether the gold arrow pulls you home.
- The searchlight only reaches about 15 m of the road at the drive zoom (a side view shows little sky); beyond that only the arrow helps. The beam is faint by design; nobody has seen it moving (frames only).
- The arrow points at the door in a straight line, not along the road; underground it points through the rock (up), which is right for the hook but not a route.
- No arrow to the car when you walk away from it (an easy follow-up if players lose the car).

**Next**

1. Speed traps on the road (S, IDEAS #181) or fresh heaps on the way home (S, #182): something to do on the drive back.
2. Cave echo (S, #178) then cave bats (S, #170) for the Deep.
3. Fumes finish with a range readout (S, #175 + #184); then a first creature that bites (M, #65).

## 2026-11-13, about 03:20–04:20 UTC — Merged lucky strikes (PR #36), then a pause menu (PR #37)

**Started by** checking for unfinished work: PR #36 (lucky strikes) was open and green (run 43356684152). Its `[lucky]` lines showed the census (437 ore tiles near the garage, 30 lucky), the test tile paying `+2 +4 extra = 6 Scrap` and natural strikes by the drill and pickaxe; `lucky_114.png` shows LUCKY STRIKE! with the gold ring and `lucky_gleam_109.png` the gold-rimmed tile. Squash-merged (e682c65). The session that opened it was still running and opened a docs PR (#38) afterwards; I left that one alone.

**Built — pause menu** (`src/Game/PauseMenu.cs`, `src/Game/WorldScene.Pause.cs`, `src/Core/Settings.cs`; buses in `src/Core/Sfx.cs`)

- **Esc no longer quits the game.** Esc (Start on a gamepad) stops the world and opens a menu: Resume; SOUND sliders for Master, Effects and Engine (each move plays a short preview: a pick tink, or a gear-shift for the engine); SCREEN: Fullscreen; Save and quit; and on the right the full key list for on foot and driving. Esc, Start or B resumes. The trip report keeps the first Esc.
- Two new audio buses made in code at start: `Effects` (one-shots, crickets, dust devils) and `Car` (engine, drill, boost, wind, turbo).
- Volumes and fullscreen are kept in `user://settings.cfg`, apart from the save (no save version change). Saved when the menu closes.
- Arrow keys and the d-pad walk Resume → Master → Effects → Engine → Fullscreen → Save and quit (wrapping); left/right moves a slider.
- "Esc: menu" at the end of the on-foot hint line; README key table updated.
- **CI**: new smoke run `--pausetest` (the walk bot, which at t = 4 s sends a real Esc key event); `[pause]` lines; `pauses=` on `[autoplay]`; a screenshot run keeping `pause_*.png`.

**Why this** — Brainstorm in `docs/IDEAS.md` (twenty-eighth session). The last three sessions were features, so this was the polish session. The roughest moment in the game was not in the play: Esc ended the game, nobody could turn the engine down, and the keys were only in the README. It is the first thing any tester reaches for (IDEAS #172).

**CI runs**: 43359321308 (green; its screenshots were overwritten on the screenshots branch by two other runs that finished right after it, and the artifact download is blocked from here), 43361315835 (green, docs only; screenshots seen), and 43363343073 (green) for two small fixes from looking at them: the fullscreen check box was dark on dark, so it is now a toggle button ("Fullscreen: Off/On") styled like the others, and the focused slider's name turns gold so a keyboard or gamepad player can see which slider moves.

**Verified (run 43359321308)**

- **Smoke `--pausetest`**: `bot presses Esc at t=4.00s` → `world stopped … on foot` → `opened paused=True focus=Resume master=1.00 effects=1.00 engine=1.00` → `sliders master=0.80 (-3.9 dB) effects=0.50 (-12.0 dB) engine=0.30 (-20.9 dB) buses=3` → `two presses of Down: focus=Effects` → `settings saved (Ok) and read back from user://settings_pausetest.cfg: master=0.80 effects=0.50 engine=0.30` → `bot presses Esc` → `closed after 30 frames paused=False` → **`world resumed: t=4.00s -> 4.00s, player moved 0.0 px, car moved 0.0 px while paused`**. Afterwards the bot kept digging (player row 63.6 → 72.6 by t=10 s), got in and drove (31–33 km/h at t=12–14 s): the world comes back.
- **Across processes**: the screenshot run (a new process, same user folder) opened the menu with `master=0.80 effects=0.50 engine=0.30`, the values the smoke run had saved.
- **Screenshots** (run 43361315835): `pause_44.png` and `pause_57.png` show the dimmed world behind a gold-rimmed PAUSED panel: Resume, the three sliders at 80% / 50% / 30%, Fullscreen, Save and quit on the left, the on-foot and driving keys on the right, "Esc / Start: resume" under it; in `pause_44` Resume has the gold focus frame, in `pause_57` (after two Down presses) the Effects grabber is lit. `pause_76.png`: the menu gone, the mechanic deeper in his hole, play going on. After the fixes (run 43363343073, same `[pause]` numbers) `pause_57.png` shows "Effects" in gold next to its lit grabber and a "Fullscreen: Off" button.
- **Nothing else changed**: every other smoke run green, no errors; the bus change only reroutes players (Master is unchanged at 100% by default).

**Not verified / known issues**

- Fullscreen as a real window mode change was never exercised (CI does not toggle it; it would change the screenshot size).
- That the sounds actually get quieter is checked through the bus dB only; nobody has listened.
- The garage screen has no pause menu: Esc there still means "back to the world". `drive.tscn` (the test track) still quits on Esc.
- Clicking Resume with the left mouse could, in theory, start a pickaxe swing on the next frame if the button is still held; the button fires on release, so it should not.

**Next**

1. Homeward arrow (S, IDEAS #162) with a garage beacon at night (S, #174): "find your way home".
2. Cave echo on the new Effects bus (S, #178), then cave bats (S, #170).
3. Fumes finish (S, #175); a first creature that bites (M, #65).

## 2026-11-13, about 02:20–03:40 UTC — Merged the next part card (PR #35), then lucky strikes (PR #36)

**Started by** checking for unfinished work: PR #35 (next part card) was open with its docs run (43351416594) in progress; it finished green at 02:40. Its `[nextpart]` lines counted with the bar (Drill Head progress 0.17 … 0.83, then READY; Hydraulics READY; Off-road Wheel counting) and `nextpart_ready_63.png` shows the gold READY card and the PART READY call-out, so I squash-merged it (8701603). The Spiked Tyre showing READY with 0 Scrap on the bar is right: the old tyres' Scrap is refunded in the swap.

**Built — lucky strikes** (`src/World/LuckyOre.cs`, `src/World/LuckyGleams.cs`, `src/Game/WorldScene.Lucky.cs`, `lucky` sound)

- About **one ore tile in 16** (scrap, iron, copper, crystal) is lucky, chosen by a hash of its position: nothing saved, no world-generation change, no save version bump.
- A lucky tile **gleams**: a gold halo and rim that breathe, a white shine sweeping corner to corner every 2.2 s, a star popping at the corner. Drawn on the glint layer over the underground darkness, full within 15 m, gone by 27 m.
- Breaking it (pickaxe or drill) pays **x3**: "LUCKY STRIKE!" and "x3 +6 Scrap" pop over the tile, a gold shock ring, the stash fountain, a jingle (coin ching + a rising bell arpeggio; deterministic, every older WAV byte-identical, checked with md5), a small shake, birds startle; the extra flies home to the bar.
- The first lucky tile near you on foot: "Something gleams gold... lucky ore pays triple".
- **CI**: every run logs a census of ore and lucky tiles within 50 m of the garage and the first gleam to come into view; `[lucky] STRIKE` lines; `lucky=… luckyUnits=… gleams=…` on `[autoplay]`. In `--veintest` the 7-tile test vein is kept ordinary (so its numbers stay) and the lone scrap tile after it is made lucky. Screenshots `lucky_gleam_*.png`, `lucky_*.png` (vein run) and `lucky_world_*` / `lucky_drill_*` / `lucky_left_*` (the first natural gleam in those runs).

**Why this** — Brainstorm in `docs/IDEAS.md` (twenty-seventh session). Mining now has a combo (vein runs), a payoff picture (ore flies home) and a target (next part card), but no surprise: every iron tile is worth one iron, so the fifth minute of digging is the first one again. Every game this borrows from hides a jackpot in the routine (Minecraft's diamond in a cave wall, Terraria's glinting gems). It is felt in the first minutes (six lucky tiles in the top 3 m near the garage) and fitted one session. The last session was a UI feature and the one before a car part, so a small feature on the mining side.

**CI runs**: 43354044562 (green), 43356684152 (green: a halo around the gleam, and a log line plus screenshots for the first natural lucky tile in the walk, drill and left runs), and this log update in its own small PR: PR #36 was squash-merged at 5bd7000 (green, run 43356684152) by the next scheduled session, which started while my run-2 notes were still being written.

**Verified (run 43354044562)**

- **Census**: 437 ore tiles within 50 m of the garage, **30 lucky (6.9%)**, 6 in the top 3 m; the nearest is scrap 3.3 m from the pad, 2.3 m down.
- **Vein test**: the test vein unchanged (Iron 0 → 8 for 7 tiles, x7, one bonus); the lone scrap tile: hint "Something gleams gold..." at 0.5 m, then `STRIKE ScrapOre at (1317,64) by pickaxe: +2 +4 extra = 6 Scrap`; `[flyhome]` right after: Scrap shown 0 of 6 (6 icons in the air), 1.5 s later 6 of 6, bar settled.
- **Natural strikes**: the drill car hit lucky scrap twice in its runs (`+2 +4 extra = 6 Scrap`), the Deep test a lucky crystal (`+1 +2 extra = 3 Crystal`).
- **Screenshots**: `lucky_gleam_106.png` (the gold-rimmed scrap tile at the mechanic's feet, the hint call-out), `lucky_112.png` / `lucky_114.png` (LUCKY STRIKE! in gold, "x3 +6 Scrap", the gold ring, the fountain, six scrap icons leaving).
- **Run 2 (43356684152)**: the same strikes and numbers; the first natural gleam in every world run is lucky scrap at (1293,73), **4.9 m from the mechanic at spawn**, under the garage pad 2.3 m down. It sits just below the bottom edge of the screen at the walk zoom (`lucky_world_15.png` shows the garage and an ordinary scrap tile; the lucky one is off screen), so a player finds it by digging down at the garage, not at a glance. `lucky_gleam_106.png` shows the halo around the gold rim.
- **Nothing else changed** (both runs): every 40 s position identical to main (starter 2209.3, drill 1414.8, Crystal Drill 1480.1, left 465.7, spiked 424.9, turbo 2302.1, devils 2173.2 / 2533.3, rocket 2552.7, flip 2543.3, hopper 2392.4, trip 1629.2). No errors.

**Not verified / known issues**

- Fun is my judgement (D1): whether 1 in 16 is a treat or too common, and whether the gleam reads as different from a stash glint (both are gold; stashes twinkle as stars, lucky ore is a gold-framed tile).
- The jingle is tuned by numbers only.
- No screenshot of a gleam underground in the dark (the Deep test struck a lucky crystal but takes no frames of it); the gleam is on the same layer as the stash glints, which do show in the dark.
- The first-strike explanation call-out did not show in the vein test because the hint was still on screen; the pops say LUCKY STRIKE anyway.
- The ore's own "+2 Scrap" pop sits behind the flying icons for a moment (`lucky_112.png`); older behaviour, more visible here.

**Next**

1. Homeward arrow (S, IDEAS #162): a part is ready and the garage is off screen → a gold arrow at the edge.
2. Hop stomp (S, #154) or pause menu with volume and keys (S, #172, needed before outside testers).
3. Cave bats (S, #170), then a first creature that bites (M: crystal mites #65).

## 2026-11-13, about 01:24–02:30 UTC — Next part card (PR #35)

**Started by** checking for unfinished work: PR #33 (hydraulics) was open and green (run 43341976592: `[hop]` tap 0.65 m, full squat 1.82 m, kicker hops 2.6–3.9 m and 1.16–1.54 s of air; every 40 s position equal to main; `hop_87.png` shows KICKER HOP! with the car high over the kicker). The session that opened it was still running: it merged main into the branch at 01:26 and started another run, so I left the merge to it (it squash-merged at about 01:40, 718ea8a) and built on top of its branch, rebasing onto main after the merge. No second open PR of mine in the meantime.

**Built — the next part card** (`src/Game/NextPart.cs`, `src/Game/NextPartChip.cs`, `src/Game/WorldScene.NextPart.cs`, `goalTier` in `data/parts/parts.json`, `ready` sound)

- A small card above the inventory bar: **NEXT [icon] Off-road Wheel [scrap] 0/2 [iron] 3/4** with a progress bar along its bottom. Counts follow what the bar shows, so they tick up as each flying ore icon lands; the bar slides up with a white glint.
- Which part: upgrades opt in with `goalTier` (1: Drill Head, Off-road Wheel; 2: Rocket Booster, Turbocharger, Spiked Tyre, V8, Hydraulics; 3: Crystal Drill). Parts on the car, or beaten by a same-kind part of the same or higher tier, are skipped. Cost = what building it adds to your car (wheels as a full set with the old tyres refunded, engine and drill as a swap, the rest one block). Closest first, later tiers held back by 0.2 per tier; it only switches when another part is 0.1 closer.
- When the last ore lands: the card turns **gold and pulses** ("READY … build it at the garage", "G: build it" at the door), a gold ring flashes, a **"PART READY: Spiked Tyre! / head to the garage to build it"** call-out (it waits for a fresh call-out) and a new chime (`ready.wav`: three ratchet clicks, then ding-DING a fifth apart; deterministic, every older WAV byte-identical, checked with md5). A save that is already ready shows the gold card without the call-out. Hidden while the trip report is up. No save or world change.
- **CI**: `[nextpart]` lines (what is shown, progress, READY with frame), `nextPart=… partsReady=… firstReady=…` on `[autoplay]`; the vein screenshot run is 300 frames (was 180) and keeps `nextpart_*.png` / `nextpart_ready_*.png`.

**Why this** — Brainstorm in `docs/IDEAS.md` (twenty-sixth session). The last sessions made mining loud (vein runs), collecting visible (ore flies home) and gave the car a new move (hydraulics), but nothing told you what the ore was *for* until you opened the garage after the trip. The most boring minute was digging without a target. Hill Climb Racing keeps the next upgrade's price in sight; Terraria lights up the crafting list when you pick up the last bar. It was on the last two sessions' "Next" lists (#150) and is felt within the first minute: the walk bot's card fills from 0 to READY in its first trip.

**CI runs**: 43346790987 (green; the logic worked in every scenario but the card was invisible in every screenshot: it measured its own size, which is zero for a control straight under a CanvasLayer, so it drew above the top of the screen — the call-out and the logs were fine), 43348795565 (green; the card is drawn from the viewport size and shows above the bar), and one for these docs.

**Verified (run 43346790987 for the logic and positions; 43348795565 for the screenshots, same logic)**

- **Walk-and-drive bots (starter car)**: the card shows Drill Head (Scrap 4, Iron 2) from the start and counts 0.00 → 0.17 → 0.33 → 0.50 → 0.67 → 0.83 as scrap and iron land, then **READY Drill Head** (bar Scrap 9, Iron 2) in the launch, rock, turbo, night, devils, flip, stash, trip and flags runs (`partsReady=1 firstReady="Drill Head"`). Runs that carry copper home then get **READY Hydraulics** (Iron 3, Copper 2) and the card jumps to it.
- **Vein test**: Drill Head 0.17 → 0.33, then it switched to Off-road Wheel at 0.50 (closer by 0.17), 0.67; **READY Spiked Tyre** (Iron 6 for the pair, the old tyres refunding 2 Scrap) at frame 55 and **READY Off-road Wheel** (Scrap 2, Iron 4) at frame 121 when the lone scrap tile's icons landed.
- **Stash test**: seven parts became ready (Drill Head, Hydraulics, Off-road Wheel, Rocket Booster, Turbocharger, V8, Spiked Tyre); the card ended on the Spiked Tyre (`partsReady=7`).
- **Screenshots** (run 43348795565): `nextpart_20.png` (NEXT [drill icon] Drill Head, Scrap 0/4, Iron 1/2, a thin green bar under it, centred above the inventory bar), `nextpart_ready_57.png` / `nextpart_ready_63.png` (the gold READY Spiked Tyre card "build it at the garage" with the PART READY call-out at the top, the iron icon still flying home), `nextpart_60.png` (the Iron slot ringing as the 6th iron lands under the gold card).
- **Nothing else changed**: every 40 s position in run 43346790987 equal to main (turbo 2302.1, night 2209.3, devils 2173.2 / 2533.3, rocket 2552.7, flip 2543.3, hopper 2392.4, trip 1629.2, flags 2209.3; the starter, drill and left runs fell outside the part of the log the tools return, and nothing in this change touches driving). No errors.

**Not verified / known issues**

- Fun is my judgement (D1): whether the card pulls you to "one more ore" or is clutter, and whether the game's choice of part is the one you would pick (you cannot pin one yet, IDEAS #161).
- The chime is tuned by numbers only.
- When several parts become ready at once (a big stash), only the last gets the call-out; the card shows that one.
- Spiked Tyres count as an upgrade over Off-road Wheels (tier 2 vs 1): once you have spikes the card never suggests Off-road Wheels.
- The cost assumes your built car is the car you drive; with a garage design that differs from the built car the real price can differ a little.

**Next**

1. Homeward arrow (S, IDEAS #162): a part is ready and the garage is off screen → a gold arrow at the screen edge with the distance.
2. Where to find it (S, #160) and pin a part at the garage (S, #161); garage afford glow (S, #165).
3. Hop stomp (S, #154) and boulders and ditches (S–M, #155) for the hydraulics; then a first creature (#158 night eyes or #65 crystal mites, M).

## 2026-11-13, about 00:20–01:50 UTC — Merged ore flies home (PR #32), then Hydraulics (PR #33)

**Started by** checking for unfinished work: PR #32 (ore flies home) was open with its second build (43335873583) running; it finished green at 00:32. Its `[flyhome]` lines matched the PR (the vein test: Iron 0 → 8 on the bar one landing at a time, flights 0.60–0.66 s; "right after the last tile: Iron shown 8 of 8, Scrap shown 0 of 2" with the two scrap icons still in the air, then 2 of 2), every 40 s position was identical to main, and `flyhome_14.png` / `flyhome_stash_44.png` / `flyhome_stash_48.png` show the icon on a bright disc rising out of the hole, the toolbox's fan of icons with an x4 tag, and the Iron slot ringing on landing. I squash-merged it (63d47ff); the session that built it was still running and added its own worklog entry below (PR #34). Two sessions overlapped for about an hour; only docs collided, merged by hand in this branch.

**Built — Hydraulics, a new part** (`src/Vehicles/SoftVehicle.Hydraulics.cs`, `src/Game/WorldScene.Hop.cs`, `data/parts/parts.json`, `hop` sound, sprite 14 in `parts.png`)

- **Hydraulics** (3 Iron + 2 Copper, a block anywhere in the body). Hold **W** (Up, B on a gamepad): the body squats onto its springs and a bar over the block fills (yellow → red, blinks when full) in 0.5 s. Let go: the car hops, mostly along its own up. A tap is a little hop, a full squat a big one. Squat in the air and let go just before touchdown and it fires on landing (a bounce). One hop per touchdown, 0.7 s recharge, 0.6 fuel each.
- Hop sound (valve click, a deep thump sliding down, a vent of air; its own random stream, every older WAV byte-identical, checked with md5), a dust burst from the wheels, a little shake, nearby birds startle. "HYDRAULICS!" call-out on the first hop, "BIG HOP x.x m" for 1.4 m+ on the flat, "KICKER HOP!" for a charged hop off a kicker's lip. "W: hop (hold to squat)" on the hint line when the car has it; README and the garage stats line ("Hop 4.2–8.8 m/s up").
- A perfect landing after a hop on the flat is graded "PERFECT HOP LANDING" but gives no speed kick and doesn't count for the trip (otherwise bunny-hopping would beat driving). Off a kicker it counts as usual, flip kicks always.
- No save or world-generation change; old saves simply see a new part in the garage.
- **CI**: `--hoptest --design=hopper` (starter + Hydraulics beside the cab): a tap about 5 m out, a full squat about 12 m out, then it squats on every kicker's run-up and lets go two tiles short of the crest. A second run adds `--fliptest`. `[hop]` lines; `hops=… kickerHops=… bestHop=… bestHopAir=…` on `[autoplay]`; `hop_*.png` / `hop_squat_*.png`.

**Why this** — Brainstorm in `docs/IDEAS.md` (twenty-fifth session). The last two sessions were mining polish, so this one is a feature, on the car side: it had no new part since the Turbocharger, and every part so far makes the car faster or makes it dig; none gives the driver a new move. The flip test said the starter car's kicker jumps are too short for a flip, so the best trick in the game needed the Rocket Booster. A hop is felt on the first press, helps with the most annoying minute of driving (a lip or step you can't get over), and turns every kicker into a bigger jump. Hydraulics (#89) had been near the top of the ranked list for ten sessions.

**CI runs**: 43338984517 (compile error: a `\n` in a call-out turned into a real line break by my edit script), 43339254075 (green), 43341976592 (green), and one for these docs.

1. Run 2: the hop worked (tap 0.93 m, full squat 2.39 m, 0.83 s), but the tap was bigger than meant, and the bot let go of its kicker squats at the first bump of the run-up (one wheel off for a moment), 5–20 tiles early, so every kicker hop was a 0.02-charge tap. Even so, two of them gave 1.04 s and 1.14 s of air off the 1.75 m kickers. Run 3: tap 330 → 270 px/s, and the bot lets go only two tiles short of the crest.

**Verified (run 43341976592)**

- **Flat**: tap (charge 0.05, 4.4 m/s up) rose **0.65 m**, 0.54 s in the air; full squat (8.8 m/s) rose **1.82 m**, 0.79 s (2.39 m in run 2 from a different spot). The full hop's landing was not given a speed kick.
- **Off kickers** (Hopper, no flips): five kicker hops, charge 0.62–1.0, released 1.8–2.0 tiles short of the crest: rose 2.6–3.9 m, **1.16–1.60 s of air**; the starter car without Hydraulics gets **0.62–0.67 s** off the same kickers (its own runs in this build). Two perfect landings (+15, +16 km/h), two clean, no damage.
- **Flips**: with `--fliptest` the starter car + Hydraulics **landed a BACKFLIP** (-353 degrees in 1.27 s, rough) **and a FRONTFLIP** (360 degrees in 1.47 s, perfect landing, +24 km/h) — the plain starter car's flip run lands none (roof landings, 0.65 s of air).
- **Screenshots**: `hop_squat_34.png` / `hop_squat_83.png` (the car squatting, the orange charge bar over the Hydraulics block beside the cab), `hop_38.png`–`hop_45.png` (the full hop: dust burst at the wheels, the car a car-height up on the flat), `hop_85.png`–`hop_90.png` (KICKER HOP!, the car high over the kicker with its landing shadow far below). Run 2's `hop_41.png` / `hop_43.png` show the same.
- **Nothing else changed**: every 40 s position identical to main — starter 2209.3, drill 1414.8, Crystal Drill 1480.1, left 465.7, spiked 424.9, turbo 2302.1, devils 2173.2 / 2533.3, rocket 2552.7, flip 2543.3, trip 1629.2, flags 2209.3. No errors. (The Hopper ends at 2392.4: its kicker hops land further on.)

**Not verified / known issues**

- Fun is my judgement (D1): whether the hop feels punchy or floaty, whether 1.8–2.4 m is too much (it clears most of the screen's height at the drive zoom), and whether kicker hops make every jump too easy.
- The human keys (W, Up, gamepad B) were not exercised; the bot drives the same code through `AutoHop`. The air-release bounce (squat in the air, let go before touchdown) was not exercised by a bot.
- A full hop released on a slope or with one wheel down tips the car (the full flat hop landed with 50 degrees mismatch); that may feel wild or wrong.
- The squat is subtle on screen; the charge bar is the clear signal. Sound tuned by numbers only.
- "KICKER HOP!" replaces whatever call-out was showing (the trip goal's GOAL DONE line stayed at the top right).

**Next**

1. Hop stomp (S, IDEAS #154): land a hop on a scrap heap for STOMP! — gives the hop a target on every road.
2. Boulders and ditches on the Wastes road (S–M, #155), a reason to hop (save version bump).
3. Lucky strike (S, #151) / recipe nudge (S, #150) for the mining side; then a first creature (#158 night eyes or #65 crystal mites).
## 2026-11-12 23:24 – 2026-11-13 00:50 UTC — Merged vein runs (PR #31), then ore flies home (PR #32, polish and feel)

**Started by** checking for unfinished work: PR #31 (vein runs) was open with run 43327542167 in progress. It finished green at 23:36 and `vein_22.png` showed the long call-out centred. My squash-merge was refused because the session that opened the PR was still running and had just pushed a worklog line; that session merged it itself at 23:48 (d6155ee), so I branched from that main.

**Built — ore flies home** (`src/Game/OreFlight.cs`, `src/Game/WorldScene.FlyHome.cs`, small hooks in `WorldScene.cs`)

- Everything mined (pickaxe or drill ore), dug up (stashes) or smashed (scrap heaps, combo scrap) **flies from where it broke into its slot on the inventory bar**: tossed up out of the hole, then down in an arc, with a short glow trail, on a bright disc in the ore's colour with a white rim. A bundle fans out (up to five icons, the last carrying the rest with an "x4" tag).
- The slot's number **goes up when the icon lands** (the bar shows what you have minus what is in the air); the slot swells, a ring flashes and the number flashes in the ore's colour, with a quiet tick for ore.
- Dirt and stone fly too on foot (smaller, faster, silent); dug by the car's drill they just count up. At most 48 icons in the air. The real counts change at once (saving, building and placing are unaffected); no save or world change; no new sound file (`tally` pitched up).
- **CI**: `[flyhome]` lines (launch from screen point to slot point, landing with flight time and what the bar shows); `flown/landed/unitsHome/inFlight/longestFlight/barSettled` on `[autoplay]`; the vein test checks the bar right after the last tile and 1.5 s later; `flyhome_*.png` (vein) and `flyhome_stash_*.png` frames.

**Why this** — Top of the last session's "Next" (IDEAS #143). After vein runs, breaking ore is loud, but the ore then vanished: the bar at the bottom, the only place your wealth lives, changed a digit nobody watched. Collecting needs a picture (Terraria's items flying to you, Hill Climb's coins zipping to the counter). Felt in the first minute: the walk bot sends dirt home within seconds of spawning. Polish two sessions running, but it was the item the last session queued.

**CI runs**: 43332602108 (green; icons small and dark on dirt and barely rose, `flyhome_12/14.png`), 43335873583 (green; bigger icons on a bright disc, tossed higher). PR #32 was squash-merged at 00:34 (63d47ff) by another session running at the same time (it then opened PR #33, hydraulics) before this docs commit reached the branch, so these notes come in their own small PR.

**Verified (run 43335873583)**

- **Vein test**: 7 iron tiles + the 4th-ore bonus = 8 iron flights, each landing in slot 4 after 0.53–0.63 s with the bar counting 1, 2, ... 8; the bonus launched separately ("bar shows 3 (have 5)"). Right after the lone scrap tile: `flown=10 landed=8 inFlight=2 barSettled=False`, Iron shown 8 of 8, Scrap shown 0 of 2; **1.5 s later** `flown=10 landed=10 unitsHome=10 inFlight=0 barSettled=True`, Scrap 2 of 2.
- **Stashes**: the old toolbox's +8 Scrap went as 5 icons (the last "x4"), the bar counting to 8; the strongbox's +5 Copper as 5 icons counting 1..5. Stash test: 35 icons carried 38 units home.
- **Every scenario**: launched = landed + in flight at every log line; every world run ends settled or with its last icons in the air (2–4) at the moment the run quits. Longest flight 0.75 s (a crystal from high on the screen).
- **Screenshots**: `flyhome_14.png` (the first iron icon tossed up out of the hole on its disc), `flyhome_stash_44.png` (a fan of scrap and iron icons over the opened toolbox, "x4" on the last), `flyhome_stash_48.png` (scrap icons dropping to the bar, the scrap slot showing 1 while the rest fly, the iron slot ringing).
- **Nothing else changed**: every 40 s position identical to main (drill 1480.1, left 465.7, spiked 424.9, turbo 2302.1, night 2209.3, devils 2173.2 / 2533.3, rocket 2552.7, flip 2543.3, trip 1629.2). No errors.

**Not verified / known issues**

- Fun is my judgement (D1): whether a stream of dirt icons while digging on foot is satisfying or busy (easy to switch off for dirt and stone; see DECISIONS).
- The tick is tuned by numbers only.
- If the window is resized during a flight the icon still lands in the slot (its target is read every frame), not checked by a bot.
- Heap scrap smashed at speed flies from the heap, which may be off screen behind the car by then; the icon then comes in from the screen edge (fine, but unchecked in a screenshot).

**Next**

1. Recipe nudge (S, IDEAS #150): a slot glows "Rocket Booster ready at the garage" when you have enough — the bar now has attention, give it a goal.
2. Lucky strike (S, #151) or pickaxe hit feel (S, #145); motherlode (S, #144).
3. A feature next (two polish sessions in a row): pickaxe tiers (S, #139), devil rider records (S, #131), then the first creature (M: crystal mites #65) or cave-ins (M, #152).

## 2026-11-12, about 22:24–23:55 UTC — Merged the grappling hook (PR #30), then vein runs (PR #31, polish and feel)

**Started by** checking for unfinished work: PR #30 (grappling hook) was open, its docs run (43319246575) still going. I waited for it past the usual 10 minutes (it was already in the screenshot step and the code commit before it was green); it finished green at 22:39 with `[hook]` lines matching the PR (out of the 12.0 m shaft in 2.23 s with 4 throws, the miss, the fling) and `hook_8.png` / `hook_50.png` looking right, so I squash-merged it (72194a5).

**Built — vein runs** (`src/Game/VeinRun.cs`, `src/Game/MiningPops.cs`, `src/Game/WorldScene.Veins.cs`, `vein` sound)

- Ore (Scrap, Iron, Copper, Crystal) broken within **3 s** of the last ore and **no more than 3 tiles** from it continues a run: "VEIN x2, x3 ..." pops over the tile, and a glassy tink climbs a major scale with every ore.
- **Every 4th ore of a run pays +1 bonus** of that ore ("+1 BONUS Iron" in gold, a quiet high fanfare).
- Every ore pops its amount ("+1 Iron", "+2 Scrap") in a bright version of the ore's colour where it broke and drifts up, instead of replacing the centre call-out. Text is drawn at screen resolution (crisp) and the same size on foot and in the car.
- The rest of the vein (ore touching the broken one, up to 6 steps, 24 tiles) **sparkles in a ripple**, nearest first, so you see which way it goes on.
- A one-time "VEIN RUN! keep mining the ore next to it: every 4th pays a bonus" call-out. Works with the drill too.
- Small HUD fix found on the way: centre call-outs wider than 400 px grew only to the right (every long toast sat off-centre, e.g. the hook's); they now grow both ways.
- `vein.wav` from `tools/make_sounds.py`, deterministic (every older WAV byte-identical, checked with md5). No save or world-generation change.
- **CI**: `--veintest` buries a 7-tile iron blob under the mechanic east of the pad; the bot mines the nearest ore until none is in reach, waits for the run to go cold, then mines a lone scrap tile (must be x1). `[vein]` lines for every ore broken in every run; `vein=… bestVein=… veinBonuses=… veinRuns=… ripples=… pops=…` on `[autoplay]`; `vein_*.png` screenshots.

**Why this** — Brainstorm in `docs/IDEAS.md` (twenty-third session). The last two sessions were features (devils, hook), so this one is polish and feel, and it was the top of last session's "Next". Mining is the most repeated action in the game and gave the least back: a tile vanishes, "+1 Iron" takes over the centre of the screen. Ore already lies in blobs; a combo with a climbing sound turns "hold the mouse" into following a vein (Minecraft's vein joy, Hill Climb's coin ding), and the sparkle trail shows there is more. Felt in the first minute: the walk bot hits ore within seconds of spawning.

**CI runs**: 43322687840, 43325159287, 43327542167 (all green), and one for this last docs edit.

1. Run 1: everything worked, but the test vein ran 4 tiles down-right and the last two were out of the pickaxe's reach from the surface (the bot mined 5, x5, one bonus); "+1 Iron" in the iron tile's own brown was hard to read on dirt (`vein_13.png`).
2. Run 2: a compact 7-tile blob and brighter pop colours (fine), but my first toast fix made long call-outs grow both ways around the box's left edge, so they sat 200 px left of centre (`vein_22.png`). Run 3 sets the box's offsets to -200..200 so it grows around the centre.

**Verified (run 43325159287)**

- **Vein test**: Iron 0 → chain 1, 2, 3, **4 BONUS +1** (Iron=5 after the 4th tile), 5, 6, **7** → **Iron=8 for 7 tiles**, dug out in 6.4 s; "run over: x7 (best 7, bonuses 1)" 3 s after the last ore; then the lone scrap tile started a new run at **chain=1** (Scrap=2). 6 ripples, 21 sparkles, 15 pops.
- **Natural ore** (drill runs): runs up to **x6** happened by themselves (Scrap x4 BONUS at (1434,84), then two Iron = x6), and single tiles stayed x1. Crystal in the Deep chained x2 at most (the bot walks between clusters).
- **Screenshots**: `vein_13.png` (+1 Iron over the first broken tile, sparkles on the six left), `vein_22.png` (VEIN x2, sparkles rippling over the rest), `vein_40.png` ("+1 BONUS Iron" in gold, "VEIN x4", "+1 Iron" in pale rust, stacked over the hole, sparkles on the last three; Iron 5 on the bar), `vein_66.png` (the last tile).
- **Nothing else changed**: every 40 s position identical to main (starter 2209.3, drill 1414.8, Crystal Drill 1480.1, left 465.7, spiked 424.9, turbo 2302.1, devils 2173.2 / 2533.3, rocket 2552.7, flip 2543.3, trip 1629.2). No errors.

**Not verified / known issues**

- Fun is my judgement (D1): whether the pops and the tink make mining feel good or busy, and whether 3 s / 3 tiles is too strict or too loose.
- The sound is tuned by numbers only; I cannot hear it.
- The bonus also pays while drilling (a drill run past a big vein gets about +25%). Deliberate for now; see DECISIONS.
- The toast centring fix is checked in run 3 (43327542167): `vein_22.png` shows "VEIN RUN!" and its long second line centred on the screen; the vein test and every 40 s position were unchanged in that run.

**Next**

1. Ore flies home (S, IDEAS #143): the ore icon arcs into its slot on the inventory bar and the count bumps — the bar never reacts today.
2. Pickaxe hit feel (S, #145) and a motherlode (S, #144); best vein on the trip report (S, #147).
3. Devil rider records (S, #131), pickaxe tiers (S, #139), then the first creature (M: crystal mites #65).

## 2026-11-12, about 21:25–22:55 UTC — Merged dust devils (PR #29), then the grappling hook (PR #30)

**Started by** checking for unfinished work: PR #29 (dust devils) was open, its docs run (43307483856) still going; it finished green at 21:28 with `[devil]` lines matching the PR (starter caught at 24 km/h, 1.42 s air, 4.9 m up; rocket three rides 0.73–1.94 s), so I squash-merged it (5fecbbb).

**Built — grappling hook** (`src/Game/Player.Hook.cs`, `src/Game/WorldScene.Hook.cs`, `Player.cs`, `hook` / `clank` sounds)

- **E** (or middle mouse; right shoulder on a gamepad, aimed with the left stick) throws a hook toward the cursor. It flies 4 m; on rock it bites (clank, chips in the tile's colour, nearby birds startle) and reels the mechanic in at 640 px/s.
- At the hold you **hang** (no gravity, you can mine from the rope), or, if it caught a ledge with room on top, you **climb onto it**. Space jumps off (mid reel-in a **fling** that keeps the rope's speed), S lets go, E again throws to the next hold while the old one still holds. Mining away the held rock drops you. A miss flies the full 4 m and snaps back.
- The camera leans toward the hook; the hint line has "E: grappling hook" (and the rope keys while on it); a GRAPPLING HOOK call-out on the first bite.
- Rope (twisted, slack while flying, taut while holding) and a two-pronged steel hook drawn by the game. Sounds generated by `tools/make_sounds.py` on their own random stream (every older WAV byte-identical).
- **CI**: `--hooktest` digs a 12 m shaft 3.5 m east of the pad and puts the bot at its bottom; it climbs out with the hook alone (aiming high up alternate walls, then at the lip), throws once into the open sky (must miss), then hooks the ground across the shaft and jumps mid reel-in (fling). `[hook]` lines; `hooks=thrown/bit/missed mantles hangs jumpOffs longestHook` on `[autoplay]`; `hook_*`, `hook_throw_*`, `hookrun_*` screenshots.

**Why this** — Brainstorm in `docs/IDEAS.md` (twenty-second session). Twenty-one sessions went into the car; the on-foot half (mine, explore, get home) has had no new verb since day one, and the most boring minute of the game is climbing out of a hole you dug — pillaring blocks or cutting stairs, 30 m of it from the Deep. A hook turns that minute into the most physical one (Terraria's hook, Spelunky's rope) and opens caverns to zipping about. It is felt in the first minute (E is on the hint line from spawn). The last session was a feature (devils), the one before polish; this one is a feature for the neglected half of the loop.

**How it got there (CI runs: 43312027280 cancelled, 43312082385 compile error, 43312463381 green, 43315305289 green; one more for these docs)**

1. A local variable named `stick` clashed with the walking stick read earlier in `ReadHuman` (CS0136); renamed.
2. Run 3: the climb worked first time. The bot then walked east back into its own shaft (my test, not the game); run 4 replaced that walk with the fling test.

**Verified (run 43315305289)**

- **Climb**: out of the **12.0 m shaft in 2.23 s with 4 throws** (bit 4, missed 0): bites at 3.4, 3.5, 3.4 m up the walls (stone, stone, dirt), hanging after each, then a 1.3 m throw at the grass lip and "climbing onto the ledge" — standing on the surface beside the shaft. Without the hook the only ways up were pillaring about 48 blocks or digging a staircase.
- **Miss**: straight up into the sky flew the full 4.0 m and found nothing (hooks 5/4/1).
- **Fling**: hooked the grass across the shaft 1.6 m away, jumped off over the shaft and landed at x=1324.0, two tiles past the hold (8.5 tiles from the start) — the rope's speed carried.
- **Screenshots**: `hook_8.png` (hanging on a taut rope in the shaft, the hook in the stone above), `hook_23.png` (at the lip, hook in the grass), `hook_25.png` (standing on the surface), `hook_35.png` (the miss: a slack rope with the hook high in the sky), `hook_50.png` (mid-fling over the shaft, GRAPPLING HOOK call-out).
- **Nothing else changed**: every 40 s position identical to main — starter 2209.3, drill 1414.8, Crystal Drill 1480.1, left 465.7, spiked 424.9, turbo 2302.1, devils 2173.2 / 2533.3, rocket 2552.7, flip 2543.3, trip 1629.2; Hill Climb 19.18 / 17.22 / 14.08 s. No errors.

**Not verified / known issues**

- Fun is my judgement (D1): whether the hook makes deep digging more fun or too easy, whether 4 m is the right reach, and whether the reel-in feels snappy or floaty.
- The human controls (E, middle mouse, the gamepad shoulder and stick aim, S to drop) were not exercised: the bot drives the same code through `PlayerInput`. Hanging and then mining from the rope, and the "rock gone" drop, were not exercised by a bot.
- The first-bite call-out is suppressed if another call-out is showing (in run 3 the Deep's title card was), and tries again on the next bite.
- Sounds tuned by numbers only. The hook does nothing to the car yet (IDEAS #137).
- The hook passes through nothing but air: it ignores scrap heaps, fuel cans and birds (they are not tiles).

**Next**

1. Vein run (S, IDEAS #138): a combo for the pickaxe — the on-foot half's second step.
2. Devil rider records (S, #131) / devil loot (S, #133); vultures over strongboxes (S, #123).
3. Pickaxe tiers (S, #139), hook swing (S, #140) once the owner has tried the reel-in, then the first creature (M: crystal mites #65).

## 2026-11-12, about 20:20–21:30 UTC — Dust devils, the first hazard (PR #29)

**Started by** checking for unfinished work: PR #28 (birds and a horn) was open with its last build green (run 43297658269); its `[birds]` lines and `birds_left_172.png` looked right, so I merged it (squash).

**Built — dust devils in The Wastes** (`src/World/DustDevils.cs`, `src/Game/WorldScene.DustDevils.cs`, `src/Vehicles/SoftVehicle.Whirl.cs`, `devil` and `gust` sounds)

- **When and where**: on hot afternoons (heat rises 10:30–11:30, falls 16:30–17:30 on the world clock; a new game reaches 10:30 about a minute in) three stretches of The Wastes road (about 111–176, 178–242 and 244–309 m east of the pad) each grow a whirlwind. It wanders its stretch at 0.7–1.5 m/s, lives 25–45 s, dies down, and another forms 6–20 s later. "MIDDAY HEAT / dust devils are dancing in The Wastes" when the heat comes up while you are east of the garage.
- **What it does**: drive in and it grips the car (strongest at the core and low down): lifts it towards 14 m/s, slows it towards half its speed plus the devil's drift, and twists it towards 2.4 rad/s. After a real ride (more than 0.12 s, at most 1.6 s) it is spent and blows itself out in a burst of sand, so it never juggles you. "DUST DEVIL! / Q/E against the spin · land flat" the first time. The landing is judged like any jump (perfect landing kicks and flips count). On foot it lifts the player and drops them a little way off. Parked cars are left alone.
- **Look and sound**: drawn by the game: a tapering funnel of dusty slices with ragged darker edges, light bands climbing it as it turns, a haze at its foot, sand grains sweeping round in front of and behind the car, bits of debris, sand kicked up. A swirling roar (`devil.wav`, heard from about 25 m, loudest inside) and a whoosh (`gust.wav`) on the grab and the blow-out.
- **CI**: `[devil]` lines (stretches, formed / died down / collapsed / gone, caught with speed and strength, down with time held, air, height, turn, speed and damage), `devils=rides/throws/collapses bestDevilAir bestDevilHeight` on `[autoplay]`; two midday runs (`--time=13`, starter and rocket east) and a midday screenshot run (`devil_*.png`).

**Why this** — Brainstorm in `docs/IDEAS.md` (twenty-first session). Twenty sessions made a rich toy, but nothing in the world could push back: the only danger was your own driving, and "enemies and danger" was the one roadmap direction never started. The Wastes is a long sandy road where the big moments only happen at kickers. A whirlwind gives that road a hazard you see coming and can choose to hit, and it is also a free jump anywhere (and a flip chance for the starter car); it needs no AI, health or art pack, so it fits one session. The last session was polish, the two before were features; this is a feature in a new direction.

**How it got there (CI runs 43301263272 and 43304872499, both green; a third for these docs)**

1. Run 1: the physics worked (the starter was thrown 1.77 s, 5.6 m up; the rocket 3 times), but the devil was nearly invisible on screen (`devil_caught_271.png`: a few faint dots of sand against sand), and the devils of a `--time=13` game only formed 5–8 s in, because they were set up before the clock.
2. Run 2: a real funnel (slices, edges, bands, bigger and denser grains), checked first against a mock-up drawn over a real screenshot; set up after the clock.

**Verified (run 43304872499)**

- **Starter, 13:00, east**: three devils up at the start (148, 207, 272 m); caught by #0 at 136 m at 24 km/h, held 0.38 s, 1.42 s in the air, 4.9 m up, turned 29 degrees, came down with a PERFECT LANDING (the landing judge timed 1.53 s of air, 4 degrees off the slope, +16 km/h kick); the devil blew itself out. 40 s position 2173.2 vs 2209.3 at 08:00 (the ride cost about 9 m).
- **Rocket, 13:00, east**: three rides at 85, 74 and 59 km/h: held 0.13–0.24 s, 0.73–1.94 s in the air, 3.1–9.7 m up, turned 35–68 degrees; damage 4–7, all landed on its wheels (rough). Each devil collapsed after its throw, and new ones formed 10–15 s later.
- **Screenshots**: `devil_caught_284.png` (the funnel on the road ahead of the car), `devil_caught_288.png` / `_291.png` (the car inside the column, tipping, "DUST DEVIL!"), `devil_caught_294.png` (thrown high with the landing shadow far below), `devil_down_302.png` (down level on the next dune).
- **Nothing else changed**: every 08:00 run ends exactly where it did on main (starter 2209.3, left 465.7, spiked 424.9, Crystal Drill 1480.1, rocket 2552.7, turbo 2302.1, flip test 2543.3, trip test 1629.2); Hill Climb 19.18 / 17.22 / 14.08 s. In the trip test the clock passes 10:30 at 56 s and devils form, but the bot never meets one. No errors.

**Not verified / known issues**

- Fun is my judgement (D1): whether devils feel like a fun surprise or a tax on every Wastes drive, and whether the twist is strong enough to matter (the bot turned only 29–68 degrees and never tried Q/E).
- **Warning distance**: at the drive zoom the camera shows about 9 m ahead, so a devil comes on screen only about a second before you reach it at 25 km/h; the roar is the earlier warning. A dust plume on the horizon would fix it (IDEAS #134).
- The on-foot lift and the MIDDAY HEAT call-out were not exercised by any bot (the bot drives from the pad at 13:00, or meets 10:30 only in the trip test while not near a devil). The sounds are tuned by numbers only.
- All six devils in the logs spun clockwise (a coin flip each; maybe chance, worth a look).
- No score or record for a devil ride itself yet (IDEAS #131).

**Next**

1. Devil rider records and a "Ride a dust devil" goal (S, #131), and a dust plume on the horizon so you see them from 30 m (S, part of #134).
2. Devil loot (S, #133): a scrap heap or fuel can whirling in the column, caught if you drive through.
3. Vultures over strongboxes (S, #123), overboost (S, #107), then a creature (M: crystal mites #65 or night eyes #118).

## 2026-11-12, about 19:20–20:30 UTC — Birds and a horn (PR #28, polish and life)

**Started by** checking for unfinished work: no open pull requests; main's build after PR #27 (run 43288451869) was running.

**Built — birds and a horn** (`src/World/Birds.cs`, `src/Game/WorldScene.Birds.cs`, `birds.png`, `flutter` / `chirp` / `caw` / `horn` sounds)

- **Flocks along the road**: 17 flocks every 22–42 m on both sides (never on a kicker), the first about 29 m east and 30 m west of the pad. Sparrows (3–6) in the Home Meadow, big black crows (2–3, drawn at 2x) in The Wastes, white snow buntings with black wings in the Frozen North. Three sparrows sit on the garage roof and sign. Perched birds peck, hop and turn round; at night they sleep (still).
- **They notice you**: a flock takes off when the car comes within 140 px + 0.38 s of travel (6 m at 36 km/h, 12 m at 90 km/h), when you walk up to it on foot (1.5 m), honk (12 m), start the engine (12 m: the roof sparrows go every time you get in), crash (9 m) or dig next to it. Birds flap up and away from the threat, staggered, with a wing-beat bob; a flutter plus a chirp (sparrows, buntings pitched up) or a flutter plus a caw (crows), quieter further from the camera. Scattered flocks come back after 75 s, out of sight.
- **Horn**: H (Y on a gamepad), a two-tone horn pitched by the engine (the Scrap 4 beeps higher, the V8 lower); on the driving hint line.
- **No gameplay change**: no collision, no score, no physics. Birds always get away.
- **CI**: `[birds]` lines (placement, every scatter with cause, distance and speed, honks), `birds=` on `[autoplay]`; the bot honks once at the first flock 7–10 m ahead; `birds_*` screenshots around the first scatters (and the first crows) in the world, left and rocket runs.

**Why this** — Brainstorm in `docs/IDEAS.md` (twentieth session). The driving is rich now, but the world was dead: the only things moving on screen were the car, its particles and the clouds, and nothing noticed you. Animals that react are the cheapest "this is a place" feeling (Terraria's critters, Minecraft's animals), and a flock bursting up ahead of the bonnet is a small spectacle that gets bigger the faster you go. It is felt in the first ten seconds (the roof sparrows flutter off when you start the engine). The last two sessions were features (turbo, day and night), so this one is polish and life.

**How it got there (CI runs 43290870602 and 43294349786, both green; a third for the docs)**

1. Run 1: everything worked, but the car scared flocks 7.5–17 m away (230 px + 0.55 s of travel), so they left before they were on screen (`birds_world_123.png`: the flock is off the right edge when it goes); the roof sparrows were flushed by the player at spawn (1.7 m) on frame 4; sparrows at 1x were hard to see.
2. Run 2: about half a second of warning instead, sparrows and buntings at 1.5x, the roof perch near the player's spawn removed, the on-foot radius 100 px.

**Verified (run 43294349786)**

- **Scatters**: the starter east scared 7 flocks in 40 s (engine 1, horn 1, car 3, crash 2), west to the Frozen North 7 (engine, horn, 5 by the car), the rocket car 9. The car now startles flocks at 4.0–6.2 m at 22–39 km/h and 7.5–10.2 m at 58–87 km/h (rocket). The garage roof sparrows go on the engine start (5.6 m away) in every run, never at spawn. The bot's honk scattered the flock 9.4–10.2 m ahead each time. Up to 10 birds in the air at once.
- **Screenshots**: `birds_left_168.png` (six sparrows sitting on the road ahead of the car), `birds_left_172.png` (the same flock going up, flying away ahead), `birds_world_244.png` / `_248.png` (crows on the sand by a scrap heap, then flapping off past a cactus), `birds_rocket_160.png` (crows waiting as the rocket car flies in), and run 1's `birds_left_103.png` (roof sparrows flying off over the garage as the engine starts).
- **Nothing else changed**: 40 s positions identical to main — starter 2209.3, left 465.7, spiked 424.9, Crystal Drill 1480.1, rocket 2552.7, turbo 2302.1; Hill Climb 19.18 / 17.22 / 14.08 s. No errors.

**Not verified / known issues**

- Fun is my judgement (D1): whether the birds make the world feel alive or busy, and whether half a second of warning feels right, needs you to play. The sounds (flutter, chirp, caw, horn) are tuned by numbers only.
- No still frame shows the roof sparrows sitting (the bot is down its hole when it gets in); `birds_left_103.png` shows them flying off.
- Birds fly through terrain (no collision) and can fly in front of the HUD-free sky forever until 7 s pass; underground flocks never exist, but a flock on a spot you dug out sits on the new ground.
- The horn does nothing else yet (no effect on anything but birds).

**Next**

1. Vultures circling over buried strongboxes in The Wastes (S, IDEAS #123) — the birds become a treasure hint.
2. Biome nights (S, #117) or night owls with shining eyes (S, #127); surface ambience by day (S, #125).
3. Overboost (S, #107), start-light gantry (S, #82), Gyro (S, #94), then the first real danger (M: night raiders or deep critters).

## 2026-11-12, about 18:20–19:20 UTC — Day and night, step 1 (PR #27)

**Started by** checking for unfinished work: no open pull requests; main's build after PR #26 (run 43277162200) green.

**Built — day and night** (`src/Core/DayCycle.cs`, `src/Game/WorldScene.Night.cs`, `src/Vehicles/SoftVehicle.Lights.cs`, `src/Game/HeadlightBeams.cs`, `Backdrop`, `StashGlints`, `Sfx`, `crickets.wav`)

- **Clock**: saved in `progress.json` (`ClockHours`, `Day`; a new game starts at 08:00). About 7 minutes a day: daytime 06:30–18:00 at 13 h per 270 s, dusk and dawn about 40 s each, night hours (21–04) twice as fast. The time shows at the start of the status line; "NIGHT FALLS / headlights on · buried stashes glint from further away" and "DAWN · DAY n" call-outs with a soft chime.
- **Sky**: gradient with a sunset band, sun and moon on an arc (the sun reddens and swells at sunset, the moon has craters and a halo), 150 twinkling stars, clouds that go pink then grey-blue, far hills as silhouettes.
- **Light**: the world is tinted warm at sunset and moonlit blue at night (mixed with the underground darkness). The carried lamp shines on foot at night. The car's **headlights** (a cone light out of the frontmost block), **tail-lights** (brighter when braking) and a **reversing lamp** (cars face right and back up to go west) switch on by themselves at dusk and deep underground; a haze along each beam and lamp flares are drawn over the darkness so the beams show in the air.
- **Night reward**: buried stashes glint from 1.8x further at night.
- **Sound**: a 3 s cricket loop on the surface at night (quieter in the car).
- **CI**: `--time=H`, `--clockrate=N`; `[night]` lines (time, night/warmth, tint, lights, reversing lamp, glints, crickets) and `clock= nightFalls= nightSeconds= headlightSeconds= glintsNight= glintsDay=` on `[autoplay]`; smoke runs "dusk into night (19:00 at 3x)" and "night drive west from 22:00"; `night_*`, `nightleft_*`, `dawn_*` screenshots.

**Why this** — Brainstorm in `docs/IDEAS.md` (nineteenth session). Every minute of the game happened at the same noon: nothing in the world changed while you played. Eight sessions in a row were driving feel and parts, and the roadmap's day and night had never been started. A sunset arriving mid-drive and headlights cutting through the dark are cheap, big "this world is alive" moments, and the night is where danger can live later. It is a feature with a large look-and-feel side; the turbo session before it was a feature, the air cam before that feel.

**Verified (CI runs 43280528180 and 43283115816, both green; a third run for the docs and a small threshold change)**

- **Clock and light** (`--time=19 --clockrate=3`): night falls at 19:15 (frame 17 of the screenshot run); tint goes (0.74, 0.66, 0.69) at 19:00 → (0.49, 0.48, 0.61) at 19:35 → full night by 20:45; headlights on from 19:35; night 38.3 s of the 40 s run, headlights 40 s, clock ends at 04:16 on day 2. Crickets 0.6 in the car, up to 1.0 on foot.
- **Night glints**: same route east, same 40 s: at most **4 stashes glinting at night vs 2 by day**; driving west at night 5.
- **Reversing lamp**: the night drive west has `reverseLamp=1.00` the whole way.
- **Screenshots** (run 2): `night_10.png` (sunset at the garage: orange sun on the hills, pink clouds), `night_40.png` (NIGHT FALLS call-out), `night_180.png` (moon, stars, the headlight beam lighting the hill ahead), `nightleft_200.png` (reversing lamp and headlights both shining, the beam lights the hill behind), `dawn_100.png` (pink dawn sky, 05:39).
- **Nothing else changed**: 40 s positions identical to main — starter 2209.3, left 465.7, spiked 424.9, Crystal Drill 1480.1, rocket 2552.7, turbo 2302.1; Hill Climb 19.18 / 17.22 / 14.08 s. No errors.

**Not verified / known issues**

- Fun and looks are my judgement (D1): whether the night is too dark, too long or too short, and whether you want to drive in it or wait it out. Headlights are judged from still frames only.
- The crickets are tuned by numbers only (I cannot hear them).
- Underground, the headlights switch on from depth alone; the threshold was raised after run 2 (a drill car at 2 m showed a beam in a half-lit tunnel); the third run's `drill_280.png` should show it off — check it.
- The night has no gameplay effect beyond the glints: no scoring, no danger, fuel and spawns unchanged. The garage and Hill Climb scenes are always daytime.
- The clock only runs in the world scene: time stops while the garage is open.

**Next**

1. Biome nights (S, IDEAS #117: fireflies, aurora, big desert moon) or glowing eyes by the road (S, #118) — make the night worth seeing.
2. Night run bonus on the trip report (S, #116); Floodlight part (S, #119).
3. Overboost (S, #107), start-light gantry (S, #82), Gyro (S, #94), cave bats (S, #110), then the first real danger (M: night raiders or deep critters).

## 2026-11-12, about 17:25–18:15 UTC — Merged air cam (PR #25), then the Turbocharger (PR #26)

**Started by** checking for unfinished work: PR #25 (air cam) was open, its last run (43267689584, a re-check after the one-off exit crash) still going; it finished green at 17:30 with a log matching the PR (61 big-air touchdowns, 0 frames with the car off screen, no errors), so I squash-merged it (ccb69f3).

**Built — Turbocharger** (`turbo` in `data/parts/parts.json`, `src/Vehicles/SoftVehicle.Turbo.cs`, `WorldScene.Turbo.cs`, dashboard, art and sounds)

- **The part**: a new block category, Turbo (2 Iron + 3 Copper, mass 0.4), meant to sit on top of the engine. Boost (0..1) builds with the throttle held past half, at a rate set by the revs (none below 0.3 of the redline, full rate 1/0.9 s from 0.8 up); off the throttle it bleeds at 2.6/s, each upshift costs 0.25. At full boost: +40% engine torque, +12% top spin, +30% fuel use. Only the best turbo on a car counts.
- **Feel and sound**: a whistle loop (`turbo.wav`: 2.3 kHz whine with harmonics and a band of rushing air) whose pitch and volume rise with the boost; lifting off with 0.45+ boost plays the blow-off (`blowoff.wav`: a sharp hiss with a fluttering tail) and puffs air out of the turbo. A copper snail-shell sprite (`parts.png` cell 13); the game draws a compressor wheel on it that spins with the boost and glows orange near full. A **BOOST** arc inside the speedometer fills pale blue to hot orange and pulses at full. The first full boost of a session says "TURBO! Full boost: +40% torque / Lift off to hear it blow off".
- **Launch control spools it**: an armed launch holds the revs high, so the turbo is spooled when you let go — a plain start feels the lag, a perfect launch does not.
- **Exit crash fixed**: world generation (and stash/heap loot, the test terrain, ore boulders) now disposes its `FastNoiseLite` / `RandomNumberGenerator` objects (`using var`). This PR's first run hit the same segmentation fault at exit that PR #25 saw once ("Leaked unsafe reference" for exactly these objects, then signal 11 after the Scrap 4 launch test had finished). The second run had no leak lines and no crash.
- **CI**: `--design=turbo` (starter pickup + turbo on the V6); runs: world 40 s, launch control, Hill Climb 40 s; `[turbo]` lines (full boost, blow-offs), `turbo=peak … full …s blowoffs …` on `[autoplay]`; a screenshot run with `turbo_full_*` and `turbo_blowoff_*` frames.

**Why this** — Brainstorm in `docs/IDEAS.md` (eighteenth session). Seven sessions in a row were the first seconds of a drive and air tricks; the garage had not had a part that changes how the car drives since the Rocket Booster, and Wastes copper bought only the booster and the V8. The roadmap asks for parts tied to each biome, the owner's D21 leans to "crafting and more parts" first, and a turbo is a part you hear and feel in the first second (Need for Speed's spool and blow-off). The previous session was feel, so this one is a feature.

**Verified (CI runs 43271044239 — exit crash in an unrelated test, every scene still ran — and 43271644749, green)**

- **Turbo pickup vs starter, same route, 40 s**: 2302.1 vs 2209.3 tiles (about 23 m further), top 55 vs 49 km/h, fuel 63 vs 70 left; full boost (0.9+) for 22.2 s of the 40, 8 blow-offs; 5 "full boost" events.
- **Hill Climb**: **17.22 s** vs 19.18 s (starter) — 10% faster; rocket car 14.08 s.
- **Launch**: perfect start +1 s 44 km/h / 8.5 m (starter 37 km/h / 7.6 m), +2 s 18.5 m (16.8 m); **20 m sprint 2.13 s** (starter 2.32 s; its second sprint 2.06 s). Plain start (lag): 11.0 m after 2 s vs 10.8 m, sprint 2.93 s vs 2.99 s.
- **Screenshots**: `turbo_full_23.png` (turbo on the bonnet, compressor wheel, the BOOST arc lit orange on the dial, the TURBO! call-out), `turbo_blowoff_73.png` (the puff of air behind the turbo mid-jump), `garage.png` (the Turbo Pickup in the editor, cost "Iron 2, Copper 3").
- **Nothing else changed**: starter 2209.3, left 465.7, spiked 424.9, Crystal Drill 1480.1, rocket 2552.7, all sprints and Hill Climb times identical to main. No errors.

**Not verified / known issues**

- Fun and sound are my judgement (D1). The whistle and blow-off are tuned by numbers only.
- The bot's revs sit near the limiter a lot (the tyres spin), so the turbo is at full boost about half of a drive: the lag only shows at a standing start and after lifting off. It may feel like a flat +40%. Lengthening the spool (or spooling from road speed instead of revs) is the knob.
- The garage stats panel still shows the plain engine torque (48 kNm); the part's info line says "+40% torque … at full boost".
- The Hill Climb scene has no turbo whistle or BOOST arc (physics only).

**Next**

1. Overboost (S, IDEAS #107): hold full boost too long and the turbo overheats — makes lifting off a choice.
2. Start-light gantry (S, #82), Gyro part (S, #94) or the new-part roll-out (S, #112).
3. Crystal Lamp (S, #64), cave bats (S, #110), surface lift (S–M, #68), then the first underground danger (#65 / #56, M).

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
