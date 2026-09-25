using Godot;

public partial class Groomba : PatrolEnemy
{
    [Export] public bool SoundOn = true;
    [Export] public float SearchDuration = 15.0f;
    [Export] public float HearingRadius = 22.0f;
    [Export] public float InvestigateSpeed = 4.2f;
    [Export] public float MaxChaseDistance = 28.0f;

    private float _searchTimer = 0f;
    private Vector3 _lastKnownPlayerPos = Vector3.Zero;
    private bool _isInvestigatingNoise = false;
    private Vector3 _noiseInvestigationPos = Vector3.Zero;
    private float _investigateScanTimer = 0f;
    private float _baseInvestigateYaw = 0f;
    private float _lostSightTimer = 0f;
    private float _nearMissCooldown = 0f;
    private float _faceResetTimer = 0f;

    [Export] MeshInstance3D RingMesh;
    [Export] public AudioStream PatrolStateSound;
    [Export] public AudioStream AttackStateSound;
    [Export] public AudioStream SearchStateSound;
    [Export] public AudioStream SelfDestructSound;
    [Export] public PackedScene ExplosionScene;
    [Export] private GpuParticles3D _smoke;
    private AudioStreamPlayer3D _audioPlayer;
    private bool _isDestroyed = false;
    public bool IsDestroyed => _isDestroyed;

    StandardMaterial3D RingMat;
    private Label3D _faceDisplay;
    private CpuParticles3D _vacuumDust;
    private float _networkSyncTimer = 0f;
    private const float NetworkSyncInterval = 0.05f; // 20 Hz sync rate
    private Vector3 _lastSentPos = Vector3.Zero;
    private Vector3 _lastSentRot = Vector3.Zero;
    private float _heartbeatTimer = 0f;

    [Export] public float VacuumRadius = 1.4f;
    private readonly System.Collections.Generic.List<Product> _swallowedProducts = new();
    private AudioStreamPlayer3D _vacuumAudioPlayer;
    private CpuParticles3D _intakeVortex;
    private float _vacuumScanTimer = 0f;
    private const float VacuumScanInterval = 0.12f;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("PatrolEnemies");
        AddToGroup("Groombas");

        // Wait for first physics frame so Navigation map is synched.
        if (Multiplayer.IsServer())
        {
            Callable.From(SetRandomPatrolTarget).CallDeferred();
        }

        if (IsMultiplayerAuthority())
        {
            GetNodeOrNull<AudioStreamPlayer3D>("AudioStreamPlayer3D")?.Play();
        }

        _audioPlayer = new AudioStreamPlayer3D();
        _audioPlayer.UnitSize = 15.0f;
        _audioPlayer.MaxDistance = 40.0f;
        _audioPlayer.VolumeDb = -2.0f;
        _audioPlayer.Bus = "Master";
        AddChild(_audioPlayer);

        if(!SoundOn) (GetNode("AudioStreamPlayer3D") as AudioStreamPlayer3D).Playing = false;

        var bodyMesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (bodyMesh != null)
        {
            var bodyMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.18f, 0.19f, 0.22f),
                Metallic = 0.25f,
                Roughness = 0.35f
            };
            bodyMesh.MaterialOverride = bodyMat;
        }

        Material activeMat = RingMesh?.GetActiveMaterial(0) ?? (RingMesh?.Mesh is PrimitiveMesh pm ? pm.Material : null);
        if (activeMat is StandardMaterial3D material)
        {
            RingMat = (StandardMaterial3D)material.Duplicate();
            RingMesh.SetSurfaceOverrideMaterial(0, RingMat);
            RingMesh.MaterialOverride = RingMat;
        }

        // Initialize expressive LED digital face
        _faceDisplay = new Label3D
        {
            Name = "FaceDisplay",
            Text = "^ ‿ ^",
            FontSize = 42,
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Modulate = Constants.PATROL_GREEN,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(0, 0.42f, 0),
            PixelSize = 0.0055f,
            RenderPriority = 6
        };
        AddChild(_faceDisplay);

        // Subtle rear vacuum exhaust dust trail
        _vacuumDust = new CpuParticles3D
        {
            Name = "VacuumDust",
            Emitting = true,
            Amount = 14,
            Lifetime = 0.45f,
            Direction = new Vector3(0, 0.25f, 1.0f),
            Spread = 30.0f,
            InitialVelocityMin = 0.6f,
            InitialVelocityMax = 1.4f,
            Gravity = new Vector3(0, 0.2f, 0),
            Position = new Vector3(0, 0.06f, 0.38f)
        };
        var dustMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.92f, 0.94f, 0.3f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _vacuumDust.Mesh = new SphereMesh
        {
            Radius = 0.05f,
            Height = 0.1f,
            Material = dustMat
        };
        AddChild(_vacuumDust);

        // Procedural vacuum suction sound player
        _vacuumAudioPlayer = new AudioStreamPlayer3D
        {
            Name = "VacuumAudioPlayer",
            Bus = "SFX",
            UnitSize = 12.0f,
            MaxDistance = 35.0f,
            VolumeDb = 2.0f,
            Stream = CreateSuctionSound()
        };
        AddChild(_vacuumAudioPlayer);

        // Nozzle suction vortex particles at front intake
        _intakeVortex = new CpuParticles3D
        {
            Name = "IntakeVortex",
            Emitting = false,
            OneShot = true,
            Amount = 16,
            Lifetime = 0.35f,
            Explosiveness = 0.85f,
            Direction = new Vector3(0, 0.2f, 1.0f),
            Spread = 50.0f,
            InitialVelocityMin = 1.2f,
            InitialVelocityMax = 2.8f,
            Position = new Vector3(0, 0.05f, -0.36f)
        };
        var vortexMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.95f, 1.0f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _intakeVortex.Mesh = new SphereMesh
        {
            Radius = 0.035f,
            Height = 0.07f,
            Material = vortexMat
        };
        AddChild(_intakeVortex);

        UpdateRingEmission(PatrolState);

        if (Health != null)
        {
            Health.HealthChanged += (cur, max) =>
            {
                if (cur <= max / 2 && _smoke != null && !_smoke.Emitting)
                {
                    _smoke.Emitting = true;
                }
            };
        }

        SyncPosition = GlobalPosition;
        SyncRotation = Rotation;
    }

    public void UpdateRingEmission(PatrolEntityState state)
    {
        if (RingMat != null)
        {
            switch (state)
            {
                case PatrolEntityState.Patrol:
                    RingMat.Emission = Constants.PATROL_GREEN;
                    break;
                case PatrolEntityState.Attack:
                    RingMat.Emission = Constants.PATROL_RED;
                    break;
                case PatrolEntityState.Search:
                    RingMat.Emission = Constants.PATROL_YELLOW;
                    break;
            }
        }

        if (_faceDisplay != null)
        {
            switch (state)
            {
                case PatrolEntityState.Patrol:
                    _faceDisplay.Text = "^ ‿ ^";
                    _faceDisplay.Modulate = Constants.PATROL_GREEN;
                    break;
                case PatrolEntityState.Attack:
                    _faceDisplay.Text = "> 皿 <";
                    _faceDisplay.Modulate = Constants.PATROL_RED;
                    break;
                case PatrolEntityState.Search:
                    _faceDisplay.Text = "⊙ _ ⊙";
                    _faceDisplay.Modulate = Constants.PATROL_YELLOW;
                    break;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_faceResetTimer > 0f)
        {
            _faceResetTimer -= (float)delta;
            if (_faceResetTimer <= 0f && !_isDestroyed)
            {
                UpdateRingEmission(PatrolState);
            }
        }

        if (Multiplayer.IsServer()) return;

        float distance = GlobalPosition.DistanceTo(SyncPosition);
        if (distance > 5.0f)
        {
            GlobalPosition = SyncPosition;
        }
        else
        {
            float lerpWeight = (float)Mathf.Clamp(delta * 20.0, 0.0, 1.0);
            GlobalPosition = GlobalPosition.Lerp(SyncPosition, lerpWeight);
        }

        float rotLerpWeight = (float)Mathf.Clamp(delta * 20.0, 0.0, 1.0);
        Rotation = new Vector3(
            Mathf.LerpAngle(Rotation.X, SyncRotation.X, rotLerpWeight),
            Mathf.LerpAngle(Rotation.Y, SyncRotation.Y, rotLerpWeight),
            Mathf.LerpAngle(Rotation.Z, SyncRotation.Z, rotLerpWeight)
        );
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.IsServer()) return;
        if (GameManager.Instance?.CurrentPhase != GamePhase.BattleRoyale) return;
        if (_isDestroyed) return;
        if (_attackCooldown > 0f) _attackCooldown -= (float)delta;
        if (_nearMissCooldown > 0f) _nearMissCooldown -= (float)delta;

        // Ground objects vacuum scan
        _vacuumScanTimer += (float)delta;
        if (_vacuumScanTimer >= VacuumScanInterval)
        {
            _vacuumScanTimer = 0f;
            CheckGroundObjectsForVacuum();
        }

        Vector3 velocity = Velocity;
        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * (float)delta;
        }

        switch (PatrolState)
        {
            case PatrolEntityState.Patrol:
                HandlePatrolState(delta, ref velocity);
                break;

            case PatrolEntityState.Attack:
                HandleAttackState(delta, ref velocity);
                break;

            case PatrolEntityState.Search:
                HandleSearchState(delta, ref velocity);
                break;
        }

        Velocity = velocity;
        MoveAndSlide();
        HandleContactDamage();

        SyncPosition = GlobalPosition;
        SyncRotation = Rotation;

        _networkSyncTimer += (float)delta;
        _heartbeatTimer += (float)delta;

        if (_networkSyncTimer >= NetworkSyncInterval && Multiplayer.HasMultiplayerPeer())
        {
            bool moved = GlobalPosition.DistanceSquaredTo(_lastSentPos) > 0.0004f;
            bool turned = Rotation.DistanceSquaredTo(_lastSentRot) > 0.0004f;
            bool heartbeat = _heartbeatTimer >= 0.4f;

            if (moved || turned || heartbeat)
            {
                _networkSyncTimer = 0f;
                _heartbeatTimer = 0f;
                _lastSentPos = GlobalPosition;
                _lastSentRot = Rotation;
                Rpc(nameof(RpcSyncTransform), SyncPosition, SyncRotation);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void RpcSyncTransform(Vector3 position, Vector3 rotation)
    {
        SyncPosition = position;
        SyncRotation = rotation;
    }

    #region Intelligent Perception & Sensory Systems

    public override void DetectPlayer()
    {
        _targetPlayer = null;
        float closestDistance = 9999f;
        PlayerController bestTarget = null;

        float forwardRange = (PatrolState == PatrolEntityState.Search) ? 22.0f : 18.0f;
        float forwardFov = (PatrolState == PatrolEntityState.Search) ? 170.0f : 140.0f;
        float closeProximityRange = 5.0f;
        float sprintHearingRange = 12.0f;

        foreach (Node node in GetTree().GetNodesInGroup("Players"))
        {
            if (node is PlayerController player && player.Health != null && !player.Health.IsDead)
            {
                float dist = GlobalPosition.DistanceTo(player.GlobalPosition);

                // 1. Close 360-degree ultrasonic proximity
                if (dist <= closeProximityRange && CanSeePlayer(player, closeProximityRange, 360f))
                {
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        bestTarget = player;
                    }
                    continue;
                }

                // 2. Forward vision cone down the aisle
                if (dist <= forwardRange && CanSeePlayer(player, forwardRange, forwardFov))
                {
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        bestTarget = player;
                    }
                    continue;
                }

                // 3. Acoustic sensing: sprinting player footsteps heard
                if (player.IsRunning && dist <= sprintHearingRange)
                {
                    if (CanSeePlayer(player, sprintHearingRange, 360f))
                    {
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            bestTarget = player;
                        }
                        continue;
                    }
                    else if (PatrolState == PatrolEntityState.Patrol && dist <= 9.0f && !_isInvestigatingNoise)
                    {
                        // Heard running footsteps around the corner! Investigate the sound!
                        _lastKnownPlayerPos = player.GlobalPosition;
                        _isInvestigatingNoise = true;
                        _noiseInvestigationPos = player.GlobalPosition;
                        _investigateScanTimer = 0f;
                        SetFloorTargetPosition(player.GlobalPosition);
                        SetState(PatrolEntityState.Search);

                        if (Multiplayer.HasMultiplayerPeer())
                        {
                            Rpc(nameof(RpcShowReaction), "! _ !", "FOOTSTEPS?!", Constants.PATROL_YELLOW);
                        }
                        else
                        {
                            RpcShowReaction("! _ !", "FOOTSTEPS?!", Constants.PATROL_YELLOW);
                        }
                        return;
                    }
                }
            }
        }

        if (bestTarget != null)
        {
            _targetPlayer = bestTarget;
            _lastKnownPlayerPos = bestTarget.GlobalPosition;
        }
    }

    public bool CanSeePlayer(PlayerController player, float maxRange, float fovDegrees = 360f)
    {
        if (player == null || !GodotObject.IsInstanceValid(player) || player.Health == null || player.Health.IsDead)
            return false;

        Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
        float dist = toPlayer.Length();
        if (dist > maxRange) return false;

        // FOV cone check
        if (fovDegrees < 350f)
        {
            Vector3 forward = -GlobalTransform.Basis.Z;
            forward.Y = 0;
            Vector3 dirFlat = new Vector3(toPlayer.X, 0, toPlayer.Z);
            if (forward.LengthSquared() > 0.001f && dirFlat.LengthSquared() > 0.001f)
            {
                float angle = Mathf.RadToDeg(forward.Normalized().AngleTo(dirFlat.Normalized()));
                if (angle > fovDegrees * 0.5f)
                {
                    return false;
                }
            }
        }

        // Raycast line of sight check against World geometry (layer 1)
        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return false;

        Vector3 eyePos = GlobalPosition + Vector3.Up * 0.25f;
        Vector3 targetChest = player.GlobalPosition + Vector3.Up * 0.9f;
        Vector3 targetHead = player.GlobalPosition + Vector3.Up * 1.5f;

        return !IsWorldRayBlocked(spaceState, eyePos, targetChest) || !IsWorldRayBlocked(spaceState, eyePos, targetHead);
    }

    private bool IsWorldRayBlocked(PhysicsDirectSpaceState3D spaceState, Vector3 from, Vector3 to)
    {
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1; // Layer 1: World
        var result = spaceState.IntersectRay(query);
        return result.Count > 0;
    }

    #endregion

    #region Acoustic Disturbance & Near-Miss Systems

    /// <summary>
    /// Static entry point called when any thrown store product impacts the floor, wall, or shelves.
    /// Alerts nearby Groombas to investigate or retaliate.
    /// </summary>
    public static void NotifyDisturbance(Product product, Vector3 impactPos, PlayerController thrower)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase != GamePhase.BattleRoyale) return;
        var scene = product.GetTree()?.CurrentScene;
        if (scene == null) return;

        foreach (Node node in product.GetTree().GetNodesInGroup("Groombas"))
        {
            if (node is Groomba groomba && GodotObject.IsInstanceValid(groomba) && !groomba.IsDestroyed)
            {
                groomba.OnDisturbanceHeard(product, impactPos, thrower);
            }
        }
    }

    /// <summary>
    /// Static entry point called every physics tick for high-speed thrown items in flight
    /// to detect close near-misses before impact.
    /// </summary>
    public static void CheckProjectileNearMiss(Product product, Vector3 projPos, Vector3 projVel, PlayerController thrower)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase != GamePhase.BattleRoyale) return;
        var scene = product.GetTree()?.CurrentScene;
        if (scene == null) return;

        foreach (Node node in product.GetTree().GetNodesInGroup("Groombas"))
        {
            if (node is Groomba groomba && GodotObject.IsInstanceValid(groomba) && !groomba.IsDestroyed)
            {
                float dist = groomba.GlobalPosition.DistanceTo(projPos);
                if (dist < 3.0f)
                {
                    groomba.OnProjectileNearMiss(product, projPos, projVel, thrower);
                }
            }
        }
    }

    public void OnDisturbanceHeard(Product product, Vector3 impactPos, PlayerController thrower)
    {
        if (_isDestroyed || !Multiplayer.IsServer()) return;
        if (GameManager.Instance?.CurrentPhase != GamePhase.BattleRoyale) return;

        float distToImpact = GlobalPosition.DistanceTo(impactPos);
        if (distToImpact > HearingRadius) return;

        GD.Print($"[Groomba] Disturbance heard at {impactPos}, dist={distToImpact:F1}m, thrower={thrower?.Name ?? "None"}");

        // 1. Extreme near-miss startle reaction (< 3.5m)
        bool isNearMiss = distToImpact <= 3.5f;
        if (isNearMiss)
        {
            Vector3 evadeDir = (GlobalPosition - impactPos);
            evadeDir.Y = 0;
            if (evadeDir.LengthSquared() > 0.01f)
            {
                Velocity += evadeDir.Normalized() * 3.5f;
            }
        }

        // 2. Check if thrower is in direct line of sight
        bool seesThrower = thrower != null && !thrower.Health.IsDead && CanSeePlayer(thrower, MaxChaseDistance);

        if (seesThrower)
        {
            // Groomba saw who threw it! Enraged engagement!
            Vector3 dirToThrower = (thrower.GlobalPosition - GlobalPosition);
            dirToThrower.Y = 0;
            if (dirToThrower.LengthSquared() > 0.01f)
            {
                LookAt(GlobalPosition + dirToThrower.Normalized(), Vector3.Up);
            }

            _targetPlayer = thrower;
            _lastKnownPlayerPos = thrower.GlobalPosition;
            _isInvestigatingNoise = false;
            _investigateScanTimer = 0f;
            SetState(PatrolEntityState.Attack);

            string[] throwerLines = { "NO LITTERING!", "CLEANING VIOLATION!", "YOU THREW THAT!", "DIRT DETECTED!", "I SAW THAT!", "TARGET ACQUIRED!" };
            string line = throwerLines[GD.Randi() % throwerLines.Length];

            if (Multiplayer.HasMultiplayerPeer())
            {
                Rpc(nameof(RpcShowReaction), "> 皿 <", line, Constants.PATROL_RED);
            }
            else
            {
                RpcShowReaction("> 皿 <", line, Constants.PATROL_RED);
            }
        }
        else
        {
            // Thrower not in direct sight (around a corner/behind shelf) - investigate the crash!
            Vector3 dirToNoise = (impactPos - GlobalPosition);
            dirToNoise.Y = 0;
            if (dirToNoise.LengthSquared() > 0.01f)
            {
                LookAt(GlobalPosition + dirToNoise.Normalized(), Vector3.Up);
            }

            _targetPlayer = null;
            _isInvestigatingNoise = true;
            _noiseInvestigationPos = impactPos;
            _investigateScanTimer = 0f;
            _searchTimer = SearchDuration;
            SetFloorTargetPosition(impactPos);
            SetState(PatrolEntityState.Search);

            string[] noiseLines = isNearMiss
                ? new[] { "WHOA! TOO CLOSE!", "WATCH IT!", "NEAR MISS!", "CLANG?!" }
                : new[] { "WHAT'S THAT?!", "MESS DETECTED!", "CLANG?!", "SUSPICIOUS NOISE!", "SPILL ON AISLE 4!", "CLEANUP REQUIRED!" };
            string line = noiseLines[GD.Randi() % noiseLines.Length];
            string face = isNearMiss ? "O _ O" : "! _ !";

            if (Multiplayer.HasMultiplayerPeer())
            {
                Rpc(nameof(RpcShowReaction), face, line, Constants.PATROL_YELLOW);
            }
            else
            {
                RpcShowReaction(face, line, Constants.PATROL_YELLOW);
            }
        }
    }

    public void OnProjectileNearMiss(Product product, Vector3 projPos, Vector3 projVel, PlayerController thrower)
    {
        if (_isDestroyed || !Multiplayer.IsServer()) return;
        if (_nearMissCooldown > 0f) return;
        _nearMissCooldown = 1.0f;

        // Flinch / evasive impulse away from projectile
        Vector3 evadeDir = (GlobalPosition - projPos);
        evadeDir.Y = 0;
        if (evadeDir.LengthSquared() > 0.01f)
        {
            Velocity += evadeDir.Normalized() * 3.0f;
        }

        string[] dodges = { "WHOA!", "DODGE!", "TOO CLOSE!", "WATCH IT!", "MISSED ME!" };
        string line = dodges[GD.Randi() % dodges.Length];

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcShowReaction), "O _ O", line, new Color(0.3f, 0.85f, 1.0f));
        }
        else
        {
            RpcShowReaction("O _ O", line, new Color(0.3f, 0.85f, 1.0f));
        }

        // Turn towards projectile origin/thrower and engage if visible
        if (thrower != null && !thrower.Health.IsDead && CanSeePlayer(thrower, MaxChaseDistance))
        {
            _targetPlayer = thrower;
            _lastKnownPlayerPos = thrower.GlobalPosition;
            _isInvestigatingNoise = false;
            _investigateScanTimer = 0f;
            SetState(PatrolEntityState.Attack);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcShowReaction(string faceText, string comicText, Color textColor)
    {
        if (_faceDisplay != null && !string.IsNullOrEmpty(faceText))
        {
            _faceDisplay.Text = faceText;
            _faceDisplay.Modulate = textColor;
            _faceResetTimer = 2.2f;
        }

        if (!string.IsNullOrEmpty(comicText))
        {
            FloatingDamageNumber.SpawnText(this, GlobalPosition + Vector3.Up * 0.75f, comicText, textColor, 44);
        }
    }

    #endregion

    #region State Handling

    private void HandlePatrolState(double delta, ref Vector3 velocity)
    {
        DetectPlayer();
        if (_targetPlayer != null && !_targetPlayer.Health.IsDead)
        {
            SetState(PatrolEntityState.Attack);
            return;
        }

        if (NavAgent.IsNavigationFinished())
        {
            SetRandomPatrolTarget();
        }

        MoveAlongPath(PatrolSpeed, delta, ref velocity);
    }

    private void HandleAttackState(double delta, ref Vector3 velocity)
    {
        if (_targetPlayer == null || _targetPlayer.Health == null || _targetPlayer.Health.IsDead)
        {
            _targetPlayer = null;
            SetState(PatrolEntityState.Search);
            return;
        }

        float distToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);
        if (distToPlayer > MaxChaseDistance)
        {
            _lastKnownPlayerPos = _targetPlayer.GlobalPosition;
            _targetPlayer = null;
            SetState(PatrolEntityState.Search);
            return;
        }

        bool hasLineOfSight = CanSeePlayer(_targetPlayer, MaxChaseDistance);
        if (hasLineOfSight)
        {
            _lostSightTimer = 0f;
            _lastKnownPlayerPos = _targetPlayer.GlobalPosition;
            SetFloorTargetPosition(_targetPlayer.GlobalPosition);
        }
        else
        {
            // Player broke line of sight (ducked behind shelf) - rush towards last known position
            _lostSightTimer += (float)delta;
            SetFloorTargetPosition(_lastKnownPlayerPos);

            if (_lostSightTimer > 2.0f || NavAgent.IsNavigationFinished())
            {
                // Lost direct sight - switch to searching the last known area
                _targetPlayer = null;
                _isInvestigatingNoise = true;
                _noiseInvestigationPos = _lastKnownPlayerPos;
                SetState(PatrolEntityState.Search);
                return;
            }
        }

        MoveAlongPath(ChaseSpeed, delta, ref velocity);
    }

    private void HandleSearchState(double delta, ref Vector3 velocity)
    {
        _searchTimer -= (float)delta;

        DetectPlayer();
        if (_targetPlayer != null && !_targetPlayer.Health.IsDead)
        {
            _isInvestigatingNoise = false;
            _investigateScanTimer = 0f;
            SetState(PatrolEntityState.Attack);
            return;
        }

        // Stationary investigation swivel scan at disturbance point
        if (_investigateScanTimer > 0f)
        {
            _investigateScanTimer -= (float)delta;
            velocity.X = 0;
            velocity.Z = 0;

            float swivelOffset = Mathf.Sin((2.4f - _investigateScanTimer) * 4.5f) * Mathf.DegToRad(55f);
            Rotation = new Vector3(Rotation.X, _baseInvestigateYaw + swivelOffset, Rotation.Z);

            if (_investigateScanTimer <= 0f)
            {
                _isInvestigatingNoise = false;
                SetForwardSearchTarget();
            }
            return;
        }

        if (NavAgent.IsNavigationFinished() || (_isInvestigatingNoise && GlobalPosition.DistanceTo(_noiseInvestigationPos) < 1.4f))
        {
            if (_isInvestigatingNoise)
            {
                // Arrived at disturbance spot! Start area inspection
                _investigateScanTimer = 2.4f;
                _baseInvestigateYaw = Rotation.Y;
                if (_faceDisplay != null)
                {
                    _faceDisplay.Text = "ಠ _ ಠ";
                    _faceResetTimer = 2.4f;
                }
                return;
            }
            else
            {
                _lastKnownPlayerPos = Vector3.Zero;
                SetForwardSearchTarget();
            }
        }

        float speed = _isInvestigatingNoise ? InvestigateSpeed : PatrolSpeed;
        MoveAlongPath(speed, delta, ref velocity);

        if (_searchTimer <= 0f)
        {
            _isInvestigatingNoise = false;
            SetState(PatrolEntityState.Patrol);
        }
    }

    private void SetForwardSearchTarget()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        Vector3 forward = -GlobalTransform.Basis.Z;
        forward.Y = 0;
        if (forward.LengthSquared() < 0.001f)
        {
            forward = Vector3.Forward;
        }
        else
        {
            forward = forward.Normalized();
        }

        // Fan out within an arc (-60 to +60 degrees) in the forward direction
        float angle = rng.RandfRange(-Mathf.Pi / 3.0f, Mathf.Pi / 3.0f);
        Vector3 searchDir = forward.Rotated(Vector3.Up, angle);
        float distance = rng.RandfRange(4.0f, 8.0f);

        Vector3 targetPos = GlobalPosition + searchDir * distance;
        targetPos.Y = 0.2f;
        Rid map = NavAgent.GetNavigationMap();
        if (map.IsValid)
        {
            Vector3 closest = NavigationServer3D.MapGetClosestPoint(map, targetPos);
            if (closest.Y <= 0.5f)
            {
                NavAgent.TargetPosition = closest;
                return;
            }
        }
        SetRandomPatrolTarget();
    }

    protected override void TriggerUnstuck()
    {
        base.TriggerUnstuck();
        if (_faceDisplay != null)
        {
            _faceDisplay.Text = "O _ O";
            _faceResetTimer = 0.6f;
        }
    }

    protected override void OnUnstuckComplete()
    {
        base.OnUnstuckComplete();
        if (PatrolState == PatrolEntityState.Search)
        {
            _isInvestigatingNoise = false;
            _investigateScanTimer = 0f;
            SetForwardSearchTarget();
        }
        else if (PatrolState == PatrolEntityState.Attack)
        {
            if (_targetPlayer != null && !CanSeePlayer(_targetPlayer, MaxChaseDistance))
            {
                _targetPlayer = null;
                SetState(PatrolEntityState.Search);
            }
            else if (_targetPlayer != null)
            {
                SetFloorTargetPosition(_targetPlayer.GlobalPosition);
            }
        }
    }

    protected override void OnStateChanged(PatrolEntityState from, PatrolEntityState to)
    {
        base.OnStateChanged(from, to);
        GD.Print($"[Groomba] State changed on Peer {Multiplayer.GetUniqueId()}: {from} -> {to}");

        UpdateRingEmission(to);
        PlayStateSound(to);

        if (Multiplayer.IsServer())
        {
            switch (to)
            {
                case PatrolEntityState.Patrol:
                    _isInvestigatingNoise = false;
                    _investigateScanTimer = 0f;
                    _lostSightTimer = 0f;
                    _lastKnownPlayerPos = Vector3.Zero;
                    SetRandomPatrolTarget();
                    break;

                case PatrolEntityState.Search:
                    _searchTimer = SearchDuration;
                    if (_isInvestigatingNoise && _noiseInvestigationPos != Vector3.Zero)
                    {
                        SetFloorTargetPosition(_noiseInvestigationPos);
                    }
                    else if (_lastKnownPlayerPos != Vector3.Zero)
                    {
                        SetFloorTargetPosition(_lastKnownPlayerPos);
                    }
                    else
                    {
                        SetForwardSearchTarget();
                    }
                    break;

                case PatrolEntityState.Attack:
                    _isInvestigatingNoise = false;
                    _investigateScanTimer = 0f;
                    _lostSightTimer = 0f;
                    break;
            }
        }
    }

    private void PlayStateSound(PatrolEntityState state)
    {
        if (_audioPlayer == null) return;

        switch (state)
        {
            case PatrolEntityState.Patrol:
                if (PatrolStateSound != null)
                {
                    _audioPlayer.Stream = PatrolStateSound;
                    _audioPlayer.VolumeDb = 0f;
                    _audioPlayer.Play();
                }
                break;

            case PatrolEntityState.Search:
                if (SearchStateSound != null)
                {
                    _audioPlayer.Stream = SearchStateSound;
                    _audioPlayer.VolumeDb = 0f;
                    _audioPlayer.Play();
                }
                break;

            case PatrolEntityState.Attack:
                if (AttackStateSound != null)
                {
                    _audioPlayer.Stream = AttackStateSound;
                    _audioPlayer.VolumeDb = -10.0f;
                    _audioPlayer.Play();
                }
                break;
        }
    }

    public override void TakeDamage(int amount, Node3D source = null)
    {
        if (_isDestroyed) return;

        if (!Multiplayer.IsServer())
        {
            if (Multiplayer.HasMultiplayerPeer())
            {
                RpcId(1, nameof(RpcRequestEnemyDamage), amount, source != null ? source.GetPath() : new NodePath());
            }
            return;
        }

        if (Health == null) return;
        Health.TakeDamage(amount);

        if (source is PlayerController playerWhoHit && !playerWhoHit.Health.IsDead)
        {
            _lastKnownPlayerPos = playerWhoHit.GlobalPosition;

            // Retaliate directly if line-of-sight is clear; otherwise hunt down their position!
            if (CanSeePlayer(playerWhoHit, MaxChaseDistance))
            {
                _targetPlayer = playerWhoHit;
                _isInvestigatingNoise = false;
                _investigateScanTimer = 0f;
                SetState(PatrolEntityState.Attack);

                if (Multiplayer.HasMultiplayerPeer())
                {
                    Rpc(nameof(RpcShowReaction), "> 皿 <", "OUCH! DIE!", Constants.PATROL_RED);
                }
                else
                {
                    RpcShowReaction("> 皿 <", "OUCH! DIE!", Constants.PATROL_RED);
                }
            }
            else
            {
                _targetPlayer = null;
                _isInvestigatingNoise = true;
                _noiseInvestigationPos = _lastKnownPlayerPos;
                _investigateScanTimer = 0f;
                SetFloorTargetPosition(_lastKnownPlayerPos);
                _searchTimer = SearchDuration;
                SetState(PatrolEntityState.Search);

                if (Multiplayer.HasMultiplayerPeer())
                {
                    Rpc(nameof(RpcShowReaction), "Ò _ Ó", "WHO HIT ME?!", Constants.PATROL_YELLOW);
                }
                else
                {
                    RpcShowReaction("Ò _ Ó", "WHO HIT ME?!", Constants.PATROL_YELLOW);
                }
            }
        }
        else
        {
            if (_faceDisplay != null)
            {
                _faceDisplay.Text = "> <";
                _faceResetTimer = 0.45f;
            }
        }

        if (Health.IsDead)
        {
            Die();
            return;
        }

        if (Health.CurrentHealth <= Health.MaxHealth / 2 && _smoke != null && !_smoke.Emitting)
        {
            if (Multiplayer.HasMultiplayerPeer())
            {
                Rpc(nameof(RpcSetSmoke), true);
            }
            else
            {
                RpcSetSmoke(true);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcSetSmoke(bool emitting)
    {
        if (_smoke != null)
        {
            _smoke.Emitting = emitting;
        }
    }

    private void HandleContactDamage()
    {
        if (_isDestroyed) return;

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            KinematicCollision3D collision = GetSlideCollision(i);
            if (collision.GetCollider() is PlayerController player && player.Health != null && !player.Health.IsDead && _attackCooldown <= 0f)
            {
                bool wasDeadBefore = player.Health.IsDead;
                player.Health.TakeDamage(ContactDamage);
                _attackCooldown = 1.0f;

                if (!wasDeadBefore && player.Health.IsDead)
                {
                    string[] killLines = { "TRASH DISPOSED!", "SPILL CLEANED!", "RECYCLED!" };
                    string line = killLines[GD.Randi() % killLines.Length];

                    if (Multiplayer.HasMultiplayerPeer())
                    {
                        Rpc(nameof(RpcShowReaction), "˘ ‿ ˘", line, new Color(1.0f, 0.85f, 0.1f));
                    }
                    else
                    {
                        RpcShowReaction("˘ ‿ ˘", line, new Color(1.0f, 0.85f, 0.1f));
                    }
                }

                Die();
                break;
            }
        }
    }

    public override void Die()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;
        if (!Multiplayer.IsServer()) return;

        GD.Print("[Groomba] Destroyed / Self-Destructed!");
        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcDestroyGroomba));
        }
        else
        {
            RpcDestroyGroomba();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcDestroyGroomba()
    {
        _isDestroyed = true;
        if (_smoke != null) _smoke.Emitting = false;
        FloatingDamageNumber.SpawnText(this, GlobalPosition + Vector3.Up * 0.8f, "K.O.!", new Color(1.0f, 0.2f, 0.2f), 54);
        SpawnExplosion();
        EjectSwallowedItems();
        QueueFree();
    }

    private void SpawnExplosion()
    {
        PackedScene scene = ExplosionScene ?? GD.Load<PackedScene>("res://Prefabs/Explosion.tscn");
        if (scene != null)
        {
            Node3D explosion = scene.Instantiate<Node3D>();
            Node parent = GetTree().CurrentScene ?? GetParent();
            if (parent != null)
            {
                parent.AddChild(explosion);
                explosion.GlobalPosition = GlobalPosition;
            }
        }
    }

    #region Ground Objects Vacuum & Dustbin System

    private AudioStreamWav CreateSuctionSound()
    {
        int sampleRate = 22050;
        float duration = 0.28f;
        int sampleCount = (int)(sampleRate * duration);
        byte[] data = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            float freq = Mathf.Lerp(260f, 620f, Mathf.Sin(progress * Mathf.Pi));
            float phase = t * Mathf.Tau * freq;

            float noise = (float)(GD.Randf() * 2.0 - 1.0) * 0.35f;

            float env = progress < 0.2f
                ? (progress / 0.2f)
                : Mathf.Pow(1.0f - progress, 0.75f);

            float tone = (Mathf.Sin(phase) * 0.65f + noise) * env * 0.45f;
            short pcm = (short)Mathf.Clamp(tone * short.MaxValue, short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav();
        wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
        wav.MixRate = sampleRate;
        wav.Data = data;
        return wav;
    }

    private void PlaySuctionSound()
    {
        if (_vacuumAudioPlayer != null && GodotObject.IsInstanceValid(_vacuumAudioPlayer))
        {
            _vacuumAudioPlayer.PitchScale = (float)GD.RandRange(0.92f, 1.08f);
            _vacuumAudioPlayer.Play();
        }
    }

    private void TriggerIntakeFX()
    {
        if (_intakeVortex != null && GodotObject.IsInstanceValid(_intakeVortex))
        {
            _intakeVortex.Restart();
            _intakeVortex.Emitting = true;
        }
    }

    private void CheckGroundObjectsForVacuum()
    {
        if (_isDestroyed) return;

        // Groomba vacuums loose ground objects within VacuumRadius (1.4m)
        var products = GetTree().GetNodesInGroup("Products");
        if (products == null || products.Count == 0) return;

        float maxDistSq = VacuumRadius * VacuumRadius;
        Vector3 myPos = GlobalPosition;

        foreach (Node node in products)
        {
            if (node is not Product product || !GodotObject.IsInstanceValid(product) || product.IsQueuedForDeletion())
                continue;

            // Fast bounding box pre-check
            Vector3 prodPos = product.GlobalPosition;
            if (Mathf.Abs(prodPos.X - myPos.X) > VacuumRadius || Mathf.Abs(prodPos.Z - myPos.Z) > VacuumRadius)
                continue;

            // Must be near floor level (floor is Y ≈ 0m, shelves are Y >= 0.55m)
            if (prodPos.Y > 0.55f || prodPos.Y < -0.5f)
                continue;

            // Do not suck up products currently in hand, inventory, or already in dustbin
            Node parent = product.GetParent();
            if (parent == null || parent.Name == "ItemHand" || parent is PlayerController || parent is Inventory || parent == this)
                continue;

            // Do not suck up active in-flight projectiles moving at high speed
            if (product.Thrower != null && product.LinearVelocity.LengthSquared() > 4.0f)
                continue;

            // Check 2D distance
            float distSq = (prodPos.X - myPos.X) * (prodPos.X - myPos.X) + (prodPos.Z - myPos.Z) * (prodPos.Z - myPos.Z);
            if (distSq <= maxDistSq)
            {
                VacuumProduct(product);
                break; // Vacuum 1 item per scan tick for rapid, rhythmic slurps
            }
        }
    }

    private void VacuumProduct(Product product)
    {
        if (product == null || !GodotObject.IsInstanceValid(product) || product.IsQueuedForDeletion())
            return;

        NodePath path = product.GetPath();
        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcVacuumProduct), path);
        }
        else
        {
            RpcVacuumProduct(path);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcVacuumProduct(NodePath productPath)
    {
        Product product = null;
        if (productPath != null && !productPath.IsEmpty)
        {
            product = GetNodeOrNull<Product>(productPath)
                ?? GetTree()?.Root?.GetNodeOrNull<Product>(productPath)
                ?? GetTree()?.CurrentScene?.GetNodeOrNull<Product>(productPath);
        }

        if (product != null && GodotObject.IsInstanceValid(product) && !product.IsQueuedForDeletion())
        {
            if (!_swallowedProducts.Contains(product))
            {
                _swallowedProducts.Add(product);
            }

            product.OutlineOff();
            product.CollisionLayer = 0;
            product.CollisionMask = 0;
            product.Freeze = true;
            product.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
            product.DeactivatePhysicsAndSync();
            product.Visible = false;
            product.Reparent(this, false);
            product.Position = Vector3.Zero;
        }

        PlaySuctionSound();
        TriggerIntakeFX();

        string[] nomFaces = { "( ˶˘ ³˶ )", "( ᵔ ᵕ ᵔ )", "˘ ᵕ ˘", "( ˵ •̀ ᴗ - ˵ )" };
        string[] nomTexts = { "*SLURP!*", "CLEANED!", "VACUUMED!", "DUSTBIN +1", "*NOM*" };
        string face = nomFaces[GD.Randi() % nomFaces.Length];
        string text = nomTexts[GD.Randi() % nomTexts.Length];

        if (_faceDisplay != null)
        {
            _faceDisplay.Text = face;
            _faceDisplay.Modulate = new Color(0.2f, 0.95f, 0.5f);
            _faceResetTimer = 1.1f;
        }

        FloatingDamageNumber.SpawnText(this, GlobalPosition + Vector3.Up * 0.75f, text, new Color(0.25f, 0.95f, 0.45f), 38);
    }

    private void EjectSwallowedItems()
    {
        Node parent = GetTree().CurrentScene ?? GetParent();
        if (parent == null) return;

        var rng = new RandomNumberGenerator();
        int ejectedCount = 0;

        foreach (Product product in _swallowedProducts)
        {
            if (product != null && GodotObject.IsInstanceValid(product) && !product.IsQueuedForDeletion())
            {
                product.Reparent(parent, true);
                product.GlobalPosition = GlobalPosition + Vector3.Up * (0.35f + rng.RandfRange(0.05f, 0.35f));
                product.Visible = true;
                product.IsForSale = false;
                product.WasBought = true;
                product.CanBePickedUp = true;
                product.Freeze = false;
                product.CollisionLayer = 1;
                product.CollisionMask = 3;
                product.ActivatePhysicsAndSync();

                float speed = rng.RandfRange(3.5f, 6.5f);
                float angle = rng.RandfRange(0f, Mathf.Tau);
                Vector3 launchVel = new Vector3(Mathf.Cos(angle) * speed, rng.RandfRange(4.0f, 7.0f), Mathf.Sin(angle) * speed);
                product.LinearVelocity = launchVel;
                product.AngularVelocity = new Vector3(rng.RandfRange(-6f, 6f), rng.RandfRange(-6f, 6f), rng.RandfRange(-6f, 6f));
                ejectedCount++;
            }
        }
        _swallowedProducts.Clear();

        GD.Print($"[Groomba] Ejected {ejectedCount} items from dustbin upon destruction!");
    }

    #endregion

    #endregion
}
