# Art

All current art is **placeholder pixel art drawn by code** (`python3 tools/make_art.py`). Replace any PNG in `assets/art/` with real art and the game picks it up, as long as the grid layout stays the same.

| File | Grid | Contents |
| --- | --- | --- |
| `tiles.png` | 16 x 16 px cells, 8 columns | Row 0 dirt (4 variants) · row 1 stone (4) · row 2 grass-topped dirt (4) · row 3 scrap ore (2), iron ore (2), copper ore (2), bedrock (2) · row 4 crack stages (4), dirt back wall (2), stone back wall (2) · row 5 planks (2), sand (2), sandstone (2), snow (2) · row 6 item icons: dirt, stone, scrap, iron, copper, then ice (2), then the crystal icon · row 7 buried stashes: old toolbox (2), supply crate (2), strongbox (2), each on a dark rock pocket · row 8 slate (4), glow crystals on slate (4) · row 9 slate back wall (2), two empty cells, then the same four crystal clusters on a clear background (drawn by the game over the underground darkness so they shine; keep their shapes in the same place as in row 8) |
| `player.png` | 32 x 48 px frames, facing right | 0 idle · 1–4 walk · 5 jump · 6–7 mining swing |
| `parts.png` | 22 x 22 px cells | In order of the `sprite` number in `data/parts/parts.json`: light frame, body panel, heavy frame, three engines, armor, bumper, fuel tank, drill block, cab, rocket booster (the nozzle and flame are drawn by the game), Crystal Drill block (its cyan cone is drawn by the game) |
| `wheels.png` | 56 x 56 px cells | Small (radius 16), standard (20), off-road (26), spiked winter tyre (20, studs poke out about 4 px), centred in the cell |
| `garage.png` | 112 x 72 px | The garage shed, drawn at 2x |
| `pickups.png` | 32 x 32 px cells, art standing on the bottom edge | Fuel can · scrap heap (meadow) · rusty scrap heap (The Wastes) · snowy scrap heap (Frozen North) |
| `decor.png` | 48 x 64 px cells, art standing on the bottom edge | Oak, snowy pine, cactus, bush, flowers, grass tuft, desert rock, snowy rock, kicker warning sign (right, left), long-jump record flag (flying right, left; the pole is at the cell's centre) |

Sounds are in `assets/audio/` (22.05 kHz mono WAV, made by `python3 tools/make_sounds.py`): `engine`, `drill` and `boost` are one-second loops; `pick`, `break`, `place`, `pickup`, `thud`, `crunch`, `snap` `treasure` (stash fanfare), `fuel` (fuel can glug and ding), `tally` (trip-report tick) and `record` (new best trip fanfare) are one-shots. Replace any file with a real recording of the same name.

## Using a free pack (Kenney and similar)

Packs marked CC0 are free for commercial use. Their sheets use their own layouts, so the simplest route is to copy the tiles you like into the grid above with any pixel editor (Aseprite, LibreSprite, Piskel). World tiles must be 16 x 16.

Car bodies bend with the physics, so each part is drawn as a flat square that tiles with its neighbours. Avoid art that sticks out of its cell.
