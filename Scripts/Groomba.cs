using Godot;

public partial class Groomba : PatrolEnemy
{
    [Export] public float SearchDuration = 3.0f;
    private float _searchTimer = 0f;
    private Vector3 _lastKnownPlayerPos = Vector3.Zero;
    [Export] MeshInstance3D RingMesh;
    [Export] public AudioStream PatrolStateSound;
    [Export] public AudioStream AttackStateSound;
    [Export] public AudioStream SearchStateSound;
    [Export] public AudioStream SelfDestructSound;
    [Export] public PackedScene ExplosionScene;
    [Export] private GpuParticles3D _smoke;
    private AudioStreamPlayer3D _audioPlayer;
    private bool _isDestroyed = false;
    StandardMaterial3D RingMat;

    public override void _Ready()
    {
        AddToGroup("PatrolEnemies");
        AddToGroup("Groombas");

        // Wait for first physics frame so Navigation map is synched.
        if (Multiplayer.IsServer())
        {
            Callable.From(SetRandomPatrolTarget).CallDeferred();
        }

        if(IsMultiplayerAuthority())
        {
            GetNode<AudioStreamPlayer3D>("AudioStreamPlayer3D").Play();
        }

        _audioPlayer = new AudioStreamPlayer3D();
        _audioPlayer.UnitSize = 15.0f;
        _audioPlayer.MaxDistance = 40.0f;
        _audioPlayer.VolumeDb = -2.0f;
        _audioPlayer.Bus = "Master";
        AddChild(_audioPlayer);
        
        Material activeMat = RingMesh?.GetActiveMaterial(0) ?? (RingMesh?.Mesh is PrimitiveMesh pm ? pm.Material : null);
        if (activeMat is StandardMaterial3D material)
        {
            RingMat = (StandardMaterial3D)material.Duplicate();
            RingMesh.SetSurfaceOverrideMaterial(0, RingMat);
            RingMesh.MaterialOverride = RingMat;
        }

        UpdateRingEmission(PatrolState);

        SyncPosition = GlobalPosition;
        SyncRotation = Rotation;
    }

    public void UpdateRingEmission(PatrolEntityState state)
    {
        if (RingMat == null) return;
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

    public override void _Process(double delta)
    {
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
        if(!Multiplayer.IsServer()) return;
        if(GameManager.Instance?.CurrentPhase != GamePhase.BattleRoyale) return;
        if(_attackCooldown > 0f) _attackCooldown -= (float)delta;

        Vector3 velocity = Velocity;
        if(!IsOnFloor())
        {
            velocity.Y -= Gravity * (float)delta;
        }

        switch (PatrolState)
        {
            case PatrolEntityState.Patrol:
                HandlePatrolState(ref velocity);
                break;

            case PatrolEntityState.Attack:
                HandleAttackState(ref velocity);
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

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncTransform), SyncPosition, SyncRotation);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void RpcSyncTransform(Vector3 position, Vector3 rotation)
    {
        SyncPosition = position;
        SyncRotation = rotation;
    }

    private void HandlePatrolState(ref Vector3 velocity)
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

        MoveAlongPath(PatrolSpeed, ref velocity);
    }

    private void HandleAttackState(ref Vector3 velocity)
    {
        if (_targetPlayer == null || _targetPlayer.Health.IsDead)
        {
            _targetPlayer = null;
            SetState(PatrolEntityState.Search);
            return;
        }

        float distToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);
        if (distToPlayer > DetectionRange * 1.5f)
        {
            _lastKnownPlayerPos = _targetPlayer.GlobalPosition;
            _targetPlayer = null;
            SetState(PatrolEntityState.Search);
            return;
        }

        _lastKnownPlayerPos = _targetPlayer.GlobalPosition;
        NavAgent.TargetPosition = _targetPlayer.GlobalPosition;
        MoveAlongPath(ChaseSpeed, ref velocity);
    }

    public override void TakeDamage(int amount, Node3D source = null)
    {
        if (_isDestroyed) return;

        if (source is PlayerController playerWhoHit && !playerWhoHit.Health.IsDead)
        {
            _lastKnownPlayerPos = playerWhoHit.GlobalPosition;
            if (PatrolState == PatrolEntityState.Search)
            {
                NavAgent.TargetPosition = _lastKnownPlayerPos;
                _searchTimer = SearchDuration;
            }
        }

        if (Health != null && Health.CurrentHealth <= Health.MaxHealth / 2 && _smoke != null && !_smoke.Emitting)
        {
            _smoke.Emitting = true;
        }
        base.TakeDamage(amount, source);
    }

    private void HandleSearchState(double delta, ref Vector3 velocity)
    {
        _searchTimer -= (float)delta;

        DetectPlayer();
        if (_targetPlayer != null && !_targetPlayer.Health.IsDead)
        {
            SetState(PatrolEntityState.Attack);
            return;
        }

        if (NavAgent.IsNavigationFinished())
        {
            _lastKnownPlayerPos = Vector3.Zero;
            SetForwardSearchTarget();
        }

        MoveAlongPath(PatrolSpeed, ref velocity);

        if (_searchTimer <= 0f)
        {
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
        Rid map = NavAgent.GetNavigationMap();
        NavAgent.TargetPosition = NavigationServer3D.MapGetClosestPoint(map, targetPos);
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
                    _lastKnownPlayerPos = Vector3.Zero;
                    SetRandomPatrolTarget();
                    break;

                case PatrolEntityState.Search:
                    _searchTimer = SearchDuration;
                    if (_lastKnownPlayerPos != Vector3.Zero)
                    {
                        NavAgent.TargetPosition = _lastKnownPlayerPos;
                    }
                    else
                    {
                        SetForwardSearchTarget();
                    }
                    break;

                case PatrolEntityState.Attack:
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

    private void HandleContactDamage()
    {
        if (_isDestroyed) return;

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            KinematicCollision3D collision = GetSlideCollision(i);
            if (collision.GetCollider() is PlayerController player && player.Health != null && !player.Health.IsDead && _attackCooldown <= 0f)
            {
                player.Health.TakeDamage(ContactDamage);
                _attackCooldown = 1.0f;
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
        SpawnExplosion();
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
}