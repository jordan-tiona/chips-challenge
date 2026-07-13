# Fidelity scoreboard (M4)

Replaying the community's public CCLP1 solutions (MS ruleset) through
the engine on Tile World's clock. A Win means the recorded solution
finishes the level exactly as it did in the original engine. Regenerate
with the replay scoreboard test; update the floor in Cclp1ReplayTests
when the pass count improves.

_Last run: 2026-07-13, engine defaults ReplayOptions { MonsterOffset = 2, SlideOffset = 1, MovesFirst = True, TeethOffset = 1 }_

- **Win**: 58
- **Death**: 65
- **NoWin**: 19
- **Unsupported**: 7

## Known engine approximations (suspected causes)

- No boosting (moving off ice/force floors grants MS a same-tick move)
- Force-floor overrides allowed every tick (MS: alternating)
- No slip list: slide processing order differs from MS strict ordering
- Chip move timing not gated to 4-tick boundaries during slides
- Teleport scan order approximated for monsters

## Failures

- #5 Facades: NoWin at t=572 — moves used 94/94, chip at (9,16), chips left 7
- #10 Graduation: NoWin at t=1844 — moves used 405/405, chip at (24,1), chips left 0
- #13 The Monster Cages: Death at t=74 — Look out for creatures! at (25,13)
- #14 Wedges: Death at t=572 — Chip can't swim without flippers! at (11,14)
- #18 Square Dancing: Death at t=108 — Look out for creatures! at (15,8)
- #19 Feel the Static: Death at t=824 — Look out for creatures! at (13,16)
- #20 Chip Suey: Death at t=160 — Look out for creatures! at (7,10)
- #23 Circles: Unsupported at t=0 — solution uses mouse or diagonal moves
- #24 Chip's Checkers: NoWin at t=346 — moves used 72/72, chip at (6,14), chips left 9
- #27 Teleport Depot: NoWin at t=540 — moves used 101/101, chip at (14,15), chips left 0
- #28 The Last Starfighter: Death at t=995 — Don't step on the bombs! at (21,13)
- #29 Sky High or Deep Down: Death at t=50 — Look out for creatures! at (4,24)
- #30 Button Brigade: NoWin at t=820 — moves used 153/153, chip at (16,17), chips left 0
- #35 Mystery Wall: Death at t=1424 — Look out for creatures! at (8,13)
- #37 Habitat: Death at t=50 — Look out for creatures! at (18,14)
- #41 Descending Ceiling: Death at t=82 — Look out for creatures! at (19,8)
- #42 Mughfe: Death at t=1062 — Look out for creatures! at (21,10)
- #43 Gears: Death at t=262 — Look out for creatures! at (8,15)
- #47 Bombs Away: Death at t=256 — Don't step on the bombs! at (20,11)
- #51 H2O Below 273 K: NoWin at t=734 — moves used 221/221, chip at (16,22), chips left 25
- #53 Start at the End: Death at t=282 — Look out for creatures! at (11,4)
- #55 The Chambers: Death at t=22 — Look out for creatures! at (17,14)
- #58 Corral: Death at t=124 — Look out for creatures! at (15,19)
- #60 Guard: Death at t=740 — Look out for creatures! at (15,14)
- #62 Design Swap: Death at t=1334 — Look out for creatures! at (1,2)
- #63 New Block in Town: NoWin at t=732 — moves used 142/142, chip at (12,14), chips left 0
- #66 Klausswergner: Death at t=56 — Look out for creatures! at (5,13)
- #67 Booster Shots: NoWin at t=2120 — moves used 385/385, chip at (10,30), chips left 20
- #69 Double Diversion: Death at t=434 — Look out for creatures! at (13,8)
- #70 Juxtaposition: NoWin at t=1688 — moves used 373/373, chip at (10,10), chips left 26
- #71 Tree: Death at t=286 — Look out for creatures! at (28,22)
- #72 Breathing Room: NoWin at t=544 — moves used 83/83, chip at (12,13), chips left 0
- #73 Occupied: Death at t=1208 — Look out for creatures! at (15,29)
- #74 Traveler: NoWin at t=2148 — moves used 520/520, chip at (24,8), chips left 6
- #75 ToggleTank: Death at t=156 — Chip can't swim without flippers! at (17,22)
- #76 Funfair: Death at t=1789 — Look out for creatures! at (6,27)
- #78 Secret Passages: NoWin at t=1540 — moves used 336/336, chip at (14,9), chips left 33
- #80 Flipside: Death at t=628 — Look out for creatures! at (12,28)
- #81 Colors for Extreme: Death at t=22 — Look out for creatures! at (18,12)
- #83 Ruined World: Death at t=80 — Look out for creatures! at (24,19)
- #85 Black Hole: Death at t=404 — Look out for creatures! at (20,16)
- #86 Starry Night: NoWin at t=1032 — moves used 200/200, chip at (8,7), chips left 4
- #87 Pluto: Death at t=238 — Look out for creatures! at (4,19)
- #88 Chip Block Galaxy: Death at t=10 — Look out for creatures! at (13,29)
- #89 Chip Grove City: Death at t=1558 — Look out for creatures! at (29,2)
- #90 Bowling Alleys: Unsupported at t=0 — solution uses mouse or diagonal moves
- #91 Roundabout: Death at t=142 — Look out for creatures! at (12,12)
- #93 Flame War: Death at t=196 — Don't step on the bombs! at (18,10)
- #95 Courtyard: Death at t=100 — Look out for creatures! at (14,6)
- #96 Going Underground: Death at t=242 — Look out for creatures! at (27,23)
- #98 Rat Race: Death at t=634 — Look out for creatures! at (20,23)
- #100 Loose Pocket: Death at t=180 — Look out for creatures! at (13,10)
- #101 Time Suspension: Death at t=2872 — Don't step on the bombs! at (10,29)
- #102 Frozen in Time: Death at t=96 — Look out for creatures! at (5,14)
- #103 Portcullis: Death at t=18 — Look out for creatures! at (14,13)
- #104 Hotel Chip: NoWin at t=3168 — moves used 705/705, chip at (7,1), chips left 9
- #106 Jailbird: Death at t=156 — Don't step on the bombs! at (8,17)
- #107 Paramecium Palace: Death at t=522 — Look out for creatures! at (15,19)
- #108 Exhibit Hall: Death at t=410 — Look out for creatures! at (5,13)
- #109 Green Clear: NoWin at t=1544 — moves used 337/337, chip at (23,7), chips left 0
- #110 Badlands: Death at t=2118 — Look out for creatures! at (4,6)
- #111 Alternate Universe: Death at t=158 — Don't step on the bombs! at (19,2)
- #112 Carousel: NoWin at t=2112 — moves used 432/432, chip at (30,1), chips left 14
- #113 Teleport Trouble: Unsupported at t=0 — solution uses mouse or diagonal moves
- #114 Comfort Zone: Death at t=46 — Look out for creatures! at (9,3)
- #115 California: Death at t=64 — Don't step on the bombs! at (9,20)
- #116 Communism: Death at t=368 — Look out for creatures! at (14,14)
- #117 Blobs on a Plane: Unsupported at t=0 — solution uses mouse or diagonal moves
- #118 Runaway Train: Death at t=98 — Look out for creatures! at (18,7)
- #119 The Sewers: NoWin at t=2770 — moves used 622/622, chip at (15,16), chips left 5
- #120 Metal Harbor: Unsupported at t=0 — solution uses mouse or diagonal moves
- #121 Chip Plank Galleon: Death at t=164 — Look out for creatures! at (10,3)
- #122 Jeepers Creepers: Death at t=38 — Look out for creatures! at (15,15)
- #124 Utter Clutter: Death at t=174 — Look out for creatures! at (21,28)
- #126 Peek-a-Boo: Death at t=1844 — Chip can't swim without flippers! at (7,15)
- #127 In the Pink: NoWin at t=1776 — moves used 414/414, chip at (2,15), chips left 1
- #128 Elemental Park: Death at t=2572 — Don't step on the bombs! at (9,13)
- #131 Easier Than It Looks: Death at t=494 — Don't step on the bombs! at (18,29)
- #133 Steam Cleaner Simulator: Unsupported at t=0 — solution uses mouse or diagonal moves
- #134 (Ir)reversible: Death at t=714 — Look out for creatures! at (1,2)
- #135 Culprit: Death at t=36 — Look out for creatures! at (20,4)
- #136 Whirlpool: Death at t=148 — Chip can't swim without flippers! at (7,1)
- #137 Thief Street: Unsupported at t=0 — solution uses mouse or diagonal moves
- #138 Chip Alone: NoWin at t=1808 — moves used 414/414, chip at (17,12), chips left 0
- #140 Automatic (Caution) Doors: Death at t=1036 — Chip can't swim without flippers! at (6,22)
- #141 Flush: Death at t=582 — Look out for creatures! at (23,14)
- #143 Amphibia: Death at t=118 — Look out for creatures! at (2,16)
- #145 Chance Time!: Death at t=136 — Don't step on the bombs! at (13,20)
- #146 Cineworld: Death at t=1352 — Look out for creatures! at (21,7)
- #147 Thief, You've Taken All That Was Me: Death at t=618 — Look out for creatures! at (8,14)
- #148 The Snipers: Death at t=1470 — Look out for creatures! at (16,3)
