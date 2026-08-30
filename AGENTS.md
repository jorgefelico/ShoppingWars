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
- **Network Movement Interpolation:** `PlayerController.cs` and `Groomba.cs` synchronize target variables (`SyncPosition`, `SyncHeadRotation`, `SyncCameraRotation`, `SyncRotation`) and perform delta-time lerping on remote clones for smooth movement across variable network latency.

### Autoloads
Configured in `project.godot`:
1. `SteamManager` (`Scripts/SteamManager.cs`) — Steam lifecycle, lobby management, GodotSteam integration.
2. `NetworkManager` (`Scripts/NetworkManager.cs`) — Player spawning, multiplayer signals, connection handshake.

### C# Scripts (14 files in `Scripts/`):

- **`MainMenu.cs`** — Main menu UI controller (Host, Join, Solo, JoinLocal, and dynamic Steam friend invite banners).
- **`SteamManager.cs`** (`Autoload`) — GodotSteam GDExtension wrapper; handles lobby creation, overlay invites, joining, and `SteamMultiplayerPeer` configuration.
- **`NetworkManager.cs`** (`Autoload`) — Connection lifecycle, client readiness handshake (`RpcClientReady`), player instancing, level loading.
- **`PlayerController.cs`** (`CharacterBody3D`, on `Prefabs/player.tscn`) — Movement, mouse look, targeting, purchasing, throwing, network interpolation (`SyncPosition`), authority management, `IDamageable`.
- **`Product.cs`** (`RigidBody3D`) — Throwable items. Contact monitor enabled, phase-gated damage check against `IDamageable`, thrower immunity, client-side velocity simulation.
- **`GameManager.cs`** (`Node`, child of `world.tscn`) — Server-authoritative match state & countdown timer singleton (`Instance`). Broadcasts high-frequency state sync (`RpcSyncState`).
- **`GamePhaseHUD.cs`** (`CanvasLayer`) — Top-screen UI displaying current phase status, countdown timer (`mm:ss`), and player money.
- **`Groomba.cs`** (`CharacterBody3D`, implements `IDamageable`) — Vacuum robot enemy AI using `NavigationAgent3D` with smooth remote interpolation.
- **`IDamageable.cs`** (`interface`) — Standard contract (`void TakeDamage(int amount)`).
- **`Inventory.cs`** & **`InventoryBar.cs`** — 5-slot inventory logic + hotbar UI; includes `DropLoot()` on death.
- **`Health.cs`** & **`HealthBar.cs`** — Health management & top-right HP bar UI.
- **`TestShooter.cs`** (`Node3D`) — Automated test turret launching products at set intervals.

### Scene Graph
- **Main Scene (`Scenes/MainMenu.tscn`)**: Background, title, Host/Join/Solo/JoinLocal buttons, dynamic `InviteContainer` for Steam invites, `StatusLabel`.
- **World Scene (`Scenes/world.tscn`)**: `World` → Light, `Floor` (80×80 plane), `NavigationRegion3D`, `SpawnPoints` (4 Marker3D nodes), `MultiplayerSpawner`, `ProduceTable` (table + product instances), `Exterior Walls` (40 `PrototypeWall` instances), `Groomba`, `TestShooter`, `GameManager`, `GamePhaseHUD`.
- **Player Prefab (`Prefabs/player.tscn`)**: Root `Player` (`CharacterBody3D`) → `CollisionShape3D`, `Head` → `Camera` → `RayCast3D` + `ItemHand`, `CrossHair`, `Inventory`, `InventoryBar`, `Health`, `HealthBar`, `Death`, `MultiplayerSynchronizer`.

## Key Directories

|Path|Purpose|
|---|---|
|`Scripts/`|All C# gameplay and networking code (14 files)|
|`Scenes/`|`MainMenu.tscn` (startup scene), `world.tscn` (arena gameplay scene)|
|`Prefabs/`|Reusable scenes: `player.tscn`, `Groomba.tscn`, `produce_table.tscn`, `prototype_wall.tscn`, `TestShooter.tscn`, `Death.tscn`, `GamePhaseHUD.tscn`|
|`Prefabs/Products/`|Product prefabs: `Apple.tscn`, `Watermelon.tscn`|
|`Models/`|Blender `.blend` and `.fbx` sources imported natively by Godot (`importer="scene"`)|
|`Materials/`|`StandardMaterial3D` `.tres` files|
|`Textures/`|Placeholder & UI textures (`mainmenubg.png`, prototype grid textures)|
|`Icons/`|UI icons for inventory (`appleicon.png`, `watermelon.png`)|
|`Shaders/`|`outline.gdshader` — inverted-hull distance-scaled outline for hovered products|
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
- **Naming:** C# public members and `[Export]`s are PascalCase; private fields are mixed underscore-camel (`_heldItem`) and PascalCase (`IsRunning`). Asset files are snake_case (`prototype_wall.tscn`, `apple_material.tres`) with product prefabs PascalCase (`Products/Apple.tscn`).
- **Authority & Multiplayer:**
  - Node names for player instances use Godot 32-bit peer IDs (`1`, `2`, etc.).
  - `PlayerController._EnterTree()` parses `Name` to set `SetMultiplayerAuthority(peerId)`.
  - Non-authority player clones queue-free local UI layers (`CanvasLayer`) in `_Ready()`.
  - Remote player and enemy movement is smoothed using `SyncPosition`/`SyncRotation` lerping in `_Process()`.
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
   - Shopping phase countdown ticks down synchronously on both screens.
   - Players buy items (`E`) deducting cash, items appear in hand and hotbar.
   - Battle Royale phase turns on, red warning appears, items deal damage when thrown (`LMB`).
   - Groomba activates, chases players, deals contact damage, and can be destroyed by thrown products.
   - Players taking lethal damage trigger death overlay and drop their inventory as free loot on the floor.
