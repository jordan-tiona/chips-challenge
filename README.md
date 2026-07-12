# Chips Challenge (working title)

A mobile-first, IP-clean recreation of the classic Windows tile puzzler.
Godot 4.7 (.NET) + C#. See [DESIGN.md](DESIGN.md) for the full plan.

## Layout

- `core/` — pure C# rules engine + tests. No Godot dependency; `.gdignore`d
  so the editor never touches it. Test with `dotnet test`.
- `game/` — Godot scenes and rendering.
- `shell/` — menus, saves, input mapping, .DAT import.

## Working on it

- Open the repo root in Godot 4.7 (.NET build) and hit Build/Play.
- `dotnet build ChipsChallenge.sln` — builds everything.
- `dotnet test` — runs the engine test suite (no Godot needed).
