# Repository Guidelines

## Project Overview

**Shopping Wars** is a fast-paced first-person multiplayer combat game built with **Godot 4.7** and **C#** (Godot.NET.Sdk 4.7.2, .NET 8). Up to 4 players run around an enclosed supermarket arena during a **Shopping phase**, buy products using cash, and fight in a **Battle Royale phase** using thrown and swung store products as weapons while dodging dynamic ambient store events, surviving the shrinking **Arena Safe Zone**, avoiding security camera lasers, dodging the **Groomba** store-vacuum hazard, competing for **Manager's Special** supply drops, and enduring the sarcastic PA commentary of store manager **Mr. Henderson**.

- **Engine:** Godot 4.7 Mono/.NET build, Forward Plus renderer, **Jolt Physics** (`project.godot`).
- **Visual Style & Graphics Pipeline:** Vibrant comic-book toon aesthetic with cell-shaded materials, rim lighting, perspective-compensated view-space comic ink outlines (`Shaders/ink_outline.gdshader`), department banners, aisle signs, pop-in floating damage text, 2D comic HUD hover cards (`ItemHoverBox.cs`), and rigged 3D stickman player character models with blend-space animations (`Prefabs/PlayerModel.tscn`).
- **High-Fidelity Rendering Pipeline:** Scalable graphics presets (`Low`, `Medium`, `High`, `Ultra`) featuring Temporal Anti-Aliasing (TAA) combined with SMAA (MSAA 3D disabled to avoid screen-space depth conflict), full-resolution Screen-Space Reflections (SSR) with calibrated depth tolerance, 16x Anisotropic Filtering across all textures, calibrated 12-probe department and aisle-aligned ReflectionProbes with eye-level origin offsets (`Y = 2.3m`) and 4m blend bands, soft PCF shadow atlases with physical bulb sizes (`light_size = 0.35`), full-resolution Screen-Space Ambient Occlusion (SSAO) and Indirect Lighting (SSIL), multi-frequency bicubic HDR bloom, atmospheric aisle god-rays via volumetric fog, commercial floor wax clearcoat, and triplanar brushed steel shelving.
- **Main scene:** `Scenes/MainMenu.tscn` (`uid://jb0i3ii5gkwf`).
- **Primary arena gameplay scene:** `Scenes/StoreInterior.tscn` (`uid://b3t8q7m1n5x2`).
- **Prototype/test arena scene:** `Scenes/world.tscn` (`uid://l188u5sqw25o`).
- **Physics layers:** 1 = `World`, 2 = `Player` (`[layer_names]`).

---

## Game Design & Core Loop (Implemented)

Shopping Wars is designed as a **4-player** store battle royale with **Steam P2P Relay multiplayer** and local LAN/solo fallback support.

### Game Loop — Match Phases & Transitions (`GameManager.cs`)

1. **Lobby phase (`Lobby`)** — Initial spawn and waiting lobby state before match start. Players can move around the store freely, choose their **Player Perk** (`[P]`), review controls (`[H]`), or adjust match settings (`[Esc]`). The host or server player interacts with the `ReadyUp` button (`E`) to kick off the match. Mr. Henderson welcomes shoppers over the store PA.
2. **Shopping Transition (`ShoppingTransition`)** — 5-second pre-shopping warmup countdown (`PREPARE TO SHOP!`). Allows players to scout aisles and sprint into position before store shelves open. Purchasing is disabled.
3. **Shopping phase (`Shopping`)** — Timed purchasing spree (default 50s). Players start with **$175** and buy items off store shelves, coolers, and display tables (`E`). Purchasing deducts `Product.Price` (affected by discounts). Thrown items and melee strikes deal **no damage** during this phase. Halfway through the timer, Mr. Henderson chimes in to hurry shoppers along.
4. **Battle Transition (`BattleTransition`)** — 10-second store lockdown countdown (`STORE LOCKDOWN - PREPARE FOR BATTLE!`). Purchasing is disabled, player perks lock in, and unbought shelf items lock down (unless using the *Scavenger* perk). Gives players time to select weapon slots and take cover before combat begins.
5. **Battle Royale phase (`BattleRoyale`)** — When the battle transition timer expires, combat activates with a warning alarm (`GamePhaseHUD` displays a red warning banner). Store items deal damage when thrown (`LMB`) or used in melee swings (`RMB`). The **Arena Safe Zone** ring begins shrinking, the **Groomba** hazard activates, **Laser Cameras** target trespassers, **Manager's Special Drops** parachute in, and server-authoritative **Ambient Events** trigger dynamically. Consumable items can be eaten (`[R]`) to restore health.
6. **Game Over / Winner phase (`GameOver`)** — Triggered when only one player remains alive (last shopper standing), if all players are eliminated (draw), or when the battle timer expires (highest remaining HP winner or tiebreaker draw).
   - Broadcasts `RpcSyncGameOver` to all clients with `winnerName` and `isDraw`.
   - Displays an animated Victory Royale / Defeat / Draw end screen (`GamePhaseHUD.tscn`) with champion details.
   - Mr. Henderson delivers victory or draw commentary over the PA.
   - Unlocks the mouse cursor.
   - Host can click **"Restart Match"** (`RestartMatch()` / `RestartGame()`), broadcasting `RpcClientRestartMatch`, resetting caches, and reloading `Scenes/StoreInterior.tscn` for all connected players.
   - Any player can click **"Main Menu"** (`ReturnToMainMenu()`) to leave the Steam lobby, close active multiplayer peers, clear rich presence, unlock the cursor, and return cleanly to `Scenes/MainMenu.tscn`.

---

### Key Gameplay Systems

**Store Announcer & Personality — Mr. Henderson (`ManagerAnnouncer.cs`, `ManagerEmotion.cs`):**
- Chronically overworked, passive-aggressive General Manager of SuperMart Store #404.
- Broadcasts announcements arena-wide through spatial ceiling speakers (`CeilingSpeaker.cs`) or 2D fallback with store chime (`Sounds/store_chime.wav`) and voice ducking.
- **Priority Queue System (P1–P4):** Critical announcements (Phase changes, Game Over) interrupt lower-priority announcements; kills, weapon roasts, and flavor commentary queue cleanly with debounce and stale-line dropping.
- **Dynamic Dialogue Reactions:**
  - *Phase Announcements:* Lobby welcome, shopping start, 25-second warning, lockdown sirens, battle start, and match end.
  - *First Blood & Multikills:* "BOGO Special: Buy One, Get One FREE COFFIN!" (Double Kill), "TRIPLE KILL!" (Triple Kill).
  - *Weapon-Specific Roasts:* Tailored lines when players are eliminated with watermelons, stale baguettes, sledgehammers/tools, cookware, propane tanks, hot dogs, TVs, and more.
  - *Store Events & Hazards:* Custom quips for safe zone contraction, supply drops, bounties, and all 8 ambient events.
- **Intercom HUD Overlay (`GamePhaseHUD.cs`):** Slides down from the top of the screen with a live broadcast LED dot, channel title, dialogue text, and 7 reactive ASCII cartoon portraits matching `ManagerEmotion` (`Neutral ( ಠ_ಠ )`, `Annoyed ( ¬_¬ )`, `Greedy ( $ ‿ $ )`, `Panicked ( ⊙_⊙; )`, `Enraged ( >皿< )`, `Megaphone 📢( ᐛ )`, `Smug ( ˘‿˘ )`). Debug testable via `[F2]`.

**Player Character Model & Animations (`Prefabs/PlayerModel.tscn`, `Animations/`):**
- Replaced placeholder capsule meshes with a rigged 3D stickman character model.
- Controlled via `AnimationTree` with `AnimationNodeBlendSpace1D` smoothly interpolating between linear looping `idle`, `walk`, and `run` animations based on movement velocity and sprint state.
- Automatically aligned with player head pitch and camera orientation, hidden locally for the active player camera and visible for remote clones and spectators.

**Player Perks System (`PlayerPerk.cs`):**
- Shoppers customize their playstyle during the Lobby phase via the perk selection modal (`[P]`) or HUD cards:
  - **Bargain Hunter (💰)** — 25% discount on all store purchases.
  - **Power Arm (💪)** — +30% throw velocity, +25% bonus throw damage, and +25% bonus melee damage.
  - **Tank (🛡️)** — Increases starting and maximum HP to 260 (base is 200 HP).
  - **Scavenger (🎒)** — Bypasses store lockdown, allowing players to raid locked shelves during the Battle Royale phase for free.
  - **Speed Demon (⚡)** — Grants +20% constant movement speed.
  - **Sticky Fingers (🧤)** — Unlocks an extra 6th inventory slot (`slot6`, key `6`).

**Health, Damage Grace & Kill Rewards (`Health.cs`, `PlayerController.cs`):**
- Base player health is **200 HP** (Tank perk: **260 HP**).
- **Damage Grace Window (`DamageGraceDuration = 0.22s`):** Dampens rapid consecutive hits within 0.22s by 50% to prevent instant multi-projectile deletion (shotgun effect) while keeping combat punchy.
- **Kill Rewards:** Eliminating an opponent awards the killer **+35 HP heal**, **+$50 cash**, and a **4-second speed boost**.

**Store Bounty System — "Customer of the Month" (`GameManager.cs`, `PlayerController.cs`):**
- Server-authoritative anti-snowball / comeback mechanic during the Battle Royale phase.
- When a dominant player reaches 2+ kills, a **$50 Bounty** is placed on their head.
- Broadcasts an arena announcement via Mr. Henderson, shows a HUD notification, and attaches a 3D target billboard (`🎯 $50 BOUNTY 🎯`) above the bounty player.
- Slaying the bounty target awards **+$75 bonus cash** to the killer and triggers celebratory PA commentary.

**Manager's Special Airdrop (`ManagersSpecialDrop.cs`, `GameManager.cs`):**
- Dynamic mid-combat contested supply drop spawned in the arena during the Battle Royale phase.
- Features a glowing golden crate, rotating star pivot, high-intensity ceiling spotlight beam, beacon light, and ambient chime.
- Announced over the PA by Mr. Henderson ("Manager's Special on aisle clearance!").
- Shoppers can claim the drop via `[E]` or smash it open with damage (`IDamageable`).
- Bursts with golden spark FX and dispenses 3–4 high-tier rare weapons (Sledgehammer, Propane Tank, Frying Pan, Pill Bottle, Watermelon, FlatScreen TV, Fire Extinguisher) plus cash bonuses.

**Item Hover Cards (`ItemHoverBox.cs`):**
- 2D center-screen comic-styled HUD card positioned just below the crosshair.
- Dynamically displays item icon, title, price badge (highlighting Bargain Hunter discounts), and rich stats:
  - Damage & Melee Durability (e.g. `💥 30 DMG`, `🔨 4 HITS`)
  - Healing amount for consumables (`💚 +25 HP`)
  - Special attributes (e.g. `⚡ EXPLOSIVE AOE`, `🫧 BUBBLY FOAM`, `🥤 FIZZY SPRAY`)
  - Interaction key prompt (`[E] Buy`, `[E] Take`, `[E] Claim Special`, `[E] Ready Up`)
- Smoothly fades in and out with tweening, replacing cluttered 3D floating world labels.

**Onboarding & Controls Guide (`OnboardingContent.cs`, `MainMenu.cs`, `GamePhaseHUD.cs`):**
- Unified rules and controls reference shared between the Main Menu and in-game HUD.
- **Pre-Match Onboarding Popup:** Appears automatically in the Main Menu when clicking Host, Join, or accepting an invite so players can review rules & controls comfortably before the level loads.
- **In-Game Guide Modal:** Instantly toggled with `[H]` during gameplay.

**Combat, Melee & Consumables:**
- **Throwing (`LMB` / `fire`)**: Throws held item with physics velocity. Items deal damage based on speed and base damage, triggering impact effects and consumable/splatter physics.
- **Melee Strikes (`RMB` / `alt_fire`)**: Raycast melee attack with camera trauma and hit animation.
  - Bare fists deal 12 damage.
  - Held items act as blunt melee weapons dealing 60% of item damage (min 15 dmg) with item durability (`MeleeDurability`, default 4 hits) before shattering.
- **Consumables (`[R]`)**: Products with `IsConsumable` (such as `PillBottle` and `SodaCan`) heal the player when consumed, removing the item from inventory with sound and camera feedback.

**Combat Feedback & Visual FX:**
- **Hit Marker (`HitMarker.cs`)**: Screen-centered crosshair flash on damage dealt with procedural audio ticks (higher pitch tick on hit, lower bass thump on elimination).
- **Floating Damage Numbers (`FloatingDamageNumber.cs`)**: Animated 3D popups above hit targets featuring randomized tilt, comic punch scaling, and color-coded tiers:
  - *Light (< 20 dmg)*: Lemon yellow text (`-15`).
  - *Medium (20–39 dmg)*: Neon orange text with comic suffix (`-25 POW!`).
  - *Heavy (40+ dmg)*: Crimson text (`-50 CRIT!`).
  - *Elimination*: Red bold text (`K.O.!`).
- **Damage Overlay (`DamageOverlay.cs`)**: Red screen vignette flash on taking damage along with directional edge indicators pointing towards the incoming damage vector.
- **Specialized Splatters (`SpecialSplatters.cs`, `WatermelonSplatter.cs`, `FruitSplatter.cs`)**:
  - *Soaps & Detergents*: Cyan/white bubbly foam burst.
  - *Soda Cans*: Fizzy carbonated cola spray.
  - *Bakery & Snacks*: Pastry crumbs, cereal flakes, and debris.
  - *Electronics*: Blue electrical spark discharge and crackles.
  - *Produce*: Juicy fruit mist, splatter droplets, pulp chunks, and seeds.

**Audio Systems (`PlayerAudio.cs`, `CeilingSpeaker.cs`, `SettingsManager.cs`):**
- **Spatial Player Audio (`PlayerAudio.cs`)**: 3D footsteps (walk/sprint alternating left and right feet, heard by local and remote players with distance falloff), jump sounds, landing impacts scaled by vertical velocity, throw whooshes, pickup chimes, and inventory slot switch sounds.
- **In-Store Ceiling Speakers (`CeilingSpeaker.cs`)**: Arena-wide 3D audio speakers broadcasting store background music, match phase announcements, and Mr. Henderson commentary.
- **Audio Buses & Settings (`SettingsManager.cs`)**: Three managed audio buses (`Master`, `Music`, `SFX`) configured with persistent user volume sliders, FOV adjustment, mouse sensitivity, and display modes saved to `user://settings.cfg`.

**Hazards & Arena Dynamics:**
- **Shrinking Arena Safe Zone (`ArenaZoneManager.cs`)**: Battle royale ring that contracts in stages during combat (116m $\to$ 58m $\to$ 28m $\to$ 12m), rendering an energy barrier visual and ticking 6 DPS to players trapped outside the safe zone.
- **Security Camera Lasers (`LaserCamera.cs`)**: Sweeping wall/ceiling cameras with scanning yaw/pitch, spotlight vision cones, player target tracking, and continuous damage ticks during the Battle phase.
- **Groomba Robot Vacuum (`Groomba.cs`, inherits `PatrolEnemy.cs`):**
  - Pathfinds using `NavigationAgent3D` (Edge-centered corridor pathing, 0.45m waypoint threshold, 3m repath radius) and floor-filtered navmesh targets.
  - Features dynamic **Stuck Detection & Corner Recovery**: detects obstacle entrapment (>0.55s stuck), triggers an active reverse/deflection maneuver with an alarmed expression (`O _ O`), and repaths automatically.
  - Active wall/corner sliding deflection (`KinematicCollision3D` normal projection) and smooth yaw turning prevent grinding or pinning on gondola shelf corners.
  - Calibrated 0.38m cylinder collision radius provides a 12cm clearance cushion inside the 0.5m NavMesh margin.
  - **Sentient Perception & Senses**:
    - *Aisle Vision*: Forward vision cone with physics raycast line-of-sight up to 18m (22m in Search/alerted mode).
    - *360° Ultrasonic Proximity*: Detects players sneaking within 5m in any direction.
    - *Acoustic Sprint Sensing*: Hears sprinting footsteps within 12m; engages if in view or investigates corners if obstructed.
    - *Acoustic Disturbance & Litter Detection*: Hears thrown products impacting or landing within a 22m radius. If line-of-sight to the thrower is clear, Groomba identifies the culprit, becomes enraged (`> 皿 <`), and charges to attack (`"NO LITTERING!"`, `"YOU THREW THAT!"`, `"CLEANING VIOLATION!"`). If the thrower threw from behind cover, Groomba investigates the impact site at accelerated speed (4.2 m/s), inspects the mess with a judgemental face (`ಠ _ ಠ`), and performs a swivel scan.
    - *Near-Miss Evasion*: In-flight projectiles passing within 2.8m or landing within 3.5m trigger an emergency flinch/jolt (`O _ O`, `"WHOA!"`, `"DODGE!"`) and turn Groomba towards the attacker.
  - **FSM States**: `Patrol` (green ring, `^ ‿ ^` face), `Attack` (red ring, `> 皿 <` face, 28m chase with corner memory), `Search` (amber ring, `⊙ _ ⊙` / `! _ !` / `ಠ _ ಠ` face, investigates disturbances and scans last known positions).
  - Deals 15 contact damage with cooldown, displays kill reaction (`˘ ‿ ˘`, `"TRASH DISPOSED!"`), and self-destructs or detonates when destroyed via thrown items (`FloatingDamageNumber` displays `K.O.!`).
- **Possessed Shopping Cart — "Cartsferatu" (`PossessedCart.cs`):**
  - Terrifying runaway supermarket shopping cart with wobbly squeaking caster wheel, piercing crimson headlights, and red undercarriage glow.
  - Physics model upgraded from a square box shape to a **0.44m radius CylinderShape3D**, eliminating sharp 90-degree box corners that snared on shelves and table corners.
  - Features dynamic **Stuck Detection & Corner Recovery**: detects entrapment during patrol, reverses away from collision normals, and picks fresh ground-level destinations. During charge, detects oblique corner impacts (>0.2s caught) to trigger wall crash recoil and dizzy stun (`CLANG!`).
  - Edge-centered `NavigationAgent3D` pathing (0.55m waypoint threshold) with ground-floor target validation prevents picking unreachable elevated shelf tops.
  - **FSM States**:
    - *Patrol*: Cruises aisles at 2.5 m/s emitting rhythmic squeaky wheel clatter.
    - *Windup*: On spotting a player down an aisle, halts for 0.75s, flares headlights, rings frantic service bell (`DING DING DING!`), and spins wheels with comic spark particles (`"RAMMING SPEED!"`).
    - *Charge*: Supersonic sprint down the aisle at **10.5 m/s**! Rams players dealing 35 damage, explosive knockback (16 m/s impulse), and **knocks a product out of their inventory** onto the floor!
    - *Stunned*: If dodged and slamming into a wall or gondola shelf, Cartsferatu crashes, plays heavy metal crunch SFX, and gets stunned for 2.2s with spinning dizzy stars (`✨ @ _ @ ✨`, `"CLANG!"`), leaving it vulnerable.
    - *Destroyed*: Can be destroyed with 150 damage (or interrupted during charge with 28+ dmg). Detonates with an explosion, drops 2 high-tier weapons and cash loot, and awards +$50 to the destroyer with custom Mr. Henderson commentary.

**Ambient Events System (`AmbientEventManager.cs`):**
Server-authoritative dynamic event scheduler triggering 8 arena-wide events during the Battle Royale phase:
1. **Lights Out (`LightsOutAmbientEvent.cs`)** — Blackout disabling fluorescent lights, activating pulsing red beacons (`EmergencyBeacon.cs`), and dropping ambient energy.
2. **Power Surge (`PowerSurgeAmbientEvent.cs`)** — Overcharges lights with blinding emission and rapid flickering bursts (`FlickeringLight.cs`).
3. **Low Gravity (`LowGravityAmbientEvent.cs`)** — Reduces player and item gravity to 30% for high-floating jumps and long throws.
4. **Speed Frenzy (`SpeedFrenzyAmbientEvent.cs`)** — Boosts player movement speed by 60% with dynamic camera FOV flare.
5. **Dense Fog (`DenseFogAmbientEvent.cs`)** — Engulfs the store in thick volumetric fog.
6. **Groomba Rage (`GroombaRageAmbientEvent.cs`)** — Enrages the Groomba vacuum, boosting its movement speed and detection.
7. **Clearance Sale (`ClearanceSaleAmbientEvent.cs`)** — "Blue Light Special" boosting all player damage dealt by +50% (1.5x multiplier).
8. **Sprinkler Malfunction (`SprinklerAmbientEvent.cs`)** — Activates ceiling fire sprinklers, soaking floors to reduce friction to 25% for slippery sliding physics.

**Supermarket Environment & Dressing:**
- **Department Banners & Signs (`SupermarketDressing.cs`)**: Overhead 3D signage for Produce, Deli & Bakery, Electronics, Pharmacy, Pantry & Snacks, Apparel, Home & Hardware, and Express Checkout with toon-shaded palettes and double-sided text.
- **Aisle Markers & Sale Tags**: Hanging aisle numbers and promotional shelf stickers ("50% OFF", "BUY 1 GET 1", "HOT DEAL!").
- **Procedural Stocking (`ProceduralShelfFiller.cs`)**: Batched, asynchronous stock generator supporting 35 item varieties with department clustering, category-specific limits, and runtime seed resets.
- **Stylization Caching (`StylizationHelper.cs`)**: Cached toon materials and ink outlines (`_outlineMatCache`, `_toonMatCache`) to prevent material duplication and shader overhead across hundreds of shelf items.

**Products & Weapons Arsenal (36 Items — 35 Throwables + 1 Firearm):**
- *Firearms & Launchers:* **Potato Gun** ($35, 45 direct spud damage, 6-round pneumatic magazine, 48 m/s projectile velocity, +25% Power Arm scaling, raycast convergence, pneumatic compressed gas puff FX, mashed potato splatter, pump reload `[R]`, indestructible PVC club melee `[RMB]`).
- *Produce & Groceries:* Apple ($3, 20 dmg), Avocado ($4, 18 dmg), Baguette ($3, 10 dmg), Banana ($2, 12 dmg), CerealBox ($4, 15 dmg), ChipsBag ($2, 8 dmg), ChocolateCake ($6, 16 dmg), FrozenPizza ($6, 22 dmg), Glizzy ($2, 10 dmg), Lemon ($2, 10 dmg), MilkGallon ($4, 18 dmg), Onion ($2, 10 dmg), SodaCan ($2, 12 dmg, consumable heal), SweetPotato ($3, 14 dmg), Watermelon ($5, 30 dmg), WineBottle ($8, 25 dmg).
- *Hardware & Tools:* Crowbar ($15, 35 dmg), PipeWrench ($12, 30 dmg), PowerDrill ($18, 35 dmg), Sledgehammer ($25, 45 dmg), PropaneTank ($30, 50 dmg + radial explosion).
- *Household & Appliances:* AlarmClock ($8, 15 dmg), Boombox ($16, 22 dmg), CookingPot ($10, 22 dmg), DetergentJug ($6, 20 dmg), FireExtinguisher ($14, 28 dmg), FlatScreenTV ($40, 50 dmg), FryingPan ($8, 20 dmg), Toaster ($10, 22 dmg), WetFloorSign ($7, 16 dmg).
- *Pharmacy, Sporting & Toys:* BaseballBat ($14, 32 dmg), Football ($5, 15 dmg), PillBottle ($5, 5 dmg, consumable heal), RubberDuck ($1, 2 dmg), SprayPaint ($4, 12 dmg).

---

## Multiplayer & Architecture

### Networking & Steam Integration
- **Transport:** Native **GodotSteam GDExtension** (`SteamMultiplayerPeer`) providing Steam Datagram Relay (SDR) P2P networking without port forwarding or public IP exposure.
- **Matchmaking & In-Game Lobby Browser:**
  - Steam Friends-Only lobbies created via `SteamManager.cs`.
  - Main menu features dynamic in-game invite acceptance buttons (`OnInviteReceived` event).
  - **"Join A Friend" Modal:** Discovers friends' active matches by reading Steam rich presence (`setRichPresence("lobby", lobbyId)`), allowing 1-click joining directly inside the game without relying on the Steam overlay. Rich presence is cleared on match shutdown / return to menu.
- **Local Fallback:** Supports local network testing using `ENetMultiplayerPeer` (`127.0.0.1:7000`) via `JoinLocalButton`.
- **Dynamic Spawning:** `MultiplayerSpawner` replicates players instantiated by `NetworkManager.cs`. Players spawn at numbered `SpawnPoints` markers.
- **Scene Load Handshake:** Joining clients load the level scene first and send `RpcClientReady` to the host before the host spawns their player node, preventing scene transition race conditions.
- **Network Movement Interpolation:** `PlayerController.cs` and `PatrolEnemy.cs` / `Groomba.cs` synchronize target variables (`SyncPosition`, `SyncHeadRotation`, `SyncCameraRotation`, `SyncRotation`) and perform delta-time lerping on remote clones.
- **Rematch Cleanup:** Restarting a match reloads the scene synchronously across clients, clears material caches (`StylizationHelper.ClearCache()`), resets spawn counts (`ProceduralShelfFiller.ResetGlobalSpawnCounts()`), and scatters players to random spawn points.

### Autoloads
Configured in `project.godot`:
1. `SteamManager` (`Scripts/SteamManager.cs`) — Steam lifecycle, lobby management, GodotSteam integration, rich presence advertising, persona queries.
2. `NetworkManager` (`Scripts/NetworkManager.cs`) — Connection lifecycle, client readiness handshake (`RpcClientReady`), player instancing, level loading, match restart orchestration.

---

### C# Scripts Inventory (57 Files)

#### Core & Architecture (5 Files)
- **`Constants.cs`** — Shared static constants (colors, patrol emissions).
- **`Utils.cs`** — Helper utility methods (`FindMeshInstance`, `CreateHoverLabel`, `GetSpawnPoints`).
- **`IDamageable.cs`** — Damage interface (`void TakeDamage(int amount, Node3D source = null)`).
- **`IInteractable.cs`** — Interactive world object interface with hover outlines and billboard text prompts.
- **`IPatrol.cs`** — AI patrol contract & `PatrolEntityState` enum (`Patrol`, `Search`, `Attack`).

#### Match Management, Networking, Audio & Announcer (6 Files)
- **`GameManager.cs`** (`Node`) — Server-authoritative match state machine, phase timers, victory/defeat/draw calculation, bounty coordinator, special drop spawner, music player, and sync RPCs.
- **`NetworkManager.cs`** (`Autoload`) — Connection lifecycle, client readiness handshake, level loading, player instancing, rematch coordination.
- **`SteamManager.cs`** (`Autoload`) — GodotSteam wrapper for lobby creation, invite handling, rich presence advertising/discovery, SDR peer management, and persona queries.
- **`SettingsManager.cs`** (`static`) — Audio bus management (`Master`, `Music`, `SFX`), mouse sensitivity, FOV, display settings, and `user://settings.cfg` persistence.
- **`ManagerAnnouncer.cs`** (`Node`) — Store manager Mr. Henderson PA commentator, audio priority queue, phase announcements, multikills, and contextual roasts.
- **`ManagerEmotion.cs`** (`enum`) — Manager emotional states (`Neutral`, `Annoyed`, `Greedy`, `Panicked`, `Enraged`, `Megaphone`, `Smug`).

#### Player, Input & UI (14 Files)
- **`PlayerController.cs`** (`CharacterBody3D`) — First-person movement, mouse look, raycasting, purchasing, throwing, melee strikes, consumable eating, perks, character model blend animations, bounty tagging, and HP sync.
- **`PlayerPerk.cs`** — Perk enum and definitions (`BargainHunter`, `PowerArm`, `Tank`, `Scavenger`, `SpeedDemon`, `StickyFingers`).
- **`PlayerAudio.cs`** (`Node3D`) — 3D spatial player audio (footsteps, jumps, landings, throw whooshes, pickup chimes, item switches).
- **`Health.cs`** — Health component (base 200 HP, Tank 260 HP) with heal/damage methods, damage grace damping window, and multiplayer sync.
- **`HealthBar.cs`** — Top-right health bar UI.
- **`Inventory.cs`** — 5-slot (or 6-slot with Sticky Fingers) inventory manager, cycling, and ground loot dropping on death.
- **`InventoryBar.cs`** (`CanvasLayer`) — Bottom HUD hotbar showing item icons, count badges, and active selection animations.
- **`HitMarker.cs`** (`Control`) — Screen-center hit crosshair with procedural audio feedback.
- **`DamageOverlay.cs`** (`CanvasLayer`) — Damage vignette flash and directional damage indicators.
- **`GamePhaseHUD.cs`** (`CanvasLayer`) — Match status, countdown timer, money tracker, perk modal (`[P]`), tutorial modal (`[H]`), settings menu (`[Esc]`), Mr. Henderson intercom panel, bounty notifications, and victory/defeat screen.
- **`ItemHoverBox.cs`** (`Control`) — Center 2D comic HUD card displaying item icon, title, price badge, damage, durability, healing, and prompt key.
- **`OnboardingContent.cs`** (`static`) — Shared rules and controls UI builder used by the Main Menu pre-match card and the in-game `[H]` modal.
- **`SpectatorHUD.cs`** (`CanvasLayer`) — Spectator overlay with player cycling controls and elimination status.
- **`MainMenu.cs`** — Main menu UI controller (Host, Join A Friend in-game lobby list popup, Steam invite popups, pre-match onboarding cards).

#### Items, Combat & Visual Effects (9 Files)
- **`Product.cs`** (`RigidBody3D`, implements `IInteractable`) — Throwable and melee store items with price, damage, durability, healing, cached splatters, and impact physics.
- **`ReadyUp.cs`** (`StaticBody3D`, implements `IInteractable`) — In-world lobby start button triggering match start.
- **`ManagersSpecialDrop.cs`** (`StaticBody3D`, implements `IInteractable`, `IDamageable`) — Golden supply crate hazard with ceiling spotlight and rare weapon drops.
- **`CombatHitEffect.cs`** (`Node3D`) — Impact spark particles, omni flash light, and sound.
- **`FloatingDamageNumber.cs`** (`Node3D`) — Comic 3D floating damage and status popups ("POW!", "CRIT!", "K.O.!").
- **`SpecialSplatters.cs`** (`static`) — Specialized comedic product splatters (soap foam, soda fizz, crumbs, electric zaps).
- **`FruitSplatter.cs`** (`Node3D`) — Generic fruit impact burst with juice mist, pulp chunks, and fruit color tinting.
- **`WatermelonSplatter.cs`** (`Node3D`) — Multi-emitter watermelon impact burst (mist, juice splatter, rind chunks, seeds).
- **`Explosion.cs`** (`Node3D`) — Explosive blast effect for propane tanks and hazard detonations.

#### Arena Hazards & Environment (8 Files)
- **`ArenaZoneManager.cs`** (`Node3D`) — Shrinking BR safe zone ring with energy barrier visuals and hazard tick damage.
- **`LaserCamera.cs`** (`Area3D`) — Scanning security camera hazard with laser targeting, spotlight cone, and battle damage ticks.
- **`PatrolEnemy.cs`** (`CharacterBody3D`, implements `IDamageable`, `IPatrol`) — Base class for patrolling enemy AI.
- **`Groomba.cs`** (`PatrolEnemy`) — Patrolling robot vacuum with LED face display, exhaust trail, state navigation, and contact damage.
- **`CeilingSpeaker.cs`** (`Node3D`) — Spatial 3D ceiling speaker fixture for in-store music and announcements.
- **`StoreFluorescentLight.cs`** (`Node3D`) — Fluorescent light fixture supporting power toggling for blackout events.
- **`FlickeringLight.cs`** (`Node3D`) — Fluorescent fixture with randomized micro-stutter flickering bursts.
- **`EmergencyBeacon.cs`** (`Node3D`) — Pulsing red emergency warning beacon activated during blackout events.

#### Environment Dressing, Tools & Shaders (4 Files)
- **`SupermarketDressing.cs`** (`Node`) — Dynamic store branding, department overhead banners, hanging aisle signs, and shelf sale tags.
- **`ProceduralShelfFiller.cs`** (`Node3D`, `[Tool]`) — Batched asynchronous tool script for procedurally stocking store shelves by category.
- **`StylizationHelper.cs`** (`static`) — Utilities and material caches for applying toon materials, diffuse ramps, and comic ink outlines.
- **`TestShooter.cs`** (`Node3D`) — Projectile test turret.

#### Ambient Events (`Scripts/AmbientEvents/` - 10 Files)
- **`AmbientEventManager.cs`** (`Node`) — Server-authoritative event scheduler and client synchronizer.
- **`IAmbientEvent.cs`** — Interface defining ambient event lifecycle.
- **`AmbientEventBase.cs`** — Base class for ambient events with helper utilities.
- **`LightsOutAmbientEvent.cs`** — Supermarket blackout event.
- **`PowerSurgeAmbientEvent.cs`** — Overloaded lighting surge event.
- **`LowGravityAmbientEvent.cs`** — Low gravity float event.
- **`SpeedFrenzyAmbientEvent.cs`** — High-speed sprint event.
- **`DenseFogAmbientEvent.cs`** — Volumetric aisle fog event.
- **`GroombaRageAmbientEvent.cs`** — Enraged Groomba speed/aggro event.
- **`ClearanceSaleAmbientEvent.cs`** — Blue Light Special (+50% player damage).
- **`SprinklerAmbientEvent.cs`** — Fire sprinkler malfunction with slippery floor physics.

---

### Scene Graph & Key Prefabs

- **Main Scene (`Scenes/MainMenu.tscn`)**: Title, Host/Join buttons, dynamic `InviteContainer` for Steam invites, `FriendsPopup` modal for active friend lobbies, and status indicators.
- **Store Arena Scene (`Scenes/StoreInterior.tscn`)**: Complete supermarket arena with perimeter wall coolers, gondola aisles (14m & 24m), checkout lanes, self-checkout kiosks, produce island tables, customer service desk, pharmacy counter, fitting rooms, freezer bunkers, electronics display tables, warehouse doors, ceiling speakers, security cameras, emergency beacons, `Groomba`, `GameManager`, `ArenaZoneManager`, `ManagerAnnouncer`, and `GamePhaseHUD`.
- **Prototype Scene (`Scenes/world.tscn`)**: Compact testing arena.
- **Player Prefab (`Prefabs/player.tscn`)**: Root `Player` (`CharacterBody3D`) $\to$ `CollisionShape3D`, `Head` $\to$ `Camera` $\to$ `RayCast3D` + `ItemHand`, `PlayerAudio`, `Sketchfab_Scene` (`Prefabs/PlayerModel.tscn`, rigged 3D stickman with `AnimationTree`), `NameCard` (`Label3D`), `CrossHair`, `HitMarker`, `ItemHoverBox`, `Inventory`, `InventoryBar`, `Health`, `HealthBar`, `Death`, `DamageOverlay`, `MultiplayerSynchronizer`.
- **Store Architecture Prefabs (`Prefabs/Store_*.tscn`)**: 20+ modular store components (`GondolaAisle`, `CheckoutLane`, `ProduceIslandTable`, `FluorescentLight`, `FreezerBunker`, `CartCorral`, etc.).
- **Shelf Stock Prefabs (`Prefabs/Stock/`)**: Pre-populated shelf layouts for canned goods, cereal boxes, detergents, apparel, bakery items, cooler drinks, and electronics.
- **Hazard & Drop Prefabs**: `Groomba.tscn`, `LaserCamera.tscn`, `CeilingSpeaker.tscn`, `ManagersSpecialDrop.cs` (procedural).
- **Product Prefabs (`Prefabs/Products/`)**: 35 throwable store product prefabs.

---

## Key Directories

|Path|Purpose|
|---|---|
|`Scripts/`|All C# gameplay, AI, hazards, UI, audio, and networking code (47 root files)|
|`Scripts/AmbientEvents/`|Modular ambient event implementations (10 files)|
|`Scenes/`|`MainMenu.tscn`, `StoreInterior.tscn` (main gameplay arena), `world.tscn` (test arena)|
|`Prefabs/`|Core reusable prefabs: `player.tscn`, `PlayerModel.tscn`, `Groomba.tscn`, `LaserCamera.tscn`, `CeilingSpeaker.tscn`, `DamageOverlay.tscn`, `ReadyUp.tscn`, `GamePhaseHUD.tscn`, `Death.tscn`, `Explosion.tscn`, `SmokeUp.tscn`, `WatermelonSplatter.tscn`, `FruitSplatter.tscn`, `Store_*.tscn`|
|`Prefabs/Products/`|35 throwable store product prefabs (`Apple.tscn`, `Watermelon.tscn`, `PropaneTank.tscn`, etc.)|
|`Prefabs/Stock/`|Pre-stocked shelf and display modules (`Stock_CerealShelf_12m.tscn`, `Stock_ProduceCrates.tscn`, etc.)|
|`Models/`|Blender `.blend`, `.glb`, and `.fbx` 3D model sources imported natively by Godot (`importer="scene"`)|
|`Animations/`|Player character animation clips (`idle.anim`, `walk.anim`, `run.anim`)|
|`Materials/`|`StandardMaterial3D` `.tres` assets with comic toon diffuse modes and color palettes|
|`Bakes/`|Baked lighting data: `StoreInterior.lmbake`, `StoreInterior.exr` for LightmapGI|
|`Textures/`|PBR textures, UI elements, floor textures, and prototype grid textures|
|`Icons/`|UI hotbar icons for items (`appleicon.png`, `watermelon.png`, etc.)|
|`Shaders/`|`outline.gdshader` (hover interactable outline), `ink_outline.gdshader` (comic ink outline), `damage_flash.gdshader` (damage vignette), `volumetric_cone.gdshader` (camera cone), `toon_post_process.gdshader`|
|`Sounds/`|Sound effects library: `Music/` (soundtrack MP3s), `Footsteps/` (concrete walk/run steps), `Player/` (jump, land, throw, pickup), `store_chime.wav`|
|`Reports/`|HTML and markdown project improvement, audit, performance, and character design reports (`Mr_Henderson_Character_Hook_Report.md`, `Shopping_Wars_Improvement_Report.html`)|
|`addons/godotsteam/`|GodotSteam GDExtension native plugin binaries (Windows, Linux, macOS, Android)|
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
- **Input Actions:**
  - `move_forward/back/left/right` (WASD)
  - `jump` (Space), `sprint` (Shift)
  - `interact` (E), `drop_item` (Q)
  - `fire` (LMB - throw item)
  - `alt_fire` (RMB - melee strike / swing)
  - `R` (Eat/Drink consumable)
  - `P` (Perk selection modal)
  - `H` (Tutorial & controls guide)
  - `ui_cancel` / `Esc` (Match settings / pause menu)
  - `F2` (Debug test Mr. Henderson PA announcement)
  - `scroll_up/down`, `slot1`–`slot6` (keys 1–6)
- **Naming:** C# public members and `[Export]`s are PascalCase; private fields use underscore-prefix camelCase (`_heldItem`, `_targetPlayer`, `_isPowered`). Asset files are snake_case (`produce_table.tscn`, `apple_material.tres`) with product prefabs PascalCase (`Products/Apple.tscn`).
- **Authority & Multiplayer:**
  - Node names for player instances use Godot 32-bit peer IDs (`1`, `2`, etc.).
  - `PlayerController._EnterTree()` and `_Ready()` parse `Name` to set `SetMultiplayerAuthority(peerId)`.
  - Non-authority player clones queue-free local UI layers (`CanvasLayer`) in `_Ready()`.
  - Remote player and enemy movement is smoothed using `SyncPosition`/`SyncRotation` lerping in `_Process()`.
- **Interaction Pattern:** World interactables implement `IInteractable`. Player raycasting detects colliders as `IInteractable` and drives `ItemHoverBox` HUD cards rather than cluttered 3D labels.
- **Style:** 4-space indent, `using Godot;`, global namespace.

---

## Testing & QA

1. **Local Dual-Window Playtesting:**
   - Launch Instance 1 $\rightarrow$ click **Solo** or run local server.
   - Launch Instance 2 $\rightarrow$ click **Join Local** to connect to `127.0.0.1:7000`.
2. **Steam Online Playtesting:**
   - Launch Host on Steam $\rightarrow$ click **Host Steam Lobby** (reviews pre-match onboarding guide).
   - Launch Client on another Steam account $\rightarrow$ click **Join A Friend** to view active friend matches, or accept invite via Steam overlay.
   - Client connects via Steam Datagram Relay (SDR), switches to `StoreInterior.tscn`, and spawns at designated `SpawnPoints`.
3. **Core Loop Verification:**
   - Players select perks (`[P]`) in the lobby (e.g. Tank for 260 HP, Bargain Hunter for 25% discounts).
   - Host interacts with `ReadyUp` button (`E`) to start Shopping phase countdown.
   - Mr. Henderson chimes over the ceiling speakers welcoming players.
   - Shopping phase countdown ticks down synchronously (50s); players buy items (`E`) deducting cash ($175 starting funds).
   - Hovering over items shows 2D comic `ItemHoverBox` with price, damage stats, and interaction prompts.
   - Battle Royale phase activates, lockdown alarm sounds:
     - Thrown items (`LMB`) and melee strikes (`RMB`) deal damage with floating damage numbers and hit markers. Rapid hits are cushioned by the damage grace window.
     - Healing items (`[R]`) restore HP.
     - Arena Safe Zone shrinks progressively over time, damaging players outside.
     - Slaying a player awards +35 HP, +$50 cash, and a 4s speed boost. Slaying 2+ players marks the player as a **$50 Bounty Target**.
     - Manager's Special Drop crate spawns under a spotlight beam with rare power weapons and cash.
     - Groomba patrols and attacks players on sight/damage with expressive face changes.
     - Security cameras track and damage trespassing players.
     - Ambient events trigger dynamically with synchronized arena effects (blackouts, fog, speed frenzy, clearance sale, sprinklers, etc.).
     - Mr. Henderson roasts weapon eliminations and reacts to match events over the intercom HUD.
   - Players taking lethal damage trigger death overlay and drop their inventory as free ground loot.
   - Match concludes with Victory Royale / Defeat / Draw screen, allowing the host to trigger a full match rematch (with automatic cache invalidation) or players to return to the main menu.
