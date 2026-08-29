# Repository Guidelines

## Project Overview

**Shopping Wars** is a first-person game built with **Godot 4.7** and **C#** (Godot.NET.Sdk 4.7.1, .NET 8). It is a functional single-scene prototype of the core gameplay loop: players run around an enclosed store arena during a **Shopping phase**, buy products using cash, and fight in a **Battle Royale phase** using thrown store products as weapons while dodging/fighting the **Groomba** store-vacuum hazard.

- Engine: Godot 4.7, Forward Plus renderer, **Jolt Physics** (`project.godot`).
- Main scene: `Scenes/world.tscn`.
- Physics layers: 1 = `World`, 2 = `Player` (`[layer_names]`).

## Game Design & Core Loop (Implemented)

Shopping Wars is designed as a **4-player** store battle royale. Multiplayer is expected but **not yet implemented** — the current build is single-player with local AI/hazards.

**Game loop — three phases (`GameManager.cs`):**

1. **Lobby phase (`Lobby`)** — initial spawn/lobby state before match start.
2. **Shopping phase (`Shopping`)** — timer runs (default 30s). Players start with `$100` and buy items off store shelves/tables (`E`). Purchasing deducts `Product.Price`. Thrown items deal **no damage** during this phase.
3. **Battle Royale phase (`BattleRoyale`)** — when the shopping timer expires, combat turns on (`GamePhaseHUD` displays red warning). Items bought during shopping deal damage when thrown (`LMB`). The **Groomba** hazard activates and chases players.

**Health, Death & Loot Drops:**
- Player has 100 HP (`Health.cs`, `HealthBar.cs`).
- When health reaches 0, input is disabled, a death overlay appears (`Death.tscn`), and all held inventory items drop onto the floor (`Inventory.DropLoot()`). Dropped floor items are marked `IsForSale = false` and can be picked up for free by any player.

**PvE Enemy (Groomba):**
- Patrolling robot vacuum (`Groomba.cs`, `CharacterBody3D`, `IDamageable`).
- Uses Godot 4 `NavigationAgent3D` and `NavigationRegion3D` to pathfind around store obstacles.
- Patrols random floor points during Battle Royale; chases the player when within `DetectionRange` (8m).
- Deals contact damage (15 HP) on bump collision.
- Implements `IDamageable`: can be damaged and destroyed (`QueueFree()`) when hit by thrown products.

**Not implemented yet:** multiplayer, save system, scene transitions, multiple product types beyond Apple.

## Architecture & Data Flow

No autoloads, no `[Signal]` declarations, no signal connections, no node groups. All wiring is done with **`[Export]` node paths** set in the scene files (`node_paths` in the `.tscn`). State lives on nodes; interaction is direct method calls.

Nine scripts in `Scripts/`:

- **`PlayerController.cs`** (`CharacterBody3D`, on `Prefabs/player.tscn`) — movement, targeting, purchasing (`TryDeductMoney`), throwing, static `Instance` reference, implements `IDamageable`.
- **`Product.cs`** (`RigidBody3D`) — throwable items. Contact monitor enabled, tracks pre-impact speed `_lastVelocity`, phase-gated damage check against any `IDamageable` body.
- **`GameManager.cs`** (`Node`, child of `world.tscn`) — match state & timer singleton (`Instance`). Transitions between `Lobby`, `Shopping`, and `BattleRoyale`.
- **`GamePhaseHUD.cs`** (`CanvasLayer`) — top-screen UI displaying phase status, countdown timer (`mm:ss`), and player money.
- **`Groomba.cs`** (`CharacterBody3D`, implements `IDamageable`) — vacuum robot enemy AI using `NavigationAgent3D`. Chases player during `BattleRoyale` phase and deals contact damage.
- **`IDamageable.cs`** (`interface`) — standard contract (`void TakeDamage(int amount)`).
- **`Inventory.cs`** & **`InventoryBar.cs`** — 5-slot inventory logic + hotbar UI; includes `DropLoot()` on death.
- **`Health.cs`** & **`HealthBar.cs`** — health management & top-right HP bar UI.

**Item data flow:** apples (`Prefabs/Products/Apple.tscn`, Jolt rigid bodies: mass 0.2, sphere collider, CCD, `collision_mask = 3`) sit on `Prefabs/produce_table.tscn` → raycast highlights them → pickup/buy zeroes the collision layer/mask (held items can't push the player or collide with anything), freezes + reparents under `Head/Camera/ItemHand` and stores the reference in `Inventory` → slot switching only toggles `Visible` (**all held items remain parented under `ItemHand`, stacked at the same position**) → throw/drop reparents to the current scene, restores the collision layer/mask, unfreezes, resumes simulation; pickable again. Every mutation (pickup, drop, throw, slot switch) also refreshes the hotbar via `InventoryBar.Refresh`.

**Scene graph** (`Scenes/world.tscn`): `World` → light, `Floor` (80×80 plane), `NavigationRegion3D`, `Player` (instance of `player.tscn` at the origin), `ProduceTable` (table + ~68 `Apple` instances), `Exterior Walls` (40 `PrototypeWall` instances forming an 80 m perimeter), `GameManager`, `GamePhaseHUD`. `player.tscn` root `Player` → `CollisionShape3D`, `Head` → `Camera` → `RayCast3D` + `ItemHand`, `CrossHair`, `Inventory`, `InventoryBar`, `Health`, `HealthBar`, `Death`.

## Key Directories

|Path|Purpose|
|---|---|
|`Scripts/`|All C# gameplay code (9 files)|
|`Scenes/`|`world.tscn` (main scene), `player.tscn` (player prefab, instanced into world)|
|`Prefabs/`|Reusable scenes: `prototype_wall.tscn`, `produce_table.tscn`, `Products/Apple.tscn`|
|`Models/`|Blender `.blend` sources imported natively by Godot (`importer="scene"`); `.import` sidecars control material remaps (e.g. `apple.blend.import` remaps Blender material `M_apple` → `Materials/apple_material.tres`) and generated physics|
|`Materials/`|`StandardMaterial3D` `.tres` files (all three: albedo texture + `uv1_scale`)|
|`Textures/`|Placeholder/prototype textures (Godot stock blue grid, purple set, apple base color)|
|`Icons/`|UI icons for the hotbar (`appleicon.png`, 100×100)|
|`Shaders/`|`outline.gdshader` — inverted-hull (`cull_front, unshaded`) distance-scaled outline; loaded at runtime by `Product.cs`, not referenced by any scene/material|
|`.godot/`|**Git-ignored.** Editor state, imported binaries, shader cache, and .NET build output (`.godot/mono/temp/`) — regenerate on editor start; never commit or hand-edit|

## Development Commands

Requires the **Godot 4.7 Mono/.NET build** (on this machine: `/usr/lib/godot-mono/godot.linuxbsd.editor.x86_64.mono`). A stock (non-mono) Godot binary cannot load the C# assembly.

```bash
# Run the game (editor auto-compiles C# on F5/run)
godot --path /home/jorge/Sites/ShoppingWars

# Manual C# build (only for standalone IDE work)
dotnet build "Shopping Wars.csproj"
# Output lands in .godot/mono/temp/bin/Debug/ (git-ignored), not repo root
```

- **The project/assembly name contains a space** — always quote `"Shopping Wars.csproj"` / `"Shopping Wars.sln"` on the command line.
- `.NET 8` (`net8.0`; `net9.0` only when exporting for Android). `RootNamespace` is `ShoppingWars` (no space) while the assembly is `Shopping Wars`.
- Solution configs: `Debug`, `ExportDebug`, `ExportRelease`. No export presets exist (`exports_presets.cfg` absent).

## Code Conventions & Common Patterns

- **Wiring:** `[Export]` node paths (`Head`, `Camera`, `RayCast`, `ItemHand`, `Inventory`) set in the `.tscn` — not `GetNode` in code, not signals. `PlayerController._Ready` has a `GetNode` fallback only for null exports.
- **Input:** always go through named actions from the input map — `move_forward/back/left/right` (WASD), `jump` (Space), `interact` (E), `fire` (LMB), `sprint` (Shift), `scroll_up/down`, `slot1`–`slot5` (keys 1–5), plus `ui_cancel`. Use `Input.IsActionJustPressed` / `IsActionPressed` / `GetVector`; never raw key checks.
- **Naming:** C# public members and `[Export]`s are PascalCase; private fields are mixed underscore-camel (`_heldItem`) and PascalCase (`IsRunning`) — match the surrounding file. Folders are PascalCase (`Scripts/`, `Prefabs/`); asset files snake_case (`prototype_wall.tscn`, `apple_material.tres`) with the product prefab `Products/Apple.tscn` PascalCase to match its root node name (root node name = scene file name is the convention).
- **Tuning values:** gameplay tuning as `const` in the class (e.g. `Accel = 30f`, `Gravity = 9.8f`); per-instance tuning as `[Export]` with defaults (e.g. `PickUpRange = 2.0f`, `WalkSpeed = 5.0f`), overridden in the `.tscn`.
- **Style:** 4-space indent, `using Godot;` only, global namespace (no `namespace` declarations despite `RootNamespace`). `.editorconfig` enforces only `charset = utf-8`; `.gitattributes` normalizes to LF.
- **Error handling:** no exceptions and no logging — null guards (`?.`), early `return`s, and `if (x == null)` fallbacks. `Inventory` methods do **no bounds checking** (`GetItem`/`SetCurrentSelectedItem`); `slot1`–`slot5` assume `InventorySize >= 5`.
- **`.uid` sidecar files** (Godot 4.4+) are committed next to C# scripts and the shader — commit them when adding such files.

## Important Files

- `project.godot` — engine features (`4.7`, `C#`, `Forward Plus`), main scene, full input map, physics layer names, Jolt, rendering (d3d12 on Windows, MSAA + SSA).
- `Scenes/world.tscn` — main scene (`uid://l188u5sqw25o`); the arena layout lives here.
- `Scenes/player.tscn` — player prefab; the `node_paths` block wires the `PlayerController` exports and `Inventory.InventoryBar`.
- `Scripts/PlayerController.cs` — largest file; movement, targeting, pickup/throw, slots.
- `Scripts/Inventory.cs`, `Scripts/Product.cs` — item logic (see above).
- `Scripts/InventoryBar.cs` — hotbar UI; refreshed from `Inventory`, not from `PlayerController`.
- `Prefabs/Products/Apple.tscn` — the only product; **copy this as the template for new products** (RigidBody3D + `Product.cs` + sphere `CollisionShape3D` + model instance; exports `DisplayName/Price/Damage/ScaleVariation/Icon`).
- `Shopping Wars.csproj` / `Shopping Wars.sln` — 8-line csproj (`Godot.NET.Sdk/4.7.1`, `EnableDynamicLoading`), single-project sln.
- `icon.svg` — app icon.

## Runtime/Tooling Preferences

- Godot **4.7 Mono** + .NET **8** SDK; no NuGet packages, no CI, no Docker, no Makefile.
- Build output is redirected into `.godot/mono/temp/` by the Godot SDK — expect no `bin/`/`obj/` at repo root; `.gitignore` covers only `.godot/` and `/android/`.
- `.blend` files are imported directly by Godot (not pre-exported); changes to Blender materials require editing the `.import` remaps in `Models/*.blend.import`.
- Stray files to be aware of (not project files): `apple.blend1` at repo root and `Models/produce_table.blend1` (Blender auto-save backups), and `Models/apple_apple_lambert1_BaseColor.1001.png` (orphaned duplicate — only the `Textures/` copy is referenced).

## Testing & QA

**No test framework exists** — no NUnit/xunit/GdUnit/Gut, no test SDK in the csproj, no test files. QA is manual:

1. `godot --path <repo>` (or F5 in the editor) and playtest the changed mechanic.
2. Core loop to verify: mouse capture works (Esc releases, click re-captures) → aim at an apple (gold outline appears) → E picks it up (appears in hand; its icon appears in the hotbar) → E picks up a second apple while holding (first stays in inventory, only the new one is visible) → 1–5 / wheel switch slots (only the selected item visible; the hotbar's gold border follows) → LMB throws it toward the crosshair, E drops it in place (icon clears, slot number returns) → thrown/dropped apples fall under Jolt and can be picked up again.
3. When adding C#-testable logic, create a separate test csproj (the single-game csproj has no test SDK). For Godot-side tests, GdUnit4 (editor plugin) would be the standard choice — none is installed.

## Known Gaps / Gotchas

- `Product.Price` and `Product.Damage` are exported and set (`Apple`: 3 / 5) but **referenced nowhere** — placeholders for the planned economy/combat systems (see Game Design); nothing consumes them yet.
- An inventory hotbar exists (5 slots, per-product icons, gold highlight on the selected slot) but there is no other HUD, and none of the planned design exists yet: no money or score display, no shopping timer, no health/death, no death item drops, no enemy AI (Roomba), no save/load, no scene transitions, no multiplayer — building any of these is greenfield work.
- `PlayerController._Ready` has a dead fallback `GetNode<Camera3D>("Camera")` (real path is `Head/Camera`) that only triggers if the export is cleared — don't rely on it.
- Dropped/thrown items reparent to `GetTree().CurrentScene`, so a product always returns to the `World` root in this single-scene setup.
