# Chip's Challenge — Mobile Port: Design & Roadmap

*Status: planning · Last updated: 2026-07-11*

## 1. Goal

A faithful, mobile-first recreation of Chip's Challenge (the Windows 3.1/98
version) that runs on a phone. Nostalgia fidelity is the point: the levels
should *feel* like the original — same tile logic, same monster behavior,
same "one wrong step and you're toast" precision.

## 2. Scope & non-goals

**In scope**
- Complete CC1 rules engine (all tiles, all monsters, MS ruleset)
- Loading original `CHIPS.DAT` level files supplied by the player
- Bundled free levels (community level packs — see §5)
- Touch controls designed for the game's step-precise movement
- Original tileset artwork (ours, not Microsoft's)
- Runs on your phone: Android export first (one-tap deploy from the Godot
  editor), iOS later if wanted. Fully offline.

**Out of scope (for now)**
- Lynx ruleset support (the engine will be structured so it can be added)
- CC2 tiles/mechanics
- Level editor
- App store release (revisit after it's fun to play)

## 3. Legal ground rules

- Game **mechanics** are not copyrightable — a clone engine is fine.
- The name "Chip's Challenge," the original **artwork**, and the 149
  original **levels** are owned by Bridgestone Multimedia / Alpha Omega.
  We do not bundle or distribute any of them.
- We *can* read a `CHIPS.DAT` the player provides from their own copy
  (same approach as the open-source Tile World).
- The **Community Level Packs (CCLP1–CCLP4)** are freely distributable
  fan-made packs, widely considered the best CC content ever made. These
  become our bundled levels — hundreds of great levels, zero IP risk.
- Working title needs to not be "Chip's Challenge." Placeholder: **"Circuit
  Rush"** (bikeshed later; the repo name is just a repo name).

## 4. Ruleset: MS, with the engine kept ruleset-agnostic

Two canonical rulesets exist:

- **MS** — the Windows version (what you played). Discrete tile-to-tile
  movement, distinctive quirks and even beloved bugs. Movement is 5
  tiles/second, monsters move on a tick schedule.
- **Lynx** — the original Atari Lynx version. Smooth sliding movement,
  cleaner logic.

**Decision: implement MS first.** It's the nostalgic target, and its
discrete stepping is actually *easier* to implement and easier to map to
touch input. The community (Tile World source, the Chip Wiki) documents
every MS behavior in detail, including the famous quirks (boosting,
slide-delay, the "Teeth" chase pattern, etc.). We keep rules behind an
interface so Lynx can slot in later.

## 5. Content plan

| Tier | Source | Ships with app? |
|------|--------|-----------------|
| Tutorial levels (~8) | Written by us | Yes |
| CCLP1 (149 levels, beginner-friendly) | Free community pack | Yes |
| CCLP2–4 | Free community packs | Yes (or downloadable) |
| Original 149 levels | Player's own `CHIPS.DAT` via file picker | No — user-supplied |

## 6. Architecture

**Engine: Godot 4.7 (.NET build) + C#** — same toolchain and language as
the other project.

Three strictly separated layers:

```
core/     Pure rules engine: a plain .NET class library (ChipsCore) with
          NO Godot dependency, plus its xUnit test project. The folder is
          .gdignore'd; the Godot project consumes it via ProjectReference.
          Step(state, input) -> state. Fully deterministic.
game/     Scenes + rendering: TileMapLayer for the grid, camera,
          animation. Reads engine state, draws it. Never mutates it.
shell/    Menus, level select, saves, touch input mapping,
          file picker for .DAT import (Android SAF / OS dialog).
```

Why this shape:
- **Determinism = testability.** Because ChipsCore has zero Godot
  dependency, the entire engine — including replay validation (§9) —
  runs under plain `dotnet test`, no headless Godot needed. Fast, CI-trivial.
- **Tick control.** The MS ruleset is tick-based (game logic at fixed
  ticks, not frames). The engine advances only via explicit `Step()`
  calls; the game layer drives it from an accumulator in `_Process` and
  tweens sprites between tiles. Never put rules in `_PhysicsProcess`.
- **The engine stays portable** and rules stay auditable against the
  Tile World reference, independent of any scene-tree concerns.

## 7. Rules engine checklist (MS)

The full tile inventory, grouped by implementation phase:

**Phase A — walking around:** floor, wall, dirt, water, fire, exit,
chips, chip socket, 4 keys + 4 doors, boots (flippers, fire, skates,
suction), hint tile, thief.

**Phase B — mechanics:** movable blocks (incl. block-into-water → dirt),
force floors, ice + ice corners, teleports, blue walls (real/fake),
invisible walls, appearing walls, recessed walls (popup), toggle
walls + green button, clone machines + red button, traps + brown button,
tanks + blue button, gravel, bombs.

**Phase C — monsters:** bugs, paramecia, fireballs, pink balls, gliders,
walkers, blobs, teeth (chases Chip on a delayed tick — the scary one).
Each has a documented movement rule; Tile World is the reference.

**Phase D — MS quirks:** boosting off force floors/ice, slide delay,
monster order (the "monster list"), even the exploitable oddities. This is
what makes it feel *right* instead of merely correct.

## 8. Touch controls (the real design problem)

CC demands exact single-step moves, but also long runs down corridors.
Plan is to prototype all three and pick by feel:

1. **Swipe = step, swipe-and-hold = repeat** in that direction.
   Likely winner; matches muscle memory of holding an arrow key.
2. **Virtual d-pad** (bottom corner, thumb-reachable). Reliable but eats
   screen space and blocks view of the grid.
3. **Tap-to-move pathfinding** (the original actually had mouse click-to-
   move!). Great for open areas, dangerous near hazards — offered as an
   *addition* alongside swipe. Implemented: BFS over currently-enterable
   tiles; any swipe/key cancels the path; a blocked step aborts it. Once
   monsters exist (M3), add safety halts when hazards approach the path.

Also: pinch-zoom / camera follow, since a 32×32 grid at phone size is too
small to render all at once. Original showed a 9×9 viewport — we do a
follow-camera at roughly that zoom. Implemented: two-finger drag pans,
pinch zooms (0.7x shows the whole map); any move re-attaches the camera
to Chip. Decision: free scouting is allowed — the fixed 9×9 window was a
Lynx-era constraint, and hiding the board contradicts deliberate-decision
play. A purist "classic view" lock can be an option later.

## 9. Testing strategy

- **Replay validation:** Tile World's `.tws` solution files record
  complete input sequences for known levels. Feed a replay into the
  engine; the level must end in a win with the exact expected tick count.
  Public TWS sets exist for all CCLP packs — this gives us thousands of
  free end-to-end tests of ruleset fidelity. Runs as plain xUnit tests
  (`dotnet test`) since the engine has no Godot dependency.
- Unit tests per tile interaction in `core/` (xUnit).
- Golden-image tests for the renderer are overkill; eyeball it.

## 10. Level file formats

- **`.DAT`** (CC1): well-documented binary format — 2 layers of 32×32
  tile bytes with simple RLE, plus metadata (title, password, hint,
  monster list, trap/clone wiring). A weekend parser.
- Internal format: we'll just use parsed DAT structures; no need to invent
  our own.

## 11. Milestones

| # | Deliverable | You can... |
|---|-------------|-----------|
| M1 | Grid renderer + Chip walking, placeholder art, DAT parser | walk around a real level on your phone |
| M2 | Phase A+B tiles, chip counter, win/lose, level flow | beat simple levels start to finish |
| M3 | Monsters + tick engine + MS quirks | play *real* CCLP levels properly |
| M4 | TWS replay validation passing on CCLP1 | trust the engine is faithful |
| M5 | Touch control polish, sound, proper tileset art, Android export preset, saves/passwords | hand your phone to a friend |
| M6 | (optional) CHIPS.DAT import UX, Lynx ruleset, iOS export | relive 1998 exactly |

M1 is roughly a day of work (Godot's remote deploy puts it on your phone
from day one); M1–M3 is where the fun engineering lives.

## 12. Open questions

- Art direction for the original tileset: pixel-art homage vs. clean
  modern flat? (Affects nothing until M5.)
- Sound: the MS version's bleeps are iconic but owned; we need
  soundalikes.
- Name, if it ever goes public.
