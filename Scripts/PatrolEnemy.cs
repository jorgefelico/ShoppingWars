using Godot;

public abstract partial class PatrolEnemy : CharacterBody3D, IDamageable, IPatrol
{
    [Export] public NavigationAgent3D NavAgent { get; set; }
    [Export] public float ArenaBounds { get; set; } = 35.0f;
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
            velocity.X = dir.Normalized().X * speed;
            velocity.Z = dir.Normalized().Z * speed;
        }
    }

    public virtual void SetRandomPatrolTarget()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        Vector3 randomTarget = new Vector3(
            rng.RandfRange(-ArenaBounds, ArenaBounds),
            GlobalPosition.Y,
            rng.RandfRange(-ArenaBounds, ArenaBounds)
        );
        NavAgent.TargetPosition = randomTarget;
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

    public void TakeDamage(int amount, Node3D source = null)
    {
        if(Health == null) return;
        Health.TakeDamage(amount);

        if(source is PlayerController playerWhoHit && !playerWhoHit.Health.IsDead)
        {
            _targetPlayer = playerWhoHit;
            NavAgent.TargetPosition = _targetPlayer.GlobalPosition;
        }

        if(Health.IsDead)
        {
            Die();
        }
    }
}