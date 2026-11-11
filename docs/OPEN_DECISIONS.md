# Decisions needed from you

Everything here waits on you. Each item lists the options, my recommendation and the date it's needed by. Reply in chat (for example "D3: B") or edit this file. When an item is decided, it moves to [DECISIONS.md](DECISIONS.md).

Last updated: 2026-11-11

## Needed now (this week)

| # | Decision | Options | My recommendation | Why it matters |
| --- | --- | --- | --- | --- |
| D1 | **Play the new build** | Walk, mine, build a drill at the garage, drive and tunnel. Tell me what feels good and bad | — | This is the game's new direction; your reaction decides what I polish next |
| D20 | **Art pack** | A) keep my generated pixel art for now · B) you download a free CC0 pack (for example Kenney "Pixel Platformer") and attach the zip here, and I fit it into the layout in `docs/ART.md` | A now, B or an artist at the vertical slice | I can't download files from this workspace, so a pack has to come from you |
| D2 | **Hours per week** | A) about 20 h part-time · B) full-time · C) less than 10 h | A to start | Every date in the roadmap assumes A. B makes it about 40% faster, C about twice as slow |
| D21 | **What comes next** | A) crafting stations and more parts (new engines, tracks, armor, boosters per biome) · B) enemies and danger (raiders in The Wastes, cave creatures) · C) day and night, weather · D) music and better sounds | A, then B | Biomes, a graphics pass and first sounds are done; this decides the next block of work |
| D3 | **Garage design approach** | A) side-view grid of 1x1 blocks (built now) · B) free placement with snapping | A | A is simple, readable and easy to make strong. B looks fancier but is much more work |

## Needed in the next 4 weeks (by mid-December 2026)

| # | Decision | Options | My recommendation | Why it matters |
| --- | --- | --- | --- | --- |
| D4 | **Final game title** | Keep "GEARHEART" if the trademark search is clear, or pick from a shortlist | Search the EU (EUIPO) and US (USPTO) trademark databases first; keep 3 backup names | Needed before the Steam page, domain, social handles and logo |
| D5 | **Fuel in the prototype** | A) keep fuel (built now: a small built-in tank, extra tank parts add more) · B) remove it until later | A, but generous | Fuel adds route planning and a reason to carry tanks, but it can feel punishing |
| D6 | **How repair works** | A) free full repair in the garage (built now) · B) repair costs money and parts · C) also field repair kits | A for the prototype, B + C later | B and C tie damage into the economy, which only makes sense once mining and money exist |
| D7 | **Playtesters for the prototype** | Who are the 20–50 people? Friends, colleagues, Discord, Reddit | A mix: 10 friends + 20–40 strangers from r/godot, r/IndieDev | Strangers give honest feedback; friends are fast to organise |
| D18 | **Ready for outside testers?** | A) put the prototype on itch.io now (free, hidden link) · B) first polish handling after your own test (D1) | B, then A within 1–2 weeks | Testers' first impression is hard to get twice; your FPS and feel feedback should come first |
| D19 | **What materials are for** | A) crafting new parts (Terraria-style tiers) · B) money to buy parts · C) both | C in the vertical slice: craft the first tiers, buy rare ones | Decides the economy and progression; nothing in the prototype depends on it yet |
| D8 | **Discord and social accounts** | Create now or at the vertical slice; which handle name | Create now under the final name (after D4) | Followers take months to build; the plan needs 1,000 wishlists by September 2027 |

## Needed before the vertical slice (by April 2027)

| # | Decision | Options | My recommendation | Why it matters |
| --- | --- | --- | --- | --- |
| D9 | **Art style** | A) HD pixel art, 16 px tiles at 2x · B) hand-painted 2D · C) low-poly 3D rendered as 2D | A | Cheapest to produce well with one artist; fits the tile world |
| D10 | **Freelance artist budget** | EUR 1,500–3,000 for 2–4 weeks | Approve once the prototype gate passes | Sets the final look for the vertical slice and the Steam page |
| D11 | **Company setup** | Sole trader or company; which country | Ask a local accountant before the first payment to a freelancer | Contracts, IP ownership and taxes depend on it |
| D12 | **AI-generated assets** | A) none · B) only placeholders, never shipped · C) allowed with Steam disclosure | B | Keeps the shipped game clean, and some players react badly to AI art |
| D13 | **Godot version pin** | Stay on 4.6.x, or move to a newer 4.x when released | Stay on 4.6.x until after Early Access unless a needed fix appears | Engine upgrades can break physics and exports |

## Needed later (2027)

| # | Decision | When | Options | My recommendation |
| --- | --- | --- | --- | --- |
| D14 | Steam page launch date | May 2027 | June 2027 (plan) or later if the slice is not ready | June 2027 |
| D15 | Early Access price | Before the store page | USD 12.99 / 14.99 / 19.99 | USD 14.99 |
| D16 | Multiplayer in Early Access | Vertical slice | Co-op 4 players (plan) or single-player only | Co-op 4: sandbox games sell better with friends |
| D17 | Publisher or self-publish | After the vertical slice | Self-publish (plan) or pitch publishers | Self-publish unless an offer funds a bigger team without taking more than about 40% of revenue |

## Decided

| # | Decision | Result | Date |
| --- | --- | --- | --- |
| — | Engine | Godot 4 with C# | 2026-11-11 |
| — | Vehicle physics | Soft body (approach B); may change after playtests | 2026-11-11 |
| — | Game direction | On-foot character in a tile world who mines, builds and drives | 2026-11-11 |
