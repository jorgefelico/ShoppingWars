# Repository Guidelines

## Project Overview

**Shopping Wars** is a first-person multiplayer game built with **Godot 4.7** and **C#** (Godot.NET.Sdk 4.7.2, .NET 8). Up to 4 players run around an enclosed supermarket arena during a **Shopping phase**, buy products using cash, and fight in a **Battle Royale phase** using thrown store products as weapons while dodging dynamic ambient store events and fighting the **Groomba** store-vacuum hazard.

- **Engine:** Godot 4.7 Mono/.NET build, Forward Plus renderer, **Jolt Physics** (`project.godot`).
- **Main scene:** `Scenes/MainMenu.tscn` (`uid://jb0i3ii5gkwf`).
- **Primary arena gameplay scene:** `Scenes/StoreInterior.tscn` (`uid://b3t8q7m1n5x2`).
- **Prototype/test arena scene:** `Scenes/world.tscn` (`uid://l188u5sqw25o`).
- **Physics layers:** 1 = `World`, 2 = `Player` (`[layer_names]`).

## Game Design & Core Loop (Implemented)

Shopping Wars is designed as a **4-player** store battle royale with **Steam P2P Relay multiplayer** and local LAN/solo support.

### Game Loop — Match Phases & Transitions (`GameManager.cs`)

1. **Lobby phase (`Lobby`)** — Initial spawn and waiting lobby state before match start. Players can move around the store freely. The host or server player interacts with the `ReadyUp` button (`E`) to kick off the match.
2. **Shopping Transition (`ShoppingTransition`)** — 10-second pre-shopping warmup countdown (`PREPARE TO SHOP!`). Allows players to scout aisles and sprint into position before stores open. Purchasing is disabled.
3. **Shopping phase (`Shopping`)** — Timed purchasing spree (default 30s). Players start with `$100` and buy items off store shelves, coolers, and display tables (`E`). Purchasing deducts `Product.Price`. Thrown items deal **no damage** during this phase.
4. **Battle Transition (`BattleTransition`)** — 10-second store lockdown countdown (`STORE LOCKDOWN - PREPARE FOR BATTLE!`). Purchasing is disabled and unbought shelf items lock down. Gives players time to select weapon slots and take cover before combat begins.
5. **Battle Royale phase (`BattleRoyale`)** — When the battle transition timer expires, combat activates with warning alarm (`GamePhaseHUD` displays a red warning banner). Store items bought during shopping deal damage when thrown (`LMB`). The **Groomba** hazard activates, patrols the aisles, and chases players. Server-authoritative **Ambient Events** trigger dynamically.
6. **Game Over / Winner phase (`GameOver`)** — Triggered when only one player remains alive (last shopper standing), if all players are eliminated (draw), or when the battle timer expires (highest remaining HP winner or tiebreaker draw).
   - Broadcasts `RpcSyncGameOver` to all clients with `winnerName` and `isDraw`.
   - Displays an animated Victory Royale / Defeat / Draw end screen (`GamePhaseHUD.tscn`) with champion details.
   - Unlocks the mouse cursor.
   - Host can click **"Restart Match"** (`RestartMatch()` / `RestartGame()`), broadcasting `RpcClientRestartMatch` and reloading `Scenes/StoreInterior.tscn` for all connected players.
   - Any player can click **"Main Menu"** (`ReturnToMainMenu()`) to disconnect multiplayer and return to `Scenes/MainMenu.tscn`.

---

### Key Gameplay Systems

**Interaction & Targeting System:**
- Objects implement `IInteractable` (`HoverText`, `Outline`, `HoverLabel`, `Interact(PlayerController player)`, `OutlineOn()`, `OutlineOff()`).
- `PlayerController` casts a forward raycast each physics frame to detect `IInteractable` objects, dynamically rendering inverted-hull outline shaders (`outline.gdshader`) and billboarded 3D text prompts (`Utils.CreateHoverLabel()`).
- Supported interactables: Store items (`Product.cs`) and the lobby start button (`ReadyUp.cs`).

**Ambient Events System (`AmbientEventManager.cs`):**
- Server-authoritative event scheduler triggering dynamic arena-wide events during the Battle Royale phase:
  - **Lights Out (`LightsOutAmbientEvent.cs`)** — Disables arena lighting (`StoreLights` group), activates flashing red emergency beacons (`EmergencyBeacon.cs`), and darkens environment ambient energy.
  - **Power Surge (`PowerSurgeAmbientEvent.cs`)** — Overcharges fluorescent lights with blinding emission and erratic flickering bursts (`FlickeringLight.cs`).
  - **Low Gravity (`LowGravityAmbientEvent.cs`)** — Reduces player and product gravity to 30%, enabling massive high-floating jumps and long-distance item tosses.
  - **Speed Frenzy (`SpeedFrenzyAmbientEvent.cs`)** — Increases player movement speed by 60% with dynamic field-of-view flare.
  - **Dense Fog (`DenseFogAmbientEvent.cs`)** — Engulfs the supermarket in thick volumetric fog, cutting visibility across aisles.
  - **Groomba Rage (`GroombaRageAmbientEvent.cs`)** — Enrages the Groomba vacuum, boosting its movement speed and aggression.
- Active events are synchronized to joining/reconnecting clients via `AmbientEventManager.SyncStateToClient()`.

**Health, Death, Spectator Mode & Loot Drops:**
- Player has 100 HP (`Health.cs`, `HealthBar.cs`).
- Remote players display overhead name tags (`NameCard` `Label3D`) synced to their Steam persona name.
- When health reaches 0, input and collisions are disabled, held items drop on the floor as free ground loot (`Inventory.DropLoot()`), and the player seamlessly transitions into **Spectator mode** (`SpectatorHUD.cs`).
- Dead players cycle between living players in first-person using `Left Click` / `Right Click`, `A` / `D`, or on-screen UI buttons (`SpectatorHUD.cs`). The camera automatically auto-advances if the spectated player is eliminated.

**PvE Enemy & AI (Groomba):**
- Patrolling robot vacuum (`Groomba.cs`, inherits `PatrolEnemy.cs`, `CharacterBody3D`, `IDamageable`, `IPatrol`).
- Uses Godot 4 `NavigationAgent3D` and `NavigationRegion3D` to pathfind around store shelves, display tables, and checkout lanes.
- **Finite State Machine (`PatrolEntityState`):**
  - `Patrol` — Moves between random arena waypoints; indicator ring glows green (`Constants.PATROL_GREEN`).
  - `Attack` (Chase) — Aggros on the closest player within detection range (8m) or the player who attacked it; ring glows neon red (`Constants.PATROL_RED`).
  - `Search` — When line of sight is broken, searches the target's last known position for 3s; ring glows amber-gold (`Constants.PATROL_YELLOW`).
- Deals contact damage (15 HP) on bump collision with cooldown.
- Implements `IDamageable`: can be damaged and destroyed (`QueueFree()`) via network RPC (`RpcDestroyGroomba`) when struck by thrown products.
- Smoothly replicates position and rotation across clients with delta interpolation.

**Products & Weapons Arsenal:**
- 25+ interactive supermarket products in `Prefabs/Products/` across grocery, hardware, appliances, and sporting goods:
  - *Produce & Groceries:* Apple ($3, 20 dmg), Watermelon ($5, 30 dmg), BreadLoaf ($3, 10 dmg), Butter ($2, 8 dmg), CerealBox ($4, 15 dmg), FrozenPizza ($6, 22 dmg), Lemon ($2, 10 dmg), MilkGallon ($4, 18 dmg), Onion ($2, 10 dmg), SodaCan ($2, 12 dmg), SweetPotato ($3, 14 dmg), WineBottle ($8, 25 dmg).
  - *Hardware & Tools:* Crowbar ($15, 35 dmg), PipeWrench ($12, 30 dmg), PowerDrill ($18, 35 dmg), Sledgehammer ($25, 45 dmg), PropaneTank ($30, 50 dmg + radial blast).
  - *Household & Appliances:* BleachBottle ($5, 18 dmg), DetergentJug ($6, 20 dmg), FireExtinguisher ($14, 28 dmg), FlatScreenTV ($40, 50 dmg), Blender ($15, 28 dmg), CookingPot ($10, 22 dmg), FryingPan ($8, 20 dmg), Toaster ($10, 22 dmg), WetFloorSign ($7, 16 dmg).
  - *Pharmacy & Misc:* PillBottle ($5, 5 dmg), RubberDuck ($1, 2 dmg), Football ($5, 15 dmg), SprayPaint ($4, 12 dmg).
- Throwable RigidBody items with speed-threshold collision damage (`MinDamageSpeed`), thrower immunity, and visual explosion/particle effects (`Explosion.cs`, `SmokeUp.tscn`).

---

## Multiplayer & Architecture

### Networking & Steam Integration
- **Transport:** Native **GodotSteam GDExtension** (`SteamMultiplayerPeer`) providing Steam Datagram Relay (SDR) P2P networking without port forwarding or public IP exposure.
- **Matchmaking & Lobbies:** Steam Friends-Only lobbies created via `SteamManager.cs`. Main menu features dynamic in-game invite acceptance buttons (`OnInviteReceived` event) and Steam Overlay invite support.
- **Local Fallback:** Supports local network testing using `ENetMultiplayerPeer` (`127.0.0.1:7000`) via `JoinLocalButton`.
- **Dynamic Spawning:** `MultiplayerSpawner` replicates players instantiated by `NetworkManager.cs`. Players spawn at numbered `SpawnPoints` markers.
- **Scene Load Handshake:** Joining clients load the level scene first and send `RpcClientReady` to the host before the host spawns their player node, preventing scene transition race conditions.
- **Network Movement Interpolation:** `PlayerController.cs` and `PatrolEnemy.cs` / `Groomba.cs` synchronize target variables (`SyncPosition`, `SyncHeadRotation`, `SyncCameraRotation`, `SyncRotation`) and perform delta-time lerping on remote clones.

### Autoloads
Configured in `project.godot`:
1. `SteamManager` (`Scripts/SteamManager.cs`) — Steam lifecycle, lobby management, GodotSteam integration, persona names.
2. `NetworkManager` (`Scripts/NetworkManager.cs`) — Connection lifecycle, client readiness handshake (`RpcClientReady`), player instancing, level loading, match restart orchestration.

---

### C# Scripts Inventory (34 Files)

#### Core & Architecture
- **`Constants.cs`** — Shared static constants (colors, patrol emissions).
- **`Utils.cs`** — Helper utility methods (`FindMeshInstance`, `CreateHoverLabel`).
- **`IDamageable.cs`** — Damage interface (`void TakeDamage(int amount, Node3D source = null)`).
- **`IInteractable.cs`** — Interactive world object interface with hover outlines and billboard text prompts.
- **`IPatrol.cs`** — AI patrol contract & `PatrolEntityState` enum (`Patrol`, `Search`, `Attack`).

#### Match Management & Networking
- **`GameManager.cs`** (`Node`) — Server-authoritative match state machine, phase timers, victory/defeat/draw calculation, and sync RPCs (`RpcSyncState`, `RpcSyncGameOver`).
- **`NetworkManager.cs`** (`Autoload`) — Connection lifecycle, client readiness handshake, level loading, player instancing, rematch coordination (`RestartMatch`).
- **`SteamManager.cs`** (`Autoload`) — GodotSteam wrapper for lobby creation, invite handling, SDR peer management, and persona queries.
- **`MainMenu.cs`** — Main menu UI controller (Host, Join, Solo, JoinLocal, Steam invite popups).

#### Player & UI
- **`PlayerController.cs`** (`CharacterBody3D`) — First-person movement, mouse look, raycast targeting, item purchasing, throwing, spectator mode cycling, authority management, and HP synchronization.
- **`Health.cs`** & **`HealthBar.cs`** — Health component and top-right HP bar UI.
- **`Inventory.cs`** & **`InventoryBar.cs`** — 5-slot inventory manager, item cycling, and ground loot dropping on death (`DropLoot()`).
- **`GamePhaseHUD.cs`** (`CanvasLayer`) — Top-of-screen phase status, countdown timer, money tracker, ambient event banner, and end-of-match Victory/Defeat/Rematch modal.
- **`SpectatorHUD.cs`** (`CanvasLayer`) — Spectator overlay with player cycling controls and elimination banner.

#### Items & Hazards
- **`Product.cs`** (`RigidBody3D`, implements `IInteractable`) — Throwable store items with phase-gated damage, pricing, and purchase logic.
- **`ReadyUp.cs`** (`StaticBody3D`, implements `IInteractable`) — In-world lobby start button triggering match start.
- **`PatrolEnemy.cs`** (`CharacterBody3D`, implements `IDamageable`, `IPatrol`) — Base class for patrolling enemy AI.
- **`Groomba.cs`** (`PatrolEnemy`) — Vacuum robot enemy with state machine navigation and color-coded indicator ring.
- **`TestShooter.cs`** (`Node3D`) — Automated projectile test turret.
- **`Explosion.cs`** (`Node3D`) — Particle burst, dynamic flash lighting, audio playback, and auto-cleanup for explosive items.
- **`WatermelonSplatter.cs`** (`Node3D`) — Multi-emitter watermelon impact burst controller (mist, juice splatter, melon chunks, seeds, rind bits, flash light, sound).
- **`FruitSplatter.cs`** (`Node3D`) — Generic multi-emitter fruit impact splatter with dynamic fruit color tinting (juice mist, splatter droplets, pulp chunks, fine spray, flash light, sound).

#### Environment & Lighting
- **`StoreFluorescentLight.cs`** (`Node3D`) — Fluorescent light fixture supporting power toggling for blackout events.
- **`FlickeringLight.cs`** (`Node3D`) — Fluorescent fixture with randomized micro-stutter flickering bursts.
- **`EmergencyBeacon.cs`** (`Node3D`) — Pulsing red emergency warning beacon activated during blackout events.

#### Ambient Events (`Scripts/AmbientEvents/`)
- **`AmbientEventManager.cs`** (`Node`) — Server-authoritative event scheduler and network synchronizer.
- **`IAmbientEvent.cs`** — Interface defining ambient event lifecycle (`Name`, `Duration`, `StartEvent()`, `EndEvent()`).
- **`AmbientEventBase.cs`** — Base class for ambient events with helper utilities.
- **`LightsOutAmbientEvent.cs`** — Supermarket blackout event.
- **`PowerSurgeAmbientEvent.cs`** — Overloaded lighting surge event.
- **`LowGravityAmbientEvent.cs`** — Low gravity float event.
- **`SpeedFrenzyAmbientEvent.cs`** — High-speed sprint event.
- **`DenseFogAmbientEvent.cs`** — Volumetric aisle fog event.
- **`GroombaRageAmbientEvent.cs`** — Enraged Groomba speed/aggro event.

---

### Scene Graph & Key Prefabs

- **Main Scene (`Scenes/MainMenu.tscn`)**: Background, title, Host/Join/Solo/JoinLocal buttons, dynamic `InviteContainer` for Steam invites, `StatusLabel`.
- **Store Arena Scene (`Scenes/StoreInterior.tscn`)**: Complete supermarket arena with perimeter wall coolers, gondola aisles (14m & 24m), checkout lanes, self-checkout kiosks, produce island tables, customer service desk, pharmacy counter, fitting rooms, freezer bunkers, electronics display tables, warehouse doors, modular lighting, `EmergencyBeacon`, `Groomba`, `GameManager`, and `GamePhaseHUD`.
- **Prototype Scene (`Scenes/world.tscn`)**: Compact testing arena.
- **Player Prefab (`Prefabs/player.tscn`)**: Root `Player` (`CharacterBody3D`) $\to` `CollisionShape3D`, `Head` $\to` `Camera` $\to` `RayCast3D` + `ItemHand`, `NameCard` (`Label3D`), `CrossHair`, `Inventory`, `InventoryBar`, `Health`, `HealthBar`, `Death`, `MultiplayerSynchronizer`.
- **Store Architecture Prefabs (`Prefabs/Store_*.tscn`)**: 20+ modular store components (`GondolaAisle`, `CheckoutLane`, `ProduceIslandTable`, `FluorescentLight`, `FreezerBunker`, etc.).
- **Shelf Stock Prefabs (`Prefabs/Stock/`)**: Pre-populated shelf layouts for canned goods, cereal boxes, detergents, apparel, bakery items, cooler drinks, and electronics.
- **Product Prefabs (`Prefabs/Products/`)**: 25+ throwable store item prefabs.

---

## Key Directories

|Path|Purpose|
|---|---|
|`Scripts/`|All C# gameplay, AI, lighting, and networking code (26 root files)|
|`Scripts/AmbientEvents/`|Modular ambient event implementations (8 files)|
|`Scenes/`|`MainMenu.tscn`, `StoreInterior.tscn` (main gameplay arena), `world.tscn` (test arena)|
|`Prefabs/`|Core reusable prefabs: `player.tscn`, `Groomba.tscn`, `ReadyUp.tscn`, `GamePhaseHUD.tscn`, `Death.tscn`, `Explosion.tscn`, `SmokeUp.tscn`, `WatermelonSplatter.tscn`, `FruitSplatter.tscn`, `Store_*.tscn`|
|`Prefabs/Products/`|25+ throwable store product prefabs (`Apple.tscn`, `Watermelon.tscn`, `PropaneTank.tscn`, etc.)|
|`Prefabs/Stock/`|Pre-stocked shelf and display modules (`Stock_CerealShelf_12m.tscn`, `Stock_ProduceCrates.tscn`, etc.)|
|`Models/`|Blender `.blend` and `.fbx` 3D model sources imported natively by Godot (`importer="scene"`)|
|`Materials/`|`StandardMaterial3D` `.tres` assets (floors, walls, ceilings, produce, metal, emissive tubes)|
|`Bakes/`|Baked lighting data: `world.lmbake`, `world.exr` for LightmapGI|
|`Textures/`|UI, floor textures, and prototype grid textures|
|`Icons/`|UI hotbar icons for items (`appleicon.png`, `watermelon.png`, etc.)|
|`Shaders/`|`outline.gdshader` — Inverted-hull distance-scaled outline for hovered interactables|
|`addons/godotsteam/`|GodotSteam GDExtension 4.22 native plugin binaries (Windows, Linux, macOS, Android)|
|`.godot/`|**Git-ignored.** Editor cache, imported binaries, shader cache, and .NET build output (`.godot/mono/temp/`)|

---

## Development Commands

Requires the **Godot 4.7 Mono/.NET build** and **.NET 8 SDK**.

```bash
# Run the game (editor auto-compiles C# on F5/run)
godot --path /path/to/shopping-wars

# Manual C# build
dotnet build "Shopping Wars.csproj"
# Output lands in .godot/mono/temp/bin/Debug/ (git-ignored), not repo root
```

- **Assembly name contains a space** — always quote `"Shopping Wars.csproj"` / `"Shopping Wars.sln"` on the command line.
- `.NET 8` (`net8.0`; `net9.0` for Android export). `RootNamespace` is `ShoppingWars`.
- Steam App ID: Default is `480` (Spacewar testing ID in `steam_appid.txt`).

---

## Code Conventions & Common Patterns

- **Wiring:** `[Export]` node paths set in scene files (`node_paths` in `.tscn`) with code fallbacks.
- **Input:** Use named actions from the input map: `move_forward/back/left/right` (WASD), `jump` (Space), `interact` (E), `drop_item` (Q), `fire` (LMB), `sprint` (Shift), `scroll_up/down`, `slot1`–`slot5` (keys 1–5), `ui_cancel` (Esc).
- **Naming:** C# public members and `[Export]`s are PascalCase; private fields use underscore-prefix camelCase (`_heldItem`, `_targetPlayer`, `_isPowered`). Asset files are snake_case (`produce_table.tscn`, `apple_material.tres`) with product prefabs PascalCase (`Products/Apple.tscn`).
- **Authority & Multiplayer:**
  - Node names for player instances use Godot 32-bit peer IDs (`1`, `2`, etc.).
  - `PlayerController._EnterTree()` and `_Ready()` parse `Name` to set `SetMultiplayerAuthority(peerId)`.
  - Non-authority player clones queue-free local UI layers (`CanvasLayer`) in `_Ready()`.
  - Remote player and enemy movement is smoothed using `SyncPosition`/`SyncRotation` lerping in `_Process()`.
- **Interaction Pattern:** World interactables implement `IInteractable`. Player raycasting detects colliders as `IInteractable` and drives `OutlineOn()` / `OutlineOff()` and `Interact(player)`.
- **Style:** 4-space indent, `using Godot;`, global namespace.

---

## Testing & QA

1. **Local Dual-Window Playtesting:**
   - Launch Instance 1 $\rightarrow$ click **Solo** or run local server.
   - Launch Instance 2 $\rightarrow$ click **Join Local** to connect to `127.0.0.1:7000`.
2. **Steam Online Playtesting:**
   - Launch Host on Steam $\rightarrow$ click **Host Steam Lobby**.
   - Launch Client on another Steam account $\rightarrow$ click dynamic in-menu invite banner or accept invite via Steam overlay.
   - Client connects via Steam Datagram Relay (SDR), switches to `StoreInterior.tscn`, and spawns at designated `SpawnPoints`.
3. **Core Loop Verification:**
   - Host interacts with `ReadyUp` button (`E`) to start Shopping phase.
   - Shopping phase countdown ticks down synchronously on all screens.
   - Players buy items (`E`) deducting cash; items appear in hand/hotbar with 3D hover billboarding.
   - Battle Royale phase activates, warning banner appears, items deal damage when thrown (`LMB`).
   - Ambient events trigger dynamically with synchronized arena effects (blackouts, fog, speed frenzy, etc.).
   - Groomba patrols and attacks players on sight/damage; can be destroyed with thrown items.
   - Players taking lethal damage trigger death overlay and drop their inventory as free ground loot.
   - Match concludes with Victory Royale / Defeat / Draw screen, allowing the host to trigger a full match rematch or players to return to the main menu.

