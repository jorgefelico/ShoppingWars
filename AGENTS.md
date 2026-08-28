# Repository Guidelines

## Project Overview

**Shopping Wars** is a first-person game built with **Godot 4.7** and **C#** (Godot.NET.Sdk 4.7.1, .NET 8). It is currently a single-scene prototype of the core mechanics: the player is in an enclosed arena containing a produce table full of apples; they can look around, pick up products (E), switch between 5 inventory slots (1–5 / mouse wheel), and throw them (LMB). No save system, no enemies, no HUD beyond a crosshair and a 5-slot inventory hotbar, no scene transitions.

- Engine: Godot 4.7, Forward Plus renderer, **Jolt Physics** (`project.godot`).
- Main scene: `Scenes/world.tscn`.
- Physics layers: 1 = `World`, 2 = `Player` (`[layer_names]`).

## Game Design & Game Loop (Planned)

Shopping Wars is a **4-player** (you + 3 friends) store battle royale. Multiplayer is expected but **not yet implemented** — the current prototype is single-player and focused on building the core mechanics below.

**Game loop — two phases:**

1. **Shopping phase (timer)** — each player starts in the store with a set amount of money. While the timer runs, players run around the store and buy items off the store shelves or produce tables.
2. **Battle royale** — when the timer runs out, the scene turns into a battle royale: last man standing wins. The items bought during the shopping phase are used as weapons/projectiles (thrown like the current apple mechanic). Not only do you have to worry about other players killing you — you also have to worry about the environment killing you.

**Death & loot:** when a player is killed, they drop their items on the floor. Dropped items can be collected by other players for free.

**PvE (planned):** the first PvE entity is a Roomba-like robot vacuum that patrols the isles; when it detects a player it chases and tries to kill them. More PvE entities will come later.

**Not implemented yet:** money/purchasing, the shopping timer, health & death, death item drops, the Roomba, multiplayer.

## Architecture & Data Flow

No autoloads, no `[Signal]` declarations, no signal connections, no node groups. All wiring is done with **`[Export]` node paths** set in the scene files (`node_paths` in the `.tscn`). State lives on nodes; interaction is direct method calls.

Four scripts in `Scripts/`:

- **`PlayerController.cs`** (`CharacterBody3D`, on `Scenes/player.tscn`) — all gameplay logic.
  - Mouse look: `Head` (yaw) + `Camera` (pitch, clamped ±85°); mouse captured on `_Ready`, released on `ui_cancel` (Esc) — quits if already released. Mouse-motion events with `Relative.Length() > 500` are ignored (spike guard).
  - Movement: `Input.GetVector("move_left","move_right","move_back","move_forward")` relative to `Head.GlobalBasis`; velocity approaches the target via `Mathf.MoveToward` (`Accel = 30`) and decays to zero with `Friction = 25` when no input is held; sprint (`sprint`) multiplies by `RunMultiplier`; jump (`jump` while `IsOnFloor()`) sets `Velocity.Y = JumpVelocity`; custom gravity integration; `MoveAndSlide()`. `WalkSpeed` (5.0) and `RunMultiplier` (1.5) are `[Export]`s with defaults; `Accel`, `Friction`, `JumpVelocity`, `Sensitivity`, `Gravity` are `const` at the top of the class.
  - `_PhysicsProcess` order: `HandleThrow()` → `UpdateTargeting()` → `HandleInteract()` → `HandleInventoryActions()` → `HandleMovement(delta)`.
  - `UpdateTargeting()`: forces the camera `RayCast3D` (2 m) and toggles `Product.OutlineOn/Off()` on the hit product (never the held one).
  - Pickup (`interact`): if the raycast hit is a `Product` within `PickUpRange` (2 m) and inventory not full → pick up, **even while already holding** (the previously held item stays in the inventory, just hidden via `Visible = false`). The new item gets `CollisionLayer = 0` / `CollisionMask = 0` (held items must not collide with anything — a held apple colliding with the player pushed the player around), `Freeze = true`, `Reparent(ItemHand)`, `Position = Zero`, `Inventory.AddItem()`. If the raycast hit is not a pickable product, E drops the held item in place.
  - Drop/throw: reparent to `GetTree().CurrentScene`, restore `CollisionLayer = 1` / `CollisionMask = 3`, `Freeze = false`. Throw (`fire`) additionally sets `LinearVelocity = dir * ThrowVelocity` (50) toward a point 10 m in front of the camera.
  - Slot switching: `slot1`–`slot5` and `scroll_up`/`scroll_down` → `SwitchInventorySlot()`, which hides the old held item and shows the new one (an empty slot clears `_heldItem`).
- **`Inventory.cs`** (`Node`, child of the player) — item-slot logic + the hotbar sync hook. Fixed `Product[]` (default 5, `[Export] InventorySize`), `selectedItemIndex` (public field), `AddItem` (first free slot, auto-selects it) / `GetItem` / `SelectNextItem` / `SelectPreviousItem` / `SetCurrentSelectedItem` / `RemoveCurrentSelectedItem` / `IsInventoryFull` / `FreeSlots`. Holds `[Export] InventoryBar`; **every mutator calls `InventoryBar.Refresh(InventoryItems, selectedItemIndex)`**, so the bar stays in sync no matter who mutates the inventory. **No signals.**
- **`Product.cs`** (`RigidBody3D`) — any throwable item. `[Export]` `DisplayName`, `Price`, `Damage`, `ScaleVariation` (bool, default `false`; when true, random scale 1.0–1.15 in `_Ready` — `Apple` sets it `true`), `Icon` (hotbar texture; `Apple`: `Icons/appleicon.png`). `_Ready` clones the first `MeshInstance3D` in the subtree (recursive search) into a hidden child `_outline` whose surfaces override with `Shaders/outline.gdshader`; `OutlineOn()/OutlineOff()` toggle it.
- **`InventoryBar.cs`** (`CanvasLayer`, child of the player) — bottom hotbar UI. `Refresh(Product[] products, int currentSelectedItem)` duplicates the shared `StyleBoxFlat` per slot panel and sets only `BorderColor` (gold on the selected slot, black on the rest; border width comes from the scene's subresource), then fills each slot's `Label`/`TextureRect` (`Icon` if set, else `DisplayName`; slot number when empty). All slots share one `StyleBoxFlat` subresource in the scene — `Refresh` duplicates it per panel, so never mutate the result of `GetThemeStylebox("panel")` in place.

**Item data flow:** apples (`Prefabs/Products/Apple.tscn`, Jolt rigid bodies: mass 0.2, sphere collider, CCD, `collision_mask = 3`) sit on `Prefabs/produce_table.tscn` → raycast highlights them → pickup zeroes the collision layer/mask (held items can't push the player or collide with anything), freezes + reparents under `Head/Camera/ItemHand` and stores the reference in `Inventory` → slot switching only toggles `Visible` (**all held items remain parented under `ItemHand`, stacked at the same position**) → throw/drop reparents to the current scene, restores the collision layer/mask, unfreezes, resumes simulation; pickable again. Every mutation (pickup, drop, throw, slot switch) also refreshes the hotbar via `InventoryBar.Refresh`.

**Scene graph** (`Scenes/world.tscn`): `World` → light, `Floor` (80×80 plane), `Player` (instance of `player.tscn` at the origin), `ProduceTable` (table + ~68 `Apple` instances), `Exterior Walls` (40 `PrototypeWall` instances forming an 80 m perimeter). `player.tscn` root `Player` → `CollisionShape3D`, `Head` → `Camera` → `RayCast3D` + `ItemHand`, `CrossHair` (3×3 px `ColorRect`), `Inventory`, `InventoryBar` (CanvasLayer → `HBoxContainer` bottom-center → `Slot1`–`Slot5`, 40×40 `PanelContainer`s each with a `Label` + `TextureRect`, `mouse_filter = 2` throughout).

## Key Directories

|Path|Purpose|
|---|---|
|`Scripts/`|All C# gameplay code (4 files)|
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
