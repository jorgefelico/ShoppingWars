using Godot;

public partial class Groomba : PatrolEnemy
{
    [Export] public float SearchDuration = 3.0f;
    private float _searchTimer = 0f;
    private Vector3 _lastKnownPlayerPos = Vector3.Zero;
    [Export] MeshInstance3D RingMesh;
    StandardMaterial3D RingMat;

    public override void _Ready()
    {
        // Wait for first physics frame so Navigation map is synched.
        Callable.From(SetRandomPatrolTarget).CallDeferred();

        if(RingMesh != null && RingMesh.GetActiveMaterial(0) is StandardMaterial3D material)
        {
            RingMat = material;
        }
    }

    public override void _Process(double delta)
    {
        if (Multiplayer.IsServer()) return;

        if (SyncPosition != Vector3.Zero)
        {
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

    private void HandleSearchState(double delta, ref Vector3 velocity)
    {
        _searchTimer -= (float)delta;

        DetectPlayer();
        if (_targetPlayer != null && !_targetPlayer.Health.IsDead)
        {
            SetState(PatrolEntityState.Attack);
            return;
        }

        if (!NavAgent.IsNavigationFinished())
        {
            MoveAlongPath(PatrolSpeed, ref velocity);
        }
        else
        {
            velocity.X = 0;
            velocity.Z = 0;
        }

        if (_searchTimer <= 0f)
        {
            SetState(PatrolEntityState.Patrol);
        }
    }

    protected override void OnStateChanged(PatrolEntityState from, PatrolEntityState to)
    {
        base.OnStateChanged(from, to);
        GD.Print($"[Groomba] State changed: {from} -> {to}");

        switch (to)
        {
            case PatrolEntityState.Patrol:
                SetRandomPatrolTarget();
                RingMat.Emission = Constants.PATROL_GREEN;
                break;

            case PatrolEntityState.Attack:
                RingMat.Emission = Constants.PATROL_RED;
                break;

            case PatrolEntityState.Search:
                _searchTimer = SearchDuration;
                if (_lastKnownPlayerPos != Vector3.Zero)
                {
                    RingMat.Emission = Constants.PATROL_YELLOW;
                    NavAgent.TargetPosition = _lastKnownPlayerPos;
                }
                break;
        }
    }

    private void HandleContactDamage()
    {
        for(int i = 0; i < GetSlideCollisionCount(); i++)
        {
            KinematicCollision3D collision = GetSlideCollision(i);
            if(collision.GetCollider() is PlayerController player && _attackCooldown <= 0f)
            {
                player.Health.TakeDamage(ContactDamage);
                _attackCooldown = 1.0f;
            }
        }
    }

    public override void Die()
    {
        if(!Multiplayer.IsServer()) return;
        GD.Print("[Groomba] Destroyed!");
        Rpc(nameof(RpcDestroyGroomba));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer,CallLocal = true)]
    public void RpcDestroyGroomba()
    {
        QueueFree();
    }
}