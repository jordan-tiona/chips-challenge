# Fidelity scoreboard (M4)

Replaying the community's public CCLP1 solutions (MS ruleset) through
the engine on Tile World's clock. A Win means the recorded solution
finishes the level exactly as it did in the original engine. Regenerate
with the replay scoreboard test; update the floor in Cclp1ReplayTests
when the pass count improves.

_Last run: 2026-07-13 (tick engine with slip list + boosting)_

- **Win**: 60
- **Death**: 69
- **NoWin**: 13
- **Unsupported**: 7

## Known engine approximations (suspected causes)

- No boosting (moving off ice/force floors grants MS a same-tick move)
- Force-floor overrides allowed every tick (MS: alternating)
- No slip list: slide processing order differs from MS strict ordering
- Chip move timing not gated to 4-tick boundaries during slides
- Teleport scan order approximated for monsters

## Failures

- #9 Swept Away: NoWin at t=656 — moves used 119/119, chip at (18,18), chips left 5
- #10 Graduation: Death at t=1388 — Don't step on the bombs! at (3,29)
- #13 The Monster Cages: Death at t=76 — Look out for creatures! at (25,13)
- #14 Wedges: Death at t=572 — Chip can't swim without flippers! at (11,14)
- #18 Square Dancing: Death at t=108 — Look out for creatures! at (15,8)
- #19 Feel the Static: Death at t=112 — Look out for creatures! at (13,16)
- #20 Chip Suey: Death at t=552 — Look out for creatures! at (24,9)
- #23 Circles: Unsupported at t=0 — solution uses mouse or diagonal moves
- #27 Teleport Depot: Death at t=68 — Chip can't swim without flippers! at (12,14)
- #28 The Last Starfighter: Death at t=807 — Chip can't swim without flippers! at (3,8)
- #29 Sky High or Deep Down: Death at t=52 — Look out for creatures! at (4,24)
- #30 Button Brigade: NoWin at t=820 — moves used 153/153, chip at (11,19), chips left 0
- #33 Spitting Image: Death at t=192 — Walking on fire needs fire boots! at (25,27)
- #35 Mystery Wall: Death at t=1424 — Look out for creatures! at (8,13)
- #36 Rhombus: NoWin at t=832 — moves used 160/160, chip at (8,15), chips left 2
- #37 Habitat: Death at t=52 — Look out for creatures! at (18,14)
- #38 Heat Conductor: Death at t=64 — Walking on fire needs fire boots! at (24,21)
- #41 Descending Ceiling: Death at t=84 — Look out for creatures! at (19,8)
- #42 Mughfe: Death at t=1064 — Look out for creatures! at (21,10)
- #43 Gears: Death at t=264 — Look out for creatures! at (8,15)
- #45 Who's the Boss?: Death at t=96 — Look out for creatures! at (2,7)
- #47 Bombs Away: Death at t=256 — Don't step on the bombs! at (20,11)
- #49 49 Cell: NoWin at t=1400 — moves used 365/365, chip at (24,12), chips left 1
- #51 H2O Below 273 K: NoWin at t=734 — moves used 221/221, chip at (16,22), chips left 25
- #53 Start at the End: Death at t=36 — Look out for creatures! at (4,5)
- #55 The Chambers: Death at t=24 — Look out for creatures! at (17,14)
- #58 Corral: Death at t=124 — Look out for creatures! at (15,19)
- #60 Guard: Death at t=708 — Look out for creatures! at (14,20)
- #62 Design Swap: Death at t=1336 — Look out for creatures! at (1,2)
- #63 New Block in Town: NoWin at t=732 — moves used 142/142, chip at (12,14), chips left 0
- #64 Chip Kart 64: NoWin at t=1112 — moves used 230/230, chip at (29,30), chips left 0
- #66 Klausswergner: Death at t=340 — Look out for creatures! at (12,20)
- #67 Booster Shots: NoWin at t=2120 — moves used 385/385, chip at (10,30), chips left 20
- #69 Double Diversion: Death at t=436 — Look out for creatures! at (13,8)
- #71 Tree: Death at t=288 — Look out for creatures! at (28,22)
- #73 Occupied: Death at t=1208 — Look out for creatures! at (15,29)
- #74 Traveler: Death at t=1812 — Look out for creatures! at (11,12)
- #75 ToggleTank: Death at t=156 — Chip can't swim without flippers! at (17,22)
- #76 Funfair: Death at t=2328 — Look out for creatures! at (12,4)
- #80 Flipside: Death at t=628 — Look out for creatures! at (12,28)
- #81 Colors for Extreme: Death at t=24 — Look out for creatures! at (18,12)
- #83 Ruined World: Death at t=80 — Look out for creatures! at (24,19)
- #85 Black Hole: Death at t=404 — Look out for creatures! at (20,16)
- #86 Starry Night: NoWin at t=1032 — moves used 200/200, chip at (8,7), chips left 4
- #87 Pluto: Death at t=240 — Look out for creatures! at (4,19)
- #88 Chip Block Galaxy: Death at t=12 — Look out for creatures! at (13,29)
- #89 Chip Grove City: Death at t=548 — Chip can't swim without flippers! at (27,27)
- #90 Bowling Alleys: Unsupported at t=0 — solution uses mouse or diagonal moves
- #93 Flame War: Death at t=196 — Don't step on the bombs! at (18,10)
- #95 Courtyard: Death at t=100 — Look out for creatures! at (14,6)
- #96 Going Underground: NoWin at t=1052 — moves used 215/215, chip at (18,9), chips left 50
- #98 Rat Race: Death at t=636 — Look out for creatures! at (20,23)
- #100 Loose Pocket: Death at t=180 — Look out for creatures! at (13,10)
- #101 Time Suspension: Death at t=2872 — Don't step on the bombs! at (10,29)
- #102 Frozen in Time: Death at t=884 — Look out for creatures! at (17,20)
- #103 Portcullis: Death at t=20 — Look out for creatures! at (14,13)
- #106 Jailbird: Death at t=156 — Don't step on the bombs! at (8,17)
- #107 Paramecium Palace: Death at t=524 — Look out for creatures! at (15,19)
- #108 Exhibit Hall: Death at t=412 — Look out for creatures! at (5,13)
- #109 Green Clear: NoWin at t=1544 — moves used 337/337, chip at (20,8), chips left 0
- #110 Badlands: Death at t=2124 — Look out for creatures! at (4,6)
- #111 Alternate Universe: Death at t=476 — Walking on fire needs fire boots! at (8,29)
- #113 Teleport Trouble: Unsupported at t=0 — solution uses mouse or diagonal moves
- #114 Comfort Zone: Death at t=48 — Look out for creatures! at (9,3)
- #115 California: Death at t=200 — Look out for creatures! at (20,25)
- #116 Communism: Death at t=368 — Look out for creatures! at (14,14)
- #117 Blobs on a Plane: Unsupported at t=0 — solution uses mouse or diagonal moves
- #118 Runaway Train: Death at t=100 — Look out for creatures! at (18,7)
- #119 The Sewers: NoWin at t=2770 — moves used 622/622, chip at (15,16), chips left 5
- #120 Metal Harbor: Unsupported at t=0 — solution uses mouse or diagonal moves
- #121 Chip Plank Galleon: Death at t=344 — Look out for creatures! at (7,27)
- #124 Utter Clutter: Death at t=176 — Look out for creatures! at (21,28)
- #126 Peek-a-Boo: Death at t=1844 — Chip can't swim without flippers! at (7,15)
- #127 In the Pink: NoWin at t=1776 — moves used 414/414, chip at (2,15), chips left 1
- #128 Elemental Park: Death at t=352 — Don't step on the bombs! at (17,6)
- #131 Easier Than It Looks: Death at t=180 — Look out for creatures! at (9,2)
- #133 Steam Cleaner Simulator: Unsupported at t=0 — solution uses mouse or diagonal moves
- #134 (Ir)reversible: Death at t=716 — Look out for creatures! at (1,2)
- #135 Culprit: Death at t=36 — Look out for creatures! at (20,4)
- #136 Whirlpool: Death at t=428 — Chip can't swim without flippers! at (16,29)
- #137 Thief Street: Unsupported at t=0 — solution uses mouse or diagonal moves
- #138 Chip Alone: Death at t=1180 — Don't step on the bombs! at (25,18)
- #140 Automatic (Caution) Doors: Death at t=1036 — Chip can't swim without flippers! at (6,22)
- #141 Flush: Death at t=584 — Look out for creatures! at (23,14)
- #143 Amphibia: Death at t=120 — Look out for creatures! at (2,16)
- #145 Chance Time!: Death at t=228 — Look out for creatures! at (16,1)
- #146 Cineworld: Death at t=1352 — Look out for creatures! at (21,7)
- #147 Thief, You've Taken All That Was Me: Death at t=618 — Look out for creatures! at (8,14)
- #148 The Snipers: Death at t=708 — Chip can't swim without flippers! at (15,30)
