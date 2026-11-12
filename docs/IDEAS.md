# Ideas

A running list of ways to make GEARHEART more fun. New ideas go under the date they were written. Ranked list at the top (fun gained for the effort); built or rejected ideas stay, marked with the reason.

Sizes: **S** = one session, **M** = two or three sessions, **L** = needs to be split into steps.

## Ranked (best fun for the effort first)

| Rank | Idea | Size | Status |
| --- | --- | --- | --- |
| 1 | Rocket Booster part (Shift for thrust) | S | **Built 2026-11-11** (PR #7) |
| 2 | Stunt callouts: "BIG AIR 1.4 s" toasts for long jumps | S | **Built 2026-11-11** with the booster |
| 3 | Crash juice: screen shake, sparks, dust and a metal crunch on hard hits | S | **Built 2026-11-12** (PR #8) |
| 4 | Buried treasure: glinting stashes underground that pay a bundle of ore | S–M | **Built 2026-11-12** (PR #9) |
| 5 | Fuel cans and scrap heaps lying on the surface between biomes, so the drive is not empty | S | **Built 2026-11-12** (PR #10) |
| 6 | Trip summary card when you get back to the garage: distance, top speed, best air, ore, stashes, heaps smashed, crashes — and your best trip to beat | S | Open (top pick for next session) |
| 7 | Landing grades: "CLEAN LANDING" bonus vs "BELLY FLOP" when you land on the roof, feeding a trip score | S | Open |
| 8 | Smash streak: heaps (and later crates/cacti) smashed within a few seconds of each other chain into "SMASH x3" with rising pitch | S | Open (builds on pickups) |
| 9 | Ore radar / "metal detector" beep that speeds up as you near a stash (car part or player tool) | S | Open |
| 10 | Speed lines and wind sound above 60 km/h | S | Open (fold into the next polish session) |
| 11 | Damage that bites: hard landings dent the car for real, with visible bent panels and smoke from a hurt engine | S–M | Open |
| 12 | Part unlocks per biome: off-road tracks/spiked tyres for snow and ice, need iron from the Frozen North | M | Open |
| 13 | Ramps and natural jumps (cliff lips, dune crests) placed by world generation | S | Open |
| 14 | Jerry-can part: a fuel can block on the car that you can fill at the garage, a lighter alternative to the fuel tank | S | Open |
| 15 | Rare part blueprints inside strongboxes (a part you can only build after finding it) | S–M | Open (needs D19 direction; small version possible now) |
| 16 | Wrecked cars on the surface that you can strip for one random part (a bigger roadside find) | M | Open |
| 17 | Cave critters: a slow glowing beetle that nibbles the car's wheels in deep caves | M | Open |
| 18 | Day and night cycle with the headlight mattering at night | M | Open |
| 19 | Delivery jobs at the garage ("bring 10 copper back": pays a part) for a reason to make a trip | M | Open |
| 20 | Ghost of your best hill-climb run to race against | S–M | Open |
| 21 | Raiders in The Wastes: a bot car that rams you | L | Open (split: dumb rammer → shooting → loot) |
| 22 | Main menu with Continue / New world / Settings (volume, controls) | M | Open |
| 23 | Lava layer deep underground that damages the car | M | Open |

## 2026-11-12 (third session)

Looked at the newest screenshots: `world_280.png` is a lovely sky, a cactus, sand dunes — and nothing to do except hold D. Fuel goes 75 → 58 and never matters, because nothing on the road can give it back and nothing tells you it is low. The drive between biomes has been "the most boring minute" in two brainstorms in a row and is still untouched, while stashes and crashes got their moments. Hill Climb Racing keeps the drive alive with coins and fuel cans every few seconds; Need for Speed with smashable props. That is the cheapest big fix.

- **Roadside pickups** (S): fuel cans (drive through: +18 fuel, glug-and-ding) and scrap heaps (plough through at speed: junk flies, crunch, a little shake, +2–4 Scrap and sometimes Iron/Copper by biome), every 10–19 m, restocking out of sight after 2.5 minutes. Why fun: something to aim for every few seconds, and fuel finally becomes a resource that rewards picking a line, especially with the thirsty booster. *Built.*
- **Low-fuel warning** (S): the status blinks "LOW FUEL" under 20%. Why fun: turns fuel into tension instead of a silent number. *Built with the pickups.*
- **Smash streak** (S): chain heaps and props into "SMASH x3", the crunch rising in pitch. Why fun: a reason to drive fast and straight through junk, a little combo high.
- **Jerry-can part** (S): a small block that adds fuel, lighter than the tank. Why fun: one more build choice tied to the new fuel economy.
- **Roadside wrecks** (M): an abandoned car you strip with the pickaxe for one random part. Why fun: a bigger surface find, and parts you did not plan to build.
- **Trip card with a record to beat** (S): back at the garage, a card for the trip just made, with "best trip" numbers. Why fun: the "one more trip" hook; now there are heaps, cans, stashes, air and crashes to count.
- **Rumble strip of the biome edge** (S): a sign and a short fanfare when you enter The Wastes or the Frozen North for the first time ("THE WASTES — copper country"). Why fun: arriving somewhere new should feel like an event.

## 2026-11-12 (second session)

Looked at the drill screenshots: underground is one flat grey, and every few seconds "+2 Scrap" pops up. Digging is steady income but never a *moment*. The most boring minute is now tunnelling: hold D, watch numbers tick. Nothing says "dig over there".

- **Buried stashes** (S–M): toolboxes, supply crates and strongboxes hidden in the rock, with a twinkle you can see through the dark. Why fun: a visible goal underground and a jackpot moment (fanfare, gold fountain, a fat bundle of ore) — the "one more tunnel" hook. *Built.*
- **Metal detector beep** (S): a ticking that speeds up as you approach a stash, like a Geiger counter. Why fun: tension and a hot/cold game while digging; pure audio, cheap.
- **Strongbox blueprints** (S–M): a few parts only come from strongboxes (e.g. a twin-turbo engine). Why fun: the find changes how the car drives, the best kind of reward.
- **Cave critters** (M): a slow, glowing beetle in deep caves that chews tyres if you sit still. Why fun: first danger underground, small and readable; a stepping stone to enemies.
- **Collapse rumble** (S–M): digging under a big overhang shakes the screen and drops a few loose blocks. Why fun: digging gets a bit of risk and drama without a full physics system.
- **Depth record** (S): "NEW DEPTH RECORD 18 m" call-out and a best depth on the status line. Why fun: a number to beat on every dig, like Hill Climb Racing's distance record.

## 2026-11-12

Looked at the game through the screenshots and bot logs. The most boring minute is still the drive between biomes, but the most *disappointing* moment is a 3-second monster jump that ends with the car landing on its roof in silence: damage 0, no shake, no sound. Crashes are a pillar and currently they are invisible.

- **Crash juice** (S): screen shake scaled to how hard you hit, sparks where metal scrapes, a dust burst in the ground's colour, thud / crunch / snap sounds, "CRASH!" and "LOST A WHEEL!" call-outs. Why fun: every jump and every mistake becomes a moment you feel. *Built.*
- **Landing grades** (S): judge each landing (clean on all wheels, sketchy, belly flop on the roof) and add to a trip score. Why fun: gives the jumps a skill goal, like Hill Climb Racing's flip bonuses.
- **Damage that bites** (S–M): the bot never takes any damage at all, even after 3 s falls. Lower the yield on hard landings so a bad crash costs something (a dent you can see, a wobbly wheel, a smoking engine) and the garage repair matters. Why fun: risk only feels like risk if it can hurt.
- **Trip summary card** (S): back at the garage, show what the trip earned (distance, top speed, best air, ore, crashes). Why fun: the "one more trip" hook; you want to beat your last card.
- **Ghost racer** (S–M): replay your best hill-climb run as a see-through car. Why fun: instant rival, no AI needed.
- **Speed lines and wind sound at high speed** (S): screen-edge streaks and a rising wind whoosh above 60 km/h. Why fun: the booster would feel even faster. (Not ranked yet; fold into a later polish session.)

## 2026-11-11

- **Rocket Booster** (S): a rear part that pushes the car with a flame while you hold Shift. Why fun: instant power fantasy, huge jumps, climbing hills that stopped you, crashes worth watching; gives copper (desert) a use. *Built.*
- **Stunt callouts** (S): time every jump and cheer the long ones on screen. Why fun: rewards risk, turns the rolling hills into a toy. *Built.*
- **Buried treasure** (S–M): rare chests and car wrecks far underground. Why fun: the "big find" moment that makes you dig one more tunnel.
- **Crash juice** (S): camera shake, sparks and a crunch when parts dent or snap. Why fun: crashes are a pillar, but right now they are silent and small on screen. *Built 2026-11-12.*
- **Surface pickups** (S): fuel cans and scrap heaps along the road. Why fun: the most boring minute right now is the 95 m drive between biomes with nothing on it. *Built 2026-11-12 (PR #10).*
- **Biome-tied parts** (M): tracks or spiked tyres that grip ice, made from Frozen North iron. Why fun: a new part that changes how the car drives, and a reason to visit each biome.
- **Garage jobs** (M): simple orders at the garage that pay out a part. Why fun: a short goal gives "one more trip".
