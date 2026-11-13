# Decision log

Newest first. One entry per big decision: what, why, and what would make us revisit it.

## 2026-11-13 — Cave bats need to see you; the echo is not a setting

- **What:** bats roost in groups of 3–6 on Deep cavern ceilings that have at least four rows of open air under them, found from the tiles when the world scene starts (no save change, so dug-out caves change where they are on the next load). They burst when the player (4.5 m) or the car (6 m) is within reach **with at most one tile of rock in between**, when a tile breaks within 3 m (pickaxe or drill), on a honk within 12 m, or when their ceiling is dug away. They are harmless and live for the game session like the birds; a roost comes back 90 s later once you are 28 m+ away.
- **Why line of sight:** without it a drill car would set off colonies in caverns it cannot see, and bats would flit around inside rock. With it, the burst happens when you look into their cavern — the moment it is a surprise and you can see it. Flying bats bounce off cavern walls and fade out after 3–5 s instead of leaving through rock.
- **Echo:** a reverb on the Effects bus (wet up to 0.3, room 0.62, 70 ms predelay), off on the surface, growing from about 1.5 m down and full from about 6 m and anywhere in the Deep. Only effects echo; the engine stays dry so the car does not turn to mush in tunnels. It is not a menu setting (the Effects slider already turns it down with everything else).
- **Revisit when:** bats become a creature that bites (#65); or players find the echo muddy (then lower the wet or make it Deep-only).

## 2026-11-13 — The home arrow is always there when the garage is off screen; the beacon is local

- **What:** the home arrow shows whenever the garage door is off screen and 10 m+ away, not only when a part is ready (the IDEAS #162 version). It is pale by default and only takes colour when it means something: gold for PART READY, orange for LOW FUEL (under a quarter tank; low fuel wins). It sits on the line from the screen centre to the door, inside a box clear of the hint lines (top 128 px) and the bar, card and dashboard (bottom 138 px).
- **Why:** the garage leaves the screen after about 7 m of driving at the drive zoom, and every trip is an out-and-back; a pointer that appears only sometimes teaches nothing. A pale arrow is quiet enough to ignore.
- **Beacon:** a side view cannot show a light "from far away" — at the drive zoom you see about 13–20 m of road — so the searchlight sweeps up to 55 degrees off vertical to reach a little further (about 15 m), and the arrow does the real long-range work. The lights are drawn additively on the glint layer (not darkened by the night tint) plus two real `PointLight2D`s that light the pad and the shed.
- **Revisit when:** the world gets more than one base or a map screen; or playtesters find the arrow nagging (then show it only at night, when ready or on low fuel).

## 2026-11-13 — Esc pauses; settings live apart from the save

- **What:** Esc (and Start on a gamepad) opens a pause menu that stops the whole scene tree (`GetTree().Paused`); the menu runs with `ProcessMode.Always`. It used to save and quit at once. The trip report keeps the first Esc. Volumes (Master, Effects, Engine) and fullscreen are stored in `user://settings.cfg` (a Godot ConfigFile), not in the save, so a save version bump or a new world never resets them.
- **Buses:** the audio buses are created in code at start (`Settings.Apply()` from `Sfx._Ready`) rather than in a `default_bus_layout.tres`, so the layout lives next to the code that uses it and nothing binary is added. One-shots, crickets and dust devils go to `Effects`; engine, drill, boost, wind and turbo loops to `Car` ("Engine" in the menu). Sliders work on loudness (value squared before dB).
- **Not done (yet):** key rebinding, a main menu, a separate music bus (there is no music). The garage keeps Esc as "back to the world".
- **Revisit when:** music arrives (#159: add a Music bus and slider), or the game gets a main menu (move Settings there and keep the pause menu as a short version).

## 2026-11-13 — Lucky strikes: chosen by a position hash, 1 in 16, pays x3

- **What:** an ore tile (scrap, iron, copper, crystal) is lucky when a fixed hash of its tile position is divisible by 16 (`src/World/LuckyOre.cs`). It gleams (gold halo and rim, a shine sweep every 2.2 s, a star) within 27 m, through the rock like a stash glint, and pays 3x its normal drop when broken, by pickaxe or drill.
- **Why a hash and not a new tile type:** nothing to save, no world-generation change and no save version bump; a lucky tile stays lucky across loads and is gone once mined. The census near the garage gives 30 lucky tiles of 437 ore (6.9%), 6 in the top 3 m, the nearest 3.3 m from the pad and 2.3 m down, so a new player finds one in the first minutes.
- **Why 1 in 16 and x3:** rare enough to be a moment (about one per vein-and-a-half), common enough to be seen on every dig. An average ore tile is now worth 1.125x what it was; parts were not repriced.
- **Drill too:** the drill gets the strike (and the extra) like the pickaxe, but the gleam is what makes it a hunt, and that is on foot.
- **Revisit when:** you have played (D1). If the economy feels too fast, lower the rate before the multiplier (the moment matters more than the amount); if it feels random rather than found, make lucky tiles cluster in rich veins.

## 2026-11-13 — Next part card: the game picks your next upgrade, from data

- **What:** a card above the inventory bar shows one upgrade and what it still takes, counted against what the bar shows (ore in the air does not count until it lands). Parts opt in with `goalTier` in `data/parts/parts.json` (1 Drill Head, Off-road Wheel; 2 Rocket Booster, Turbocharger, Spiked Tyre, V8, Hydraulics; 3 Crystal Drill). The cost is the change to the car you drive: wheels as a full set (old tyres refunded), engine and drill as a swap, other parts as one more block. A part the car has, or one beaten by a same-kind part of the same or higher tier, is skipped.
- **Which one:** a ready part first (the highest tier); otherwise the best progress minus 0.2 per tier above 1, so a new player is pointed at the Drill Head and Off-road Wheels before copper parts; the shown part only changes when another is at least 0.1 closer (no flicker).
- **Why:** the ore you carry had no purpose until you opened the garage. Keeping the next upgrade in sight (Hill Climb's upgrade costs, Terraria's crafting list lighting up) turns every ore into progress, and the moment the last one lands is a reason to drive home.
- **Not done yet:** you cannot choose the target (pin a part at the garage, IDEAS #161); the card does not say where the missing ore is (#160).
- **Revisit when:** you have played (D1). If the game's choice feels wrong, pinning comes next; if the card is clutter while driving, it can hide above some speed.

## 2026-11-13 — Hydraulics: a hop on a button, charged by squatting

- **What:** a new block part, Hydraulics (3 Iron + 2 Copper, mass 0.35), placed anywhere in the body. Hold W / Up / gamepad B: the body is pulled down onto its springs (1100 px/s² times the charge) and the charge fills in 0.5 s (`HopChargeTime`). Let go with a wheel on the ground: every node gets the same upward speed, 270 px/s for a tap up to 560 px/s at full charge (`hopTapSpeed` / `hopSpeed` in parts.json), along 75% the car's own up and 25% straight up; any sinking is cancelled first so every hop starts from the same footing. A release in the air is kept for 0.25 s and fires on touchdown. One hop, then 0.7 s recharge; 0.6 fuel per hop. W was free in the car (it is jump on foot), B was free on a gamepad.
- **Why:** every earlier part made the car faster or made it dig; none gave the driver a new move. A hop is felt on the first press, gets the car over steps, ditches and out of holes, and turns a kicker into a bigger jump (the starter car's kicker jumps were too short for a flip). Copper ties it to The Wastes like the booster and turbo.
- **No speed kick for flat hops:** a perfect landing after a hop that was not off a kicker is still graded (PERFECT HOP LANDING) but gets no speed kick and does not count for the trip's perfect landings, or hopping down the road would beat driving it. Flip kicks still count (a flip off a hop is a skill move).
- **Revisit when:** you have played (D1). If it feels floaty or too strong, lower `hopSpeed`; if hops off kickers make every jump trivial, cap the hop's share off a kicker face.
## 2026-11-13 — Ore flies home: the bar shows what has landed, not what you have

- **What:** everything added to the inventory by mining, a stash or a smashed heap flies from where it broke into its slot (`OreFlight`); the slot's number is "what you have minus what is still in the air", so it ticks up as each icon lands (0.5–0.9 s later). The real count changes at once, so saving, building and placing blocks are unaffected.
- **Why:** the bar was the only place your wealth lived and it never moved; collecting needs a picture (Terraria, Hill Climb's coin counter). Counting on landing ties the number to the thing you just watched.
- **Limits, on purpose:** dirt and stone dug by the car's drill do not fly (a tunnel would bury the screen); at most 48 icons are in the air (any more count up at once); a bundle sends at most five icons, the last carrying the rest with an "x3" tag. Dirt and stone flights are smaller, shorter and silent; ore gets a tick (`tally`, pitched up) on landing.
- **Revisit when:** you have played (D1). If it feels busy while digging dirt on foot, make dirt and stone count up without flying too.

## 2026-11-12 — Vein runs: a combo for ore, with a small bonus

- **What:** an ore tile (Scrap, Iron, Copper or Crystal) broken within 3 s (`VeinRun.Window`) of the last one and no more than 3 tiles from it (`VeinRun.Reach`, counted as the larger of the two axis distances) continues a run; anything else starts a new one at x1. Every 4th ore of a run (`BonusEvery`) pays +1 of that ore. Any ore kind continues a run (blobs of different ores often touch). The drill counts too.
- **Why:** mining is the most repeated action and gave almost no feedback; ore already lies in blobs, so the combo rewards doing what a player already does, but with attention (follow the vein, don't wander off). The bonus is small on purpose (about +25% on a long vein, nothing on single tiles) so the economy barely moves; the sound and the sparkle trail are the real reward.
- **Feedback moved off the call-out line:** "+1 Iron" used to replace whatever centre call-out was showing; ore amounts now pop at the tile instead (Crystal keeps its call-out, which says when you have enough for the Crystal Drill).
- **Not saved:** the best run is kept for the session only, so the save format is unchanged.
- **Revisit when:** you have played (D1). If runs feel too strict, raise `Window` or `Reach`; if the bonus feels too generous with the drill, make it pickaxe-only.

## 2026-11-12 — Grappling hook: a free tool on foot, reel-in style

- **What:** E (or middle mouse; the right shoulder on a gamepad, aimed with the left stick) throws a hook toward the cursor. It flies 4 m (`HookRange` 256 px) at 1500 px/s and bites the first solid tile; a miss snaps back. A bite reels the mechanic in at 640 px/s with no gravity, then they **hang** (no gravity, can mine), or, if the tile it bit has three open tiles above it, **climb onto it** (moved by hand over the lip, no collision on the way). Space jumps off (mid reel-in a fling: the rope's sideways speed is kept, plus an upward kick capped at 1.4 jumps), S lets go, E again throws to the next hold while the old one still holds. Mining away the held tile drops you. The camera leans 35% of the way toward the hook.
- **Why:** the on-foot half had no new verb since day one, and climbing out of a deep hole (pillaring blocks or digging stairs) was the dullest minute in the game. Reel-in (Terraria) rather than a swinging rope (Worms) because it is predictable on a tile grid, cannot get stuck in a pendulum, and works with one key; swinging is kept as an idea (#140) for after the owner has tried this.
- **Free, not crafted:** every player has it from the start, so it is felt in the first minute. Whether tools should cost materials is part of D19 (what materials are for); a better hook (longer, faster) would be the natural upgrade if they should.
- **Faster air movement kept after a rope ride:** in the air, above walking speed, the mechanic now slows at 35% of the usual rate, so a fling carries. Nothing could exceed walking speed before, so every older run is unchanged (verified: all 40 s positions identical).
- **Revisit when:** you have played (D1). Too strong (it trivialises digging out) → shorten `HookRange` or slow `PullSpeed` in `Player.Hook.cs`; too weak → the reverse. If it should cost something, see D19.

## 2026-11-12 — Dust devils: the first hazard is weather, and it is also a free jump

- **What:** three stretches of The Wastes road (about 111–176, 178–242 and 244–309 m east of the pad) each grow a dust devil on hot afternoons (heat rises 10:30–11:30, falls 16:30–17:30 on the world clock). A devil wanders its stretch at 0.7–1.5 m/s, lives 25–45 s, then dies down and another forms 6–20 s later. Driving into one grips the car: its velocity is eased towards 14 m/s upwards, sideways towards half its own speed plus half the devil's drift, and its rotation towards 2.4 rad/s; the grip is strongest at the core and low down. After a real ride (held more than 0.12 s, or 1.6 s at most) the devil is spent and blows itself out in a burst of sand. On foot it lifts you and drops you a little way off. The push is a uniform velocity change plus a rigid turn (`SoftVehicle.Whirl`), so the soft body is never bent by it.
- **Why:** nothing in the world could push back; the roadmap's "enemies and danger" was the one direction never started. A creature needs AI, health and art; a whirlwind needs none of that and still gives the road something to react to. It is a hazard (a rough landing or a crash if you let it spin you) and a reward (a 1–2 s jump anywhere, a chance at a flip for the starter car), so it never feels like a pure tax. Spending itself after one throw stops it from juggling the car.
- **Afternoons only:** the game starts at 08:00 and devils need heat, so the first trip is calm, they arrive about a minute in, and every bot run that starts at 08:00 is untouched (all earlier evidence still holds). The midday CI runs use `--time=13`.
- **Not done yet:** no score for a devil ride beyond the jump and landing it causes; no early warning further than the screen and the roar; parked cars are not lifted (only while you drive).
- **Revisit when:** you have played (D1): too strong or too weak is `WhirlLiftSpeed`/`WhirlLiftRate` in `SoftVehicle.Whirl.cs` and `DevilSpin` in `WorldScene.DustDevils.cs`; too many or too few is `Zones`, `Life` and `Rest` in `DustDevils.cs`.

## 2026-11-12 — Birds: life in the world, never a target

- **What:** flocks along the road (`Birds`, every 22–42 m, not on kickers; sparrows in the Home Meadow, crows drawn at 2x in The Wastes, snow buntings in the Frozen North) and three sparrows on the garage roof. They take off when the car comes within 140 px + 0.38 s of travel (about 6 m at 36 km/h, 12 m at 90 km/h), when the player walks within about 1.5 m, on a honk (H, about 12 m), an engine start (about 12 m), a crash (about 9 m) or digging next to them. They come back 75 s later, out of sight; the list is not saved.
- **Why:** nothing in the world moved except you; animals that notice you are the cheapest way to make a world feel alive (Terraria's critters, Minecraft's animals). Scaring them at about half a second of warning means you see them sitting there and then see them go up ahead of the bonnet; earlier (the first try, 9–17 m) they left before they were on screen.
- **Birds always get away:** there is no collision, no damage to birds and no score for them. A kid-friendly game and a cheap system: the car never hits them, the flight simply starts in time. Points for flushing a flock (IDEAS #124) are possible later, but birds must keep escaping.
- **Horn:** H (Y on a gamepad), the same two-tone horn pitched by the engine (the Scrap 4 beeps higher, the V8 lower). It has no other effect yet.
- **Revisit when:** you have played (D1): if birds feel too busy, raise the step between flocks in `Birds.Place`; if they go too early or too late, the knob is the radius in `WorldScene.UpdateBirds`.

## 2026-11-12 — Day and night, step 1: a seven-minute day, looks and light only

- **What:** a world clock (`DayCycle`, saved as `ClockHours` and `Day` in `progress.json`; a new game starts at 08:00). Daytime 06:30–18:00 runs at 13 game hours per 270 s; dusk (18:00–20:30) and dawn (04:00–06:30) ease the light; the night hours 21:00–04:00 run twice as fast. A full day is about 7 minutes of play, a night about 1¼ minutes plus dusk and dawn. The sky (gradient, sunset glow, sun and moon on an arc, stars, tinted clouds, hill silhouettes) and a `CanvasModulate` tint over the world (warm at sunset, moonlit blue at night, mixed with the existing underground darkness) follow the clock. The car gets headlights (a cone `PointLight2D` out of the frontmost block), tail-lights (brighter when braking) and a reversing lamp (cars face right and back up to go west), plus a haze along each beam and lamp flares drawn over the darkness. Lights come on by themselves at dusk and deep underground. Crickets chirp at night on the surface. Buried stashes glint from 1.8x further at night. The status line shows the time.
- **Why:** nothing in the world changed while you played; the roadmap's day and night had never been started. A sunset arriving mid-drive and headlights in the dark are big, cheap "the world is alive" moments, and the night is where later danger (raiders, critters) can live. The night glints give the dark a small reward now, so it is not just "harder to see".
- **Not gameplay (yet):** the clock does not touch physics, fuel, spawns or scoring; the 40 s bot positions are identical to main. No save version bump: old `progress.json` files just start at 08:00 (missing fields take their defaults) and world generation is unchanged.
- **Kept the night short on purpose:** a night that is too long or too dark makes people quit or wait. If testers dislike it, the knobs are `DayCycle.NightSpeedUp`, `DayCycle.Tint` (the night colour) and `HeadlightReach`. A "sleep till dawn" at the garage (IDEAS #120) is the escape hatch if needed.
- **Revisit when:** you have played a full day (D1), or when night danger is added (then night may need to pay more, IDEAS #116).

## 2026-11-12 — Turbocharger: the first part that reads the gearbox revs

- **What:** a new block, the Turbocharger (`turbo`, category Turbo, 2 Iron + 3 Copper, mass 0.4). Boost pressure (0..1) builds while the throttle is held past half, at a rate set by the pretend gearbox's revs: nothing below 0.3 of the redline, the full rate (1 / 0.9 s) from 0.8 up. It bleeds at 2.6/s off the throttle, drops 0.25 on each upshift, and lifting off with 0.45+ boost blows off (sound and a puff). At full boost the engine has +40% torque and +12% top spin, and burns 30% more fuel. Only the best turbo on a car counts. The bot design `--design=turbo` is the starter pickup with the turbo on top of the V6.
- **Why:** each biome should feed a part that changes how the car drives (roadmap), and Wastes copper only bought the booster and the V8. A turbo is the classic "new part, new car" moment, you hear it before you feel it, and its lag makes launch control matter more: armed launches hold the revs high, so the turbo spools on the line.
- **Gearbox no longer show-only:** the gearbox was sound and dashboard only. The turbo is the first thing that reads its revs for physics. The gearbox does not read the turbo back, so there is no loop; but changing the gearbox's shift points now changes how fast the turbo spools.
- **Revisit when:** you have driven it (D1). If it feels like a flat +40% (the revs sit high most of the time, so it is at full boost about half of a drive), lengthen `spoolTime` or raise `TurboSpoolFromRpm`; if it is too strong for 3 Copper, lower `turboTorque` in `data/parts/parts.json`.

## 2026-11-12 — World generation disposes its noise and random objects

- **What:** `FastNoiseLite` and `RandomNumberGenerator` objects made during world generation (and for stash loot, heap loot, the test terrain and ore boulders) are now `using var`, so they are freed at once instead of by the .NET garbage collector.
- **Why:** two CI runs in a row (PR #25's docs run and this PR's first run) crashed with a segmentation fault *after* a test had finished, right after "Leaked unsafe reference" errors for exactly these objects: the finalizer freed them while Godot was shutting down.
- **Revisit when:** the exit crash comes back with other object types in the leak list.

## 2026-11-12 — Air cam: the camera looks where you are going and frames the landing

- **What:** while driving, the camera aims ahead of the car by 0.22 s of travel (at most 220 px, about 3.4 m), plus the distance the Camera2D's own smoothing would otherwise trail behind (speed / smoothing speed), so the car sits left of centre driving right and the road ahead gets the room. On a jump, a plain ballistic arc (gravity only, from the body centre 44 px down, against the tiles and kicker ramps) predicts the landing; if the whole flight is predicted to last 0.7 s or more, the camera eases (over about a third of a second) toward the midpoint of car and landing and zooms out until both fit with a margin, but never below 0.85 zoom. A soft dark shadow sits on the ground at the predicted landing. On touchdown it eases back in. The car is always clamped to stay on screen.
- **Why:** at 1280x720 and zoom 1.1–1.5 a big jump hid the landing, and since flip tricks the player is spinning blind. Chase cams in racing games and Trials look ahead; the shadow is the platformer trick for judging where you come down. The prediction ignores the booster and air drag on purpose: it is a hint, and it updates every frame.
- **Not gameplay:** nothing in physics or the bots reads the camera, so playtest numbers are unchanged (the starter's 40 s position matched main to 0.1 px).
- **Revisit when:** you have played (D1). If the look-ahead feels seasick or too far, lower `AirCam.LeadPerSpeed`; if the zoom-out feels too much, raise `AirZoomMin` or `BigAir`. If the shadow makes landings too easy, drop it.

## 2026-11-12 — Flip tricks, and spin that belongs to the driver

- **What:** while every wheel is off the ground the car's rotation is added up. A full turn (less 60°, so a landing onto a slope counts) that ends with the car judged on its wheels is a **BACKFLIP** (the end you drive towards goes up and over) or **FRONTFLIP**, DOUBLE / TRIPLE for more. It pays a speed kick of 150 px/s (about 8 km/h) per turn, up to three, on top of a perfect-landing kick, and 60 points per turn on the trip report's Jumps row. Ending on the roof, nose or tail, or wheels first but with the body slamming down more than 35° off the slope, does not count ("…, but it doesn't count"). A bounce while the landing is still being judged adds to the same jump's rotation.
- **Air handling changed:** with every wheel off the ground, Q/E rotate 1.5x as hard (9 → 13.5 rad/s²), rising to 2.3x after holding the same key for 0.15–0.45 s; and spin in the direction you are holding is no longer bled by the in-air spin damping (2.9/s) until it reaches 690°/s. Taps stay close to the old feel for lining up a landing; a held key commits to a flip. Released spin is damped as before, so crashes and bounces still do not turn into endless tumbles.
- **Why:** the first two CI runs showed the old air control could not flip the car at all: the damping capped spin at about 270°/s (13.5 rad/s²) to 410°/s (with the hold boost), so even 1.5 s of air turned the car only 316–334°. Hill Climb Racing and Trials hang their best moments on flips; a trick you can only do with a rocket and luck is not a trick.
- **Revisit when:** you have played (D1). If landings feel twitchy, lower `AirFlipBoost`; if flips are too easy with the starter car, raise `MaxHeldSpin` damping or `FlipSlack`. Constants: `SoftVehicle.AirFlipBoost/AirHoldBoost/AirHoldDelay/AirHoldRamp/MaxHeldSpin`, `WorldScene.Flips.cs` `FlipSlack/FlipKickPerTurn`, `Trip.PerFlip`.

## 2026-11-12 — Rock it free: a stuck car is a minigame, not a walk home

- **What:** a car whose driver holds the throttle (above 30%, no handbrake, not drilling, not on its roof, a wheel on the ground) for 1 s without the body moving faster than about 1 km/h is **STUCK**. While stuck, Q/E tilt on the ground is 2.5x stronger, and every fresh tap of Q or E with a wheel on the ground gives a *rock*: a hop (110 px/s, +40 per step of the chain, up to 270), a shove the way the throttle points (60–140 px/s, 3–8 km/h) and a twist the way you tapped. Alternating taps within 0.9 s of each other build a chain up to x5. Moving 1.5 m sideways from where it got stuck at more than about 3.6 km/h is **ROCKED FREE!**; letting go of everything for 1.5 s, flipping or parking ends it quietly.
- **Why:** being wedged (a notch, a step, a tunnel lip) was the dullest moment in the game: full throttle, nothing moving, no hint, walk home. Rocking a stuck car is what people do in real life and what off-road games (Spintires, MudRunner) make into a skill. Rhythm builds the chain, so frantic mashing of one key is weaker than a steady Q-E-Q-E. The hop only comes off the ground, because in the first CI run taps in the air stacked and flew the rocket car 13 m up.
- **Taps in the air** are kept for 0.4 s and fire on touchdown, so a steady rhythm survives the bounce but hops never stack.
- **Measured (CI run 43242512714):** a 2-tile (0.5 m) trench past the pad: rocking gets the starter pickup out 4.7 s after it got stuck (12 rocks); the control run without rocking (backing up and charging again) is still in the trench after 20 s. A 3-tile (0.75 m) wall is out of reach with or without rocking (by design: a wall taller than the car's wheels needs the drill or a shovel). The Crystal Drill car's wedge on the bot route: free after 1.2 s (3 rocks).
- **Revisit when:** you have played (D1). The constants are at the top of the "Rock it free" block in `SoftVehicle`. If it feels like a cheat for climbing walls, lower the top hop or require the chain to build; if nobody finds it, the STUCK call-out is the place to make it louder.

## 2026-11-12 — Gate sprint: 20 m from any standing start on the pad

- **What:** every time the car pulls away from a standstill on the garage pad (anywhere on it), the run to 20 m sideways from that spot is timed, in either direction. The clock starts at the last moment the car was below 0.5 km/h, so time spent charging a launch does not count. It is cancelled by stopping on the pad again, getting out, or taking more than 12 s. The best time is kept in progress.json.
- **Why:** a fixed distance from wherever you stand is fair without a start line to park on, and 20 m takes 2–3 s with the starter car, short enough to retry at once. Measuring to the trip gate (18 m from the pad's middle) instead would have made the time depend on where you parked.
- **Measured (CI):** starter V6 perfect launch 2.32 s and 2.06 s, plain or bogged 2.99 s; rocket 1.76 s, V8 2.77 s, Scrap 4 3.85 s.
- **Revisit when:** you have played (D1). `GateSprint.Distance` sets the length; a start-light gantry (IDEAS #82) could give the start a visible line and a reaction time.

## 2026-11-12 — Launch control: a timing window, not a reaction test

- **What:** at a standstill (under about 3 km/h, a wheel on the ground), holding the handbrake and the throttle arms launch control. A charge climbs from idle to the top in 1.1 s and is shown on the rev bar (the gear box reads "L", then a green "GO"). Letting go of the handbrake with the throttle still down grades the start by the charge: 0.75–0.97 **PERFECT START** (+18 km/h shove along the body, exhaust pop, cheer), 0.45–0.75 **GOOD START** (+8 km/h), lower **BOGGED DOWN** (nothing extra). Holding past the top bounces off the limiter back to 0.6 and climbs again, so a missed window is retried by holding on, not by starting over. Letting go of the throttle first cancels it.
- **Why:** the first second of every trip was the dullest; a rising bar with a window is readable at a glance and rewards rhythm, not reflexes (the window is about 0.24 s wide and comes round every 0.44 s while you hold). The shove uses the same overdrive trick as a perfect landing, so the engine keeps pushing past its usual top for a moment.
- **Measured (CI):** starter V6 on the pad, 2 s after letting go: perfect 19.0 m / 42 km/h, bogged 14.1 m / 35 km/h, plain start 14.4 m / 39 km/h; 0.5 s after: 32 vs 14–16 km/h.
- **Revisit when:** you have played (D1): the window width (`LaunchPerfectFrom/To`), the shove (`LaunchPerfectKick`) and the rise time are constants at the top of `SoftVehicle`. If perfect starts feel mandatory, shrink the shove; if nobody finds the feature, add a first-time toast at the garage.

## 2026-11-12 — Found: the car physics loses small forces far from x = 0 (float precision)

- **What:** node positions are stored as absolute world pixels in 32-bit floats. The garage stands at about x = 20,600 px, where a float can only step 0.002 px; the Verlet integrator keeps velocity as the difference of two positions, so any change smaller than about 1.9 px/s per substep is rounded away. Reproduced offline with a Python port of the solver: at x = 20,500 the Scrap 4-Cylinder pickup sits at −8.9 px/s with full throttle and the V8 pickup is stuck at 82 px/s (4.6 km/h); at x = 500 both pull away normally. The CI log matches (Scrap 4: −8 px/s creep, wheels grounded, no slip; V8: 6 km/h for over 2 s), and in the flat Free Drive scene near x = 0 the Scrap 4 pickup reaches 20 km/h in 2 s.
- **Consequence:** every car's handling in the world is subtly off from what the formulas say, and it gets coarser further east (above x = 32,768 the step doubles). Parked cars creep by about 8 px/s.
- **Fix (PR #21, open at the end of the session):** each car keeps its nodes relative to a local origin (a multiple of 1,024 px along x, moved as the car drives) and adds it only when talking to the world (terrain through `ShiftedTerrain`, crashes, tyre contacts, bounds, other cars). The car node sits at its origin, so drawing needs no conversion. Chosen over doubles (Godot's C# `Vector2` is float, so doubles would mean rewriting the solver) and over moving the whole world (the far ends would still be 20,000 px out).
- **Revisit when:** a second car or loose objects show up far from x = 0 in the world (their contacts go through the frame conversion in `SoftPhysicsWorld`, tested only by the build so far).

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
