# GEARHEART

*Dig it, build it, race it, live it.*

A 2D side-view sandbox where you mine, craft and build vehicles part by part, then race, fight and explore across several worlds. Built with Godot 4 (.NET / C#).

**Status:** Phase 1 — Prototype. You play a mechanic in a destructible tile world: walk, jump and mine by hand, collect Scrap, Iron and Copper, build and upgrade your car at the garage, then get in and drive it. A drill on the car tunnels through the ground.

**Decisions waiting on you:** [docs/OPEN_DECISIONS.md](docs/OPEN_DECISIONS.md) · **Art layout:** [docs/ART.md](docs/ART.md)

## Setup

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer.
2. Install **Godot 4.6.x .NET** from [godotengine.org/download](https://godotengine.org/download).
3. Clone this repo, open Godot, click **Import**, and select `project.godot`.
4. Press **F5** (Play). The first run restores NuGet packages and builds the C# code, which takes about a minute.

## How to play

**On foot**

| Input | Action |
| --- | --- |
| A / D | Walk (one-tile steps are climbed automatically) |
| Space / W | Jump |
| Hold left mouse | Mine the highlighted tile (within reach) |
| Right mouse | Place a block (dirt or stone) |
| E / middle mouse | Grappling hook toward the cursor (4 m): it bites rock and reels you in; you hang there (and can mine), or climb onto a ledge it caught. Space jumps off (mid reel-in: a fling), S lets go, E again throws to the next hold. Gamepad: right shoulder, aimed with the left stick |
| 1 / 2 | Choose dirt or stone |
| F | Get in the car (when next to it) |
| G | Open the garage (when next to it) |
| Esc (Start on a gamepad) | Pause menu: Resume, volume sliders (Master, Effects, Engine), fullscreen, the full key list, Save and quit. Esc again resumes. Volumes are kept in `user://settings.cfg`, apart from the save |

**Driving**

| Input | Action |
| --- | --- |
| A / D | Drive |
| Q / E | Tilt (air control, or to point the drill down) |
| H | Horn (Y on a gamepad): scares birds off the road |
| Space | Handbrake (hold it with D/A at a standstill for launch control) |
| Shift | Rocket boost (needs a Rocket Booster part; burns fuel fast) |
| W / Up | Hop (needs Hydraulics; B on a gamepad): hold to squat, let go to hop — the longer the squat, the higher the hop |
| X | Drill on / off |
| F | Get out |
| R | Repair and refuel (at the garage) |

**The loop:** mine ore by hand → open the garage → spend materials on parts (a Drill Head costs 4 Scrap and 2 Iron) → drive further and dig deeper for Iron and Copper. Scrap is shallow, Iron starts about 2 m down, Copper about 5 m down. It gets dark underground; you carry a lamp.

**Engines you can hear:** every engine part has its own sound — the Scrap 4-Cylinder buzzes and rattles, the V6 hums, the V8 burbles — and a pretend gearbox (4, 5 or 6 gears) makes the note climb, drop at each shift and climb again. The dashboard shows the gear over a rev bar that blinks red on the limiter (free-spinning wheels in the air hit it). Floor it through a shift or lift off at high revs and the exhaust pops a flame; getting in cranks the starter. The gearbox is sound and show only: it does not change how the car drives.

**Launch control:** stopped? Hold Space and D (or A): the revs climb up the rev bar and the gear box shows "L". Let go of Space when the bar reaches the green window at the top ("GO") for a **PERFECT START** (+18 km/h and an exhaust pop); a bit early is a GOOD START (+8 km/h), far too early is BOGGED DOWN. Hold on past the top and the revs bounce off the limiter and climb again, so you can wait for the next window.

**Stuck?** If you hold the throttle and the car won't move (wedged in a notch, nose against a step), the game says **STUCK!**. Tap Q, E, Q, E with the throttle held: each tap bounces the car on its springs and shoves it forward, and taps in a steady rhythm bounce it higher (ROCK IT! x2 … x5) until it's **ROCKED FREE!**

**Gate sprint:** every standing start on the garage pad is timed over 20 m, like a drag strip. The clock starts the moment the car leaves the line (the launch charge doesn't count) and a ticking SPRINT timer shows top right; at 20 m a call-out gives your time and whether it beat your best ("20 m SPRINT 2.91 s · NEW BEST! 0.18 s faster"). Your best sprint shows on the status line while you stand on the pad. A perfect start is worth a few tenths.

**Rocket Booster:** 2 Scrap, 2 Iron and 3 Copper buy a booster for the back of the car. Hold Shift (or RB / X on a gamepad) for thrust: long jumps, steep hills and spectacular crashes. Long jumps get a "BIG AIR" call-out with the airtime, and hard landings and crashes shake the screen and throw sparks.

**Next part:** a small card above the inventory bar always shows the upgrade you are closest to building and what it still takes ("Iron 3/4"); the counts tick up as mined ore lands. When the last piece lands the card turns gold, a chime rings and the game tells you to head to the garage.

**Hydraulics:** 3 Iron and 2 Copper buy a lowrider kit; bolt it anywhere in the body. Hold W (Up, or B on a gamepad): the car squats on its springs and a bar over the block fills; let go and it hops — a tap is a little bunny hop (about half a metre), a full half-second squat over 2 m. Hop a ditch or a rock, hop out of a hole, or squat on a kicker's run-up and let go at the lip for extra air (and room for a flip). Squat in the air and let go as you touch down to bounce. One hop per touchdown, a short recharge, a splash of fuel each; a perfect landing after a hop on the flat gets no speed kick.

**Turbocharger:** 2 Iron and 3 Copper buy a turbo; bolt it on top of the engine. Floor it and the boost builds (slowly at low revs, fast near the redline): you hear a whistle that climbs with it, the compressor wheel on the bonnet spins up and a BOOST arc fills on the speedometer. At full boost the engine has up to 40% more torque and a higher top speed (and drinks a little more fuel). Lift off and it blows off with a "pssh" and a puff of air; every upshift costs a little boost. Holding launch control on the line spools it up before you go.

**Birds:** sparrows sit on the garage roof and peck along the Home Meadow road, big crows wait in The Wastes and snow buntings in the Frozen North. Start the engine and the roof sparrows flutter off; drive at a flock and it takes off ahead of your bonnet (from further away the faster you go), with a chirp or a caw. Honk (H) to clear them from about 12 m. They come back after a while, and at night they sleep.

**Cave bats and echo:** small colonies of bats hang from the cavern ceilings of the Deep. Come into sight of them (about 3.5 m on foot, 5 m in the car, further when they look down at you, and only once they are across your screen: you see them hanging, or see them dive down from a high ceiling), dig into their cavern or honk, and they drop off the ceiling and burst out squeaking, flitting round the cavern before they vanish into the dark; they come back later. Underground the effects (pickaxe, breaks, crystals, squeaks) get a cave echo that grows with depth.

**Roadside plants:** bushes, flowers and grass in the meadow, cacti in The Wastes and snowy shrubs in the Frozen North burst into leaves, petals or snow when the car runs them over (a cactus thumps the car a little), and leave a stump or stubble that grows back a couple of minutes later. Keep mowing and the game counts: MOWER x3, x5, x10 …

**Speed traps:** camera posts stand on flat stretches of road about 40, 125, 210 and 295 m out on each side of the garage. Drive past one and it flashes and reads your speed over the car; the blue sign under the camera keeps your best through that trap. Beat it for NEW BEST (the sign turns gold).

**Dust devils:** on hot afternoons (from about 10:30 to 17:30 on the clock) whirlwinds of sand form in The Wastes and wander up and down the road; you can see their columns from far off. Drive into one and it grabs the car: lifts it a couple of metres, slows it and twists it, then throws it out and blows itself out in a burst of sand. Fight the spin with Q/E and land flat for a perfect landing, or hold the spin and try to ride it into a flip. Or wait for it to wander off or die down. On foot one picks you up and drops you a little way off.

**Buried stashes:** something glints in the rock? Dig towards it. Old Toolboxes lie shallow, Lost Supply Crates deeper, and Ancient Strongboxes (a pile of Iron and Copper) deep down or out in the far biomes. The pickaxe and the drill both open them. There is one a short walk from the garage on each side.

**Lucky strikes:** about one ore tile in sixteen is lucky: it gleams gold (a gold rim, a shine sweeping across it, a twinkling star) and you can see it through the rock from about 15 m. Mine it, by hand or with the drill, for a **LUCKY STRIKE**: it pays triple, with a gold ring, a fountain of sparks and a jingle.

**On the road:** fuel cans and scrap heaps lie along the surface every 10–19 m. Drive through a can for +18 fuel (it stays put if your tank is nearly full). Plough through a scrap heap at speed to smash it for Scrap (sometimes Iron in the meadow and the north, Copper in The Wastes), or walk into it to pick it up. Both come back a couple of minutes after you take them. The fuel readout blinks LOW FUEL under 20%.

**Trips:** leave the garage grounds (about 18 m out) and a trip starts; the status line shows how far out you are and your farthest ever ("Trip 84 m (best 212 m)"), with a "NEW FARTHEST!" cheer when you pass it. Come back to the garage and a **trip report** ticks in: farthest, top speed, jumps (longest air and good landings), scrap heaps and best streak, fuel cans, stashes, haul and carnage, each with points, then the trip score and your best trip to beat (a fanfare and NEW BEST tags when you beat it). Enter or Esc closes it.

**Flips:** in the air, hold Q or E to spin the car right round. Each full turn ticks ("FULL TURN! now land it"); come down on your wheels and it's a **BACKFLIP!** or **FRONTFLIP!** (DOUBLE, TRIPLE …) with a speed kick of about 8 km/h per turn on top of any perfect-landing kick, and flips score on the trip report's "Jumps" row. Land on the roof, nose or tail and the flip doesn't count. With all wheels off the ground the car turns 1.5x harder, more if you keep holding the key, and held spin is not slowed down: tap Q/E to line up a landing, hold to flip. A backflip needs a big jump off a kicker (about 1 s of air or more; easiest with a Rocket Booster).

**Camera:** while driving, the camera looks ahead the way you are going. On a big jump it eases out so you can see where you will land, and a shadow on the ground marks the spot.

**Day and night:** the clock runs while you play (the time is on the status line): a day lasts about seven minutes, a new game starts at 08:00. At sunset the sky turns orange and pink, then the stars and moon come out and the world goes a moonlit blue. The car's headlights switch on by themselves (and a reversing lamp when you back up to go west), crickets chirp, and **buried stashes glint from much further away at night**, so a night drive is a good time to hunt for treasure. Lights also come on deep underground.

**The way home:** when the garage is off screen, a small arrow at the edge of the screen points to it with the distance ("42 m"). It turns gold with PART READY when you have the ore for an upgrade, and orange with LOW FUEL when the tank drops below a quarter (R at the garage refuels). After sunset the garage lights up: a lamp post on the pad, the shed lit warm inside and a searchlight on the roof sweeping the sky.

**Kickers:** a yellow sign with a ramp on it means a jump kicker ahead: a steep dirt ramp (a dune lip in The Wastes, a snow cornice in the Frozen North) with a sharp crest. Hit it fast, keep the nose level with Q/E in the air and land flat for a PERFECT LANDING. The first one is just past the garage gate on each side. Every kicker keeps your longest jump: a chequered flag stands where you came down, with the distance over it, so on the next run you can see the mark to clear ("LONG JUMP 7.4 m · NEW RECORD! (+1.2 m)"). A roof landing or nose dive is measured but doesn't count.

**Trip goals:** every trip has one bonus goal ("Go 50 m out", "Reach the Frozen North", "Smash 4 scrap heaps", "Fly 1.0 s in one jump", "Dig up a buried stash"...). It shows in gold under the status line before you leave, tracks your progress on the way, and pays 100–200 bonus points on the trip report when done. Distance and speed goals grow with your records.

**Landings:** every jump of 0.6 s or more is judged as you touch down. Land on all wheels at once with the car matching the slope (tilt with Q/E in the air) for a **PERFECT LANDING**: a burst of speed, warm streaks and a ta-DING. All wheels down without the body touching is a **CLEAN LANDING**. Land on the roof, nose or tail and the game names it (ROOF LANDING, NOSE DIVE, TAIL SLAM, BELLY FLOP). Perfect and clean landings score on the trip report's "Jumps" row.

**Spiked Tyres:** 2 Scrap and 4 Iron buy studded wheels. On snow and ice they keep nearly full grip, where plain tyres spin and slide (and spray ice); on bare ground they grip a little less than standard wheels. The first time your plain tyres spin on ice the game tells you about them, and the first time the spikes bite it cheers.

**Biomes:** the garage stands in the **Home Meadow**. Drive about 95 m east to reach **The Wastes** (sand dunes, sandstone, copper close to the surface) or west to the **Frozen North** (snow, rugged hills, more caves, lots of iron, and ice patches with almost no grip, also on the hills).

The world, your materials and your car are saved automatically (`user://world.bin`, `progress.json`, `designs/current.json`). Start the game with `-- --fresh` to begin again.

## Garage

- Pick a part on the right, then **left click or drag** on the grid to place it. **Right click or drag** removes.
- Wheels go in the cell **directly under a block**. Choose their suspension (Soft or Sport) before placing.
- A design needs a cab, an engine and a wheel, and all blocks must touch.
- **Build & Drive** pays the material cost (removed parts are refunded) and returns to the world. **Hill Climb** is a separate timed race track.
- Parts live in `data/parts/parts.json`: change numbers there to rebalance without touching code.

## Automated playtests

Every build runs the game headless at a fixed 120 FPS and fails on any error: a bot walks, mines and places blocks in the world, then drives the car (once with the starter pickup, once tunnelling with a drill car); plus the garage, the physics lab, the Hill Climb and the old boulder test. The build also renders real screenshots with software graphics and pushes them to the `screenshots` branch. Progress lines (`[autoplay]`, `[race]`, `[mining]`, `[stunt]`, `[crash]`, `[stash]`, `[pickup]`, `[trip]`, `[ice]`, `[landing]`, `[kicker]`, `[engine]` (starter, shifts, exhaust pops; `--design=v8` and `--design=scrap4` swap the starter's engine), `[launch]` (launch-control starts and plain starts: speed and distance 0.5, 1 and 2 s after; `--launchtest` gets straight in and lets go in the green after one limiter bounce, `--launchearly` lets go far too early), `[stall]` (per-wheel numbers when a car does not move off after getting in), `[sprint]` (20 m gate sprints from the pad: time, launch grade, best before; `--launchtest` reverses back onto the pad for a second sprint), `[flip]` (each full turn in the air, landed or crashed flips; `--fliptest` drives east and tries a backflip, then a frontflip, … off every kicker), `[aircam]` (look-ahead while driving; on each big jump the predicted landing, how far off it was, the widest zoom and any frames with the car off screen), `[rock]` (stuck, every rock, rocked free; `--rocktest` digs a trench past the pad with a wall too tall to drive up, `--rockdepth=N` sets its depth in tiles, `--norock` is the control run without rocking); `--crashtest` drops the car from 15 m, `--stashtest` buries stashes under the bot's pickaxe, `--triptest` makes two out-and-back drives for the trip report, `--icetest` lays 40 m of ice by the garage and drives across it, for plain and spiked tyres; `--time=H` starts the clock at hour H and `--clockrate=N` runs it N times as fast, with `[night]` lines for the light, headlights, glints and crickets; `--jumptest` plants long-jump record flags before driving: one to beat, the rest out of reach; `[trap]` lines list the speed-trap posts and every flash (speed, best before and after) — `--traptest` plants a slow best on the first trap east and an unbeatable one on the second; `[devil]` lines show dust devils forming, dying down, catching the car and setting it down again — run with `--time=13` to drive at midday; `[home]` lines show the home arrow appearing, its mode, distance and direction, and the garage lights switching on (`--lowfuel` starts the car with 30% fuel); `[hook]` lines show every grappling-hook throw, bite, hang, ledge climb and miss — `--hooktest` digs a 12 m shaft by the garage and climbs out of it with the hook alone) appear in the build log. Run one yourself:

```
godot --headless --path . --fixed-fps 120 res://scenes/world.tscn --quit-after 4800 -- --fresh --autoplay
```

## Workflow

- `main` always builds and runs. New work happens on a branch and is merged through a pull request.
- Every push to `main` runs **GitHub Actions**, which exports Windows and Linux builds (see the Actions tab, then *Artifacts*).
- Big decisions go in [`docs/DECISIONS.md`](docs/DECISIONS.md).
