using Godot;

public abstract partial class PatrolEnemy : CharacterBody3D, IDamageable, IPatrol
{
    [Export] public NavigationAgent3D NavAgent { get; set; }
    [Export] public Health Health;
    protected PlayerController _targetPlayer;
    protected float _attackCooldown = 0f;
    [Export] protected float PatrolSpeed = 2.5f;
    [Export] protected float ChaseSpeed = 5.0f;
    [Export] protected float DetectionRange = 8.0f;
    [Export] protected int ContactDamage = 15;
    [Export] public Vector3 SyncPosition = Vector3.Zero;
    [Export] public Vector3 SyncRotation = Vector3.Zero;
    
    protected const float Gravity = 9.8f;
    public PatrolEntityState PatrolState {get; set;} = PatrolEntityState.Patrol;
    public float SpeedMultiplier { get; set; } = 1.0f;

    // Stuck detection and corner recovery
    protected float _stuckTimer = 0f;
    protected float _unstuckTimer = 0f;
    protected Vector3 _unstuckDir = Vector3.Zero;
    protected Vector3 _lastStuckPos = Vector3.Zero;
    protected float _stuckPosCheckTimer = 0f;
    protected const float StuckCheckInterval = 0.25f;
    protected const float StuckMinDistance = 0.04f;
    protected const float StuckThresholdTime = 0.55f;

    public override void _Ready()
    {
        base._Ready();
        ConfigureNavigationAgent();
        _lastStuckPos = GlobalPosition;
    }

    public void ConfigureNavigationAgent()
    {
        if (NavAgent == null) NavAgent = GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        if (NavAgent != null)
        {
            NavAgent.PathDesiredDistance = 0.45f;
            NavAgent.TargetDesiredDistance = 0.8f;
            NavAgent.PathMaxDistance = 3.0f;
            NavAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.Edgecentered;
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        SpeedMultiplier = multiplier;
    }

    public virtual void SetFloorTargetPosition(Vector3 targetPos)
    {
        if (NavAgent == null) return;
        Vector3 floorPos = new Vector3(targetPos.X, 0.2f, targetPos.Z);
        Rid map = NavAgent.GetNavigationMap();
        if (map.IsValid)
        {
            Vector3 closest = NavigationServer3D.MapGetClosestPoint(map, floorPos);
            if (closest.Y <= 0.5f)
            {
                NavAgent.TargetPosition = closest;
                return;
            }
        }
        NavAgent.TargetPosition = floorPos;
    }

    public virtual void MoveAlongPath(float speed, ref Vector3 velocity)
    {
        MoveAlongPath(speed, GetPhysicsProcessDeltaTime(), ref velocity);
    }

    public virtual void MoveAlongPath(float speed, double delta, ref Vector3 velocity)
    {
        if (NavAgent == null) return;

        // Active unstuck maneuver (backing out and turning away from obstacle)
        if (_unstuckTimer > 0f)
        {
            _unstuckTimer -= (float)delta;
            velocity.X = _unstuckDir.X * (speed * 0.9f) * SpeedMultiplier;
            velocity.Z = _unstuckDir.Z * (speed * 0.9f) * SpeedMultiplier;

            if (_unstuckDir.LengthSquared() > 0.01f)
            {
                float targetAngle = Mathf.Atan2(-_unstuckDir.X, -_unstuckDir.Z);
                Rotation = new Vector3(Rotation.X, Mathf.LerpAngle(Rotation.Y, targetAngle, (float)delta * 8.0f), Rotation.Z);
            }

            if (_unstuckTimer <= 0f)
            {
                _stuckTimer = 0f;
                _lastStuckPos = GlobalPosition;
                OnUnstuckComplete();
            }
            return;
        }

        if (NavAgent.IsNavigationFinished())
        {
            velocity.X = 0;
            velocity.Z = 0;
            _stuckTimer = 0f;
            return;
        }

        Vector3 nextPathPos = NavAgent.GetNextPathPosition();
        Vector3 dir = nextPathPos - GlobalPosition;
        dir.Y = 0;

        if (dir.LengthSquared() > 0.001f)
        {
            dir = dir.Normalized();

            // Wall and corner sliding deflection
            int colCount = GetSlideCollisionCount();
            for (int i = 0; i < colCount; i++)
            {
                KinematicCollision3D col = GetSlideCollision(i);
                Vector3 normal = col.GetNormal();
                if (normal.Y < 0.4f)
                {
                    normal.Y = 0;
                    if (normal.LengthSquared() > 0.01f)
                    {
                        normal = normal.Normalized();
                        float dot = dir.Dot(normal);
                        if (dot < 0f)
                        {
                            Vector3 slideDir = dir - dot * normal;
                            if (slideDir.LengthSquared() > 0.05f)
                            {
                                dir = slideDir.Normalized();
                            }
                        }
                    }
                }
            }

            // Smooth rotation towards travel direction
            float targetAngle = Mathf.Atan2(-dir.X, -dir.Z);
            Rotation = new Vector3(Rotation.X, Mathf.LerpAngle(Rotation.Y, targetAngle, (float)delta * 12.0f), Rotation.Z);

            velocity.X = dir.X * speed * SpeedMultiplier;
            velocity.Z = dir.Z * speed * SpeedMultiplier;

            // Stuck detection
            _stuckPosCheckTimer += (float)delta;
            if (_stuckPosCheckTimer >= StuckCheckInterval)
            {
                _stuckPosCheckTimer = 0f;
                float distMoved = GlobalPosition.DistanceTo(_lastStuckPos);
                if (distMoved < StuckMinDistance)
                {
                    _stuckTimer += StuckCheckInterval;
                }
                else
                {
                    _stuckTimer = Mathf.Max(0f, _stuckTimer - StuckCheckInterval * 1.5f);
                }
                _lastStuckPos = GlobalPosition;

                if (_stuckTimer >= StuckThresholdTime)
                {
                    TriggerUnstuck();
                }
            }
        }
        else
        {
            velocity.X = 0;
            velocity.Z = 0;
        }
    }

    protected virtual void TriggerUnstuck()
    {
        _stuckTimer = 0f;
        _unstuckTimer = 0.4f;

        Vector3 escapeDir = Vector3.Zero;
        int colCount = GetSlideCollisionCount();
        for (int i = 0; i < colCount; i++)
        {
            KinematicCollision3D col = GetSlideCollision(i);
            Vector3 normal = col.GetNormal();
            if (normal.Y < 0.4f)
            {
                normal.Y = 0;
                escapeDir += normal;
            }
        }

        if (escapeDir.LengthSquared() > 0.01f)
        {
            _unstuckDir = (escapeDir.Normalized() * 0.7f + GlobalTransform.Basis.Z * 0.5f).Normalized();
        }
        else
        {
            _unstuckDir = GlobalTransform.Basis.Z;
            _unstuckDir.Y = 0;
            if (_unstuckDir.LengthSquared() > 0.01f) _unstuckDir = _unstuckDir.Normalized();
            else _unstuckDir = Vector3.Back;
        }
    }

    protected virtual void OnUnstuckComplete()
    {
        if (PatrolState == PatrolEntityState.Patrol)
        {
            SetRandomPatrolTarget();
        }
    }

    public virtual void SetRandomPatrolTarget()
    {
        if (NavAgent == null) return;
        Rid map = NavAgent.GetNavigationMap();
        if (!map.IsValid) return;

        // Try up to 15 times to find a valid floor-level point (reject shelf tops, table tops, ceiling rafters)
        for (int attempt = 0; attempt < 15; attempt++)
        {
            Vector3 candidate = NavigationServer3D.MapGetRandomPoint(map, NavAgent.NavigationLayers, false);
            // Ground floor in the supermarket is Y = 0.0 to 0.35m
            if (candidate.Y >= -0.2f && candidate.Y <= 0.5f)
            {
                NavAgent.TargetPosition = candidate;
                return;
            }
        }

        // Fallback: project a random candidate to floor level
        Vector3 fallback = NavigationServer3D.MapGetRandomPoint(map, NavAgent.NavigationLayers, false);
        fallback.Y = 0.2f;
        Vector3 closest = NavigationServer3D.MapGetClosestPoint(map, fallback);
        if (closest.Y <= 0.5f)
        {
            NavAgent.TargetPosition = closest;
        }
    }

    public virtual void DetectPlayer()
    {
        _targetPlayer = null;
        float closestDistance = DetectionRange;

        foreach (Node node in GetTree().GetNodesInGroup("Players"))
        {
            if (node is PlayerController player && !player.Health.IsDead)
            {
                float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
                if (dist <= closestDistance)
                {
                    closestDistance = dist;
                    _targetPlayer = player;
                }
            }
        }
    }

    public abstract void Die();

    public virtual void TakeDamage(int amount, Node3D source = null)
    {
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
            _targetPlayer = playerWhoHit;
            SetFloorTargetPosition(_targetPlayer.GlobalPosition);
            SetState(PatrolEntityState.Search);
        }

        if (Health.IsDead)
        {
            Die();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void RpcRequestEnemyDamage(int amount, NodePath sourcePath)
    {
        if (!Multiplayer.IsServer()) return;
        Node3D source = !sourcePath.IsEmpty ? GetNodeOrNull<Node3D>(sourcePath) : null;
        TakeDamage(amount, source);
    }

    public virtual void SetState(PatrolEntityState newState)
    {
        if (PatrolState == newState) return;
        PatrolEntityState oldState = PatrolState;
        PatrolState = newState;
        OnStateChanged(oldState, newState);

        if (Multiplayer.IsServer() && Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncPatrolState), (int)newState);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void RpcSyncPatrolState(int stateIndex)
    {
        PatrolEntityState newState = (PatrolEntityState)stateIndex;
        if (PatrolState == newState) return;
        PatrolEntityState oldState = PatrolState;
        PatrolState = newState;
        OnStateChanged(oldState, newState);
    }

    protected virtual void OnStateChanged(PatrolEntityState from, PatrolEntityState to) { }
}