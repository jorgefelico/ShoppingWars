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

    public void SetSpeedMultiplier(float multiplier)
    {
        SpeedMultiplier = multiplier;
    }

    public virtual void MoveAlongPath(float speed, ref Vector3 velocity)
    {
        if (NavAgent.IsNavigationFinished())
        {
            velocity.X = 0;
            velocity.Z = 0;
            return;
        }

        Vector3 nextPathPos = NavAgent.GetNextPathPosition();
        Vector3 dir = nextPathPos - GlobalPosition;
        dir.Y = 0;

        if (dir.LengthSquared() > 0.01f)
        {
            LookAt(GlobalPosition + dir, Vector3.Up);
            velocity.X = dir.Normalized().X * speed * SpeedMultiplier;
            velocity.Z = dir.Normalized().Z * speed * SpeedMultiplier;
        }
    }

    public virtual void SetRandomPatrolTarget()
    {
        Rid map = NavAgent.GetNavigationMap();
        Vector3 randomPoint = NavigationServer3D.MapGetRandomPoint(map, NavAgent.NavigationLayers, false);
        NavAgent.TargetPosition = randomPoint;
    }

    public void DetectPlayer()
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
            NavAgent.TargetPosition = _targetPlayer.GlobalPosition;
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