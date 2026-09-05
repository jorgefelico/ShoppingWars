# Repository Guidelines

## Project Overview

**Shopping Wars** is a first-person multiplayer game built with **Godot 4.7** and **C#** (Godot.NET.Sdk 4.7.2, .NET 8). Up to 4 players run around an enclosed store arena during a **Shopping phase**, buy products using cash, and fight in a **Battle Royale phase** using thrown store products as weapons while dodging/fighting the **Groomba** store-vacuum hazard.

- Engine: Godot 4.7, Forward Plus renderer, **Jolt Physics** (`project.godot`).
- Main scene: `Scenes/MainMenu.tscn` (`uid://jb0i3ii5gkwf`).
- Arena gameplay scene: `Scenes/world.tscn` (`uid://l188u5sqw25o`).
- Physics layers: 1 = `World`, 2 = `Player` (`[layer_names]`).

## Game Design & Core Loop (Implemented)

Shopping Wars is designed as a **4-player** store battle royale with **Steam P2P Relay multiplayer** and local LAN/solo support.

**Game loop — three phases (`GameManager.cs`):**

1. **Lobby phase (`Lobby`)** — initial spawn/lobby state before match start. The host or server player interacts with the `ReadyUp` button (`E`) in the store arena to kick off the match.
2. **Shopping phase (`Shopping`)** — timer runs (default 30s). Players start with `$100` and buy items off store shelves/tables (`E`). Purchasing deducts `Product.Price`. Thrown items deal **no damage** during this phase.
3. **Battle Royale phase (`BattleRoyale`)** — when the shopping timer expires, combat turns on (`GamePhaseHUD` displays red warning). Items bought during shopping deal damage when thrown (`LMB`). The **Groomba** hazard activates, patrols the arena, and chases players.

**Interaction & Targeting System:**
- Objects implement `IInteractable` (`HoverText`, `Outline`, `HoverLabel`, `Interact(PlayerController player)`, `OutlineOn()`, `OutlineOff()`).
- `PlayerController` casts a forward raycast each physics frame to detect `IInteractable` objects, dynamically rendering inverted-hull outline shaders (`outline.gdshader`) and billboarded 3D text prompts (`Utils.CreateHoverLabel()`).
- Supported interactables: Store items (`Product.cs`) and the lobby start button (`ReadyUp.cs`).

**Health, Death & Loot Drops:**
- Player has 100 HP (`Health.cs`, `HealthBar.cs`).
- Remote players show overhead name tags (`NameCard` `Label3D`) displaying their Steam persona name.
- When health reaches 0, input is disabled, a death overlay appears (`Death.tscn`), and all held inventory items drop onto the floor (`Inventory.DropLoot()`). Dropped floor items are marked `IsForSale = false` and can be picked up for free by any player.

**PvE Enemy & AI (Groomba):**
- Patrolling robot vacuum (`Groomba.cs`, inherits `PatrolEnemy.cs`, `CharacterBody3D`, `IDamageable`, `IPatrol`).
- Uses Godot 4 `NavigationAgent3D` and `NavigationRegion3D` to pathfind around store obstacles.
- **Finite State Machine (`PatrolEntityState`):**
  - `Patrol` — Moves between random arena waypoints; ring glows green (`Constants.PATROL_GREEN`).
  - `Attack` (Chase) — Aggros on the closest player within detection range (8m) or the player who dealt damage; ring glows neon red (`Constants.PATROL_RED`).
  - `Search` — When a player breaks line of sight / distance, moves to their last known position for a search duration (3s); ring glows amber-gold (`Constants.PATROL_YELLOW`).
- Deals contact damage (15 HP) on bump collision with attack cooldowns.
- Implements `IDamageable`: can be damaged and destroyed (`QueueFree()`) via network RPC (`RpcDestroyGroomba`) when hit by thrown products.
- Smoothly replicates synchronized position and rotation across all clients.

**Products & Weapons:**
- Products: `Apple.tscn` ($3, 20 dmg), `Watermelon.tscn` ($5, 30 dmg).
- Throwable RigidBody items with `linear_velocity` replication for trajectory prediction on clients.
- `TestShooter.tscn` automated product launcher for projectile testing.

## Multiplayer & Architecture

### Networking & Steam Integration
- **Transport:** Native **GodotSteam GDExtension** (`SteamMultiplayerPeer`) providing Steam Datagram Relay (SDR) P2P networking. No port forwarding or public IP sharing required.
- **Matchmaking & Lobbies:** Steam Friends-Only lobbies created via `SteamManager.cs`. Main menu features dynamic in-game invite acceptance buttons (`OnInviteReceived` event) and Steam Overlay invite support.
- **Local Fallback:** Supports local network testing using `ENetMultiplayerPeer` (`127.0.0.1:7000`) via `JoinLocalButton`.
- **Dynamic Spawning:** `MultiplayerSpawner` in `Scenes/world.tscn` replicates players instantiated by `NetworkManager.cs`. Players spawn at numbered `SpawnPoints` markers.
- **Scene Load Handshake:** Joining clients load `Scenes/world.tscn` first and send `RpcClientReady` to the host before the host spawns their player node, preventing scene transition race conditions.
- **Network Movement Interpolation:** `PlayerController.cs` and `PatrolEnemy.cs` / `Groomba.cs` synchronize target variables (`SyncPosition`, `SyncHeadRotation`, `SyncCameraRotation`, `SyncRotation`) and perform delta-time lerping on remote clones for smooth movement across variable network latency.

### Autoloads
Configured in `project.godot`:
1. `SteamManager` (`Scripts/SteamManager.cs`) — Steam lifecycle, lobby management, GodotSteam integration, persona names.
2. `NetworkManager` (`Scripts/NetworkManager.cs`) — Player spawning, multiplayer signals, connection handshake.

### C# Scripts (20 files in `Scripts/`):

- **`Constants.cs`** — Shared static constants (e.g. `PATROL_GREEN`, `PATROL_YELLOW`, `PATROL_RED` emission colors).
- **`MainMenu.cs`** — Main menu UI controller (Host, Join, Solo, JoinLocal, and dynamic Steam friend invite banners).
- **`SteamManager.cs`** (`Autoload`) — GodotSteam GDExtension wrapper; handles lobby creation, overlay invites, joining, persona names, and `SteamMultiplayerPeer` configuration.
- **`NetworkManager.cs`** (`Autoload`) — Connection lifecycle, client readiness handshake (`RpcClientReady`), player instancing, level loading.
- **`PlayerController.cs`** (`CharacterBody3D`, on `Prefabs/player.tscn`) — Movement, mouse look, raycast targeting (`IInteractable`), purchasing, throwing, network interpolation (`SyncPosition`), authority management, Steam nameplate sync, `IDamageable`.
- **`Product.cs`** (`RigidBody3D`, implements `IInteractable`) — Throwable items. Contact monitor enabled, phase-gated damage check against `IDamageable`, thrower immunity, client-side velocity simulation.
- **`ReadyUp.cs`** (`StaticBody3D`, implements `IInteractable`) — In-world lobby ready button to trigger `StartShoppingPhase()` on the server.
- **`GameManager.cs`** (`Node`, child of `world.tscn`) — Server-authoritative match state & countdown timer singleton (`Instance`). Broadcasts high-frequency state sync (`RpcSyncState`).
- **`GamePhaseHUD.cs`** (`CanvasLayer`) — Top-screen UI displaying current phase status, countdown timer (`mm:ss`), and player money.
- **`PatrolEnemy.cs`** (`CharacterBody3D`, implements `IDamageable`, `IPatrol`) — Abstract base class for patrolling enemy AI with navigation, state transitions, player detection, and damage handling.
- **`Groomba.cs`** (`PatrolEnemy`) — Vacuum robot enemy AI with glowing indicator ring and Patrol $\to$ Attack $\to$ Search state machine.
- **`IPatrol.cs`** (`interface`) — Patrol navigation contract & `PatrolEntityState` enum (`Patrol`, `Search`, `Attack`).
- **`IInteractable.cs`** (`interface`) — Interactive world contract with hover outlines and billboard text prompts.
- **`IDamageable.cs`** (`interface`) — Damage contract (`void TakeDamage(int amount, Node3D source = null)`).
- **`Inventory.cs`** & **`InventoryBar.cs`** — 5-slot inventory logic + hotbar UI; includes `DropLoot()` on death.
- **`Health.cs`** & **`HealthBar.cs`** — Health management & top-right HP bar UI.
- **`TestShooter.cs`** (`Node3D`) — Automated test turret launching products at set intervals.
- **`Utils.cs`** — Helper utility methods (`FindMeshInstance`, `CreateHoverLabel`).

### Scene Graph
- **Main Scene (`Scenes/MainMenu.tscn`)**: Background, title, Host/Join/Solo/JoinLocal buttons, dynamic `InviteContainer` for Steam invites, `StatusLabel`.
- **World Scene (`Scenes/world.tscn`)**: `World` $\to$ DirectionalLight3D, LightmapGI (`Bakes/world.lmbake`), `Floor` (80×80 plane), `NavigationRegion3D`, `SpawnPoints` (4 Marker3D nodes), `MultiplayerSpawner`, `ProduceTable` (table + product instances), `ReadyUp` interactive button, `Exterior Walls`, `Groomba`, `TestShooter`, `GameManager`, `GamePhaseHUD`.
- **Player Prefab (`Prefabs/player.tscn`)**: Root `Player` (`CharacterBody3D`) $\to$ `CollisionShape3D`, `Head` $\to$ `Camera` $\to$ `RayCast3D` + `ItemHand`, `NameCard` (`Label3D`), `CrossHair`, `Inventory`, `InventoryBar`, `Health`, `HealthBar`, `Death`, `MultiplayerSynchronizer`.

## Key Directories

|Path|Purpose|
|---|---|
|`Scripts/`|All C# gameplay, AI, and networking code (20 files)|
|`Scenes/`|`MainMenu.tscn` (startup scene), `world.tscn` (arena gameplay scene)|
|`Prefabs/`|Reusable scenes: `player.tscn`, `Groomba.tscn`, `ReadyUp.tscn`, `SteamManager.tscn`, `produce_table.tscn`, `TestShooter.tscn`, `Death.tscn`, `GamePhaseHUD.tscn`|
|`Prefabs/Products/`|Product prefabs: `Apple.tscn`, `Watermelon.tscn`|
|`Models/`|Blender `.blend` and `.fbx` sources imported natively by Godot (`importer="scene"`)|
|`Materials/`|`StandardMaterial3D` `.tres` files (`floor_prototype`, `wall_prototype`, `ceiling`, `apple_material`, `concrete_floor`)|
|`Bakes/`|Baked lighting data: `world.lmbake`, `world.exr` for LightmapGI|
|`Textures/`|Placeholder & UI textures (`mainmenubg.png`, prototype grid textures)|
|`Icons/`|UI icons for inventory (`appleicon.png`, `watermelon.png`)|
|`Shaders/`|`outline.gdshader` — inverted-hull distance-scaled outline for hovered interactables|
|`addons/godotsteam/`|GodotSteam GDExtension 4.22 native plugin binaries (Windows, Linux, macOS, Android)|
|`.godot/`|**Git-ignored.** Editor state, imported binaries, shader cache, and .NET build output (`.godot/mono/temp/`)|

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

## Code Conventions & Common Patterns

- **Wiring:** `[Export]` node paths set in scene files (`node_paths` in `.tscn`) with code fallbacks.
- **Input:** Use named actions from the input map: `move_forward/back/left/right` (WASD), `jump` (Space), `interact` (E), `fire` (LMB), `sprint` (Shift), `scroll_up/down`, `slot1`–`slot5` (keys 1–5), `ui_cancel` (Esc).
- **Naming:** C# public members and `[Export]`s are PascalCase; private fields are mixed underscore-camel (`_heldItem`, `_targetPlayer`) and PascalCase (`IsRunning`). Asset files are snake_case (`produce_table.tscn`, `apple_material.tres`) with product prefabs PascalCase (`Products/Apple.tscn`).
- **Authority & Multiplayer:**
  - Node names for player instances use Godot 32-bit peer IDs (`1`, `2`, etc.).
  - `PlayerController._EnterTree()` and `_Ready()` parse `Name` to set `SetMultiplayerAuthority(peerId)`.
  - Non-authority player clones queue-free local UI layers (`CanvasLayer`) in `_Ready()`.
  - Remote player and enemy movement is smoothed using `SyncPosition`/`SyncRotation` lerping in `_Process()`.
- **Interaction Pattern:** World interactables implement `IInteractable`. Player raycasting detects colliders as `IInteractable` and drives `OutlineOn()` / `OutlineOff()` and `Interact(player)`.
- **Style:** 4-space indent, `using Godot;`, global namespace.

## Testing & QA

1. **Local Dual-Window Playtesting:**
   - Launch Instance 1 $\rightarrow$ click **Solo** or run local server.
   - Launch Instance 2 $\rightarrow$ click **Join Local** to connect to `127.0.0.1:7000`.
2. **Steam Online Playtesting:**
   - Launch Host on Steam $\rightarrow$ click **Host Steam Lobby**.
   - Launch Client on another Steam account $\rightarrow$ click dynamic in-menu invite button or accept invite via Steam overlay.
   - Client connects via Steam Datagram Relay (SDR), switches to `world.tscn`, and spawns at designated `SpawnPoints`.
3. **Core Loop Verification:**
   - Server/host interacts with `ReadyUp` button (`E`) to transition from Lobby to Shopping.
   - Shopping phase countdown ticks down synchronously on all screens.
   - Players buy items (`E`) deducting cash, items appear in hand and hotbar with 3D hover billboarding.
   - Battle Royale phase turns on, red warning appears, items deal damage when thrown (`LMB`).
   - Groomba activates, cycles between Patrol/Search/Attack states with color-coded ring emissions, deals contact damage, and can be destroyed by thrown products.
   - Players taking lethal damage trigger death overlay and drop their inventory as free loot on the floor.
