using System.Linq;
using System.Net.NetworkInformation;
using Godot;

public partial class Groomba : CharacterBody3D, IDamageable
{
    [Export] private NavigationAgent3D NavAgent;
    [Export] private float PatrolSpeed = 2.5f;
    [Export] private float ChaseSpeed = 5.0f;
    [Export] private float DetectionRange = 8.0f;
    [Export] private int ContactDamage = 15;
    [Export] private float ArenaBounds = 35.0f;
    [Export] public Health Health;

    private PlayerController _targetPlayer;
    private float _attackCooldown = 0f;
    private const float Gravity = 9.8f;

    public override void _Ready()
    {
        // Wait for first physics frame so Navigation map is synched.
        Callable.From(SetRandomPatrolTarget).CallDeferred();
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

        DetectPlayer();

        // Chase State
        if(_targetPlayer != null && !_targetPlayer.Health.IsDead)
        {
            NavAgent.TargetPosition = _targetPlayer.GlobalPosition;
            MoveAlongPath(ChaseSpeed, ref velocity);
        } else
        {
        // Patrol State
            if(NavAgent.IsNavigationFinished())
            {
                SetRandomPatrolTarget();
            }
            MoveAlongPath(PatrolSpeed, ref velocity);
        }

        Velocity = velocity;
        MoveAndSlide();
        HandleContactDamage();
    }

    private void MoveAlongPath(float speed, ref Vector3 velocity)
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

        if(dir.LengthSquared() > 0.01f)
        {
            LookAt(GlobalPosition + dir, Vector3.Up);
            velocity.X = dir.Normalized().X * speed;
            velocity.Z = dir.Normalized().Z * speed;
        }
    }

    private void SetRandomPatrolTarget()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        Vector3 randomTarget = new Vector3(
            rng.RandfRange(-ArenaBounds, ArenaBounds),
            GlobalPosition.Y,
            rng.RandfRange(-ArenaBounds, ArenaBounds)
        );
        NavAgent.TargetPosition = randomTarget;
    }

    private void DetectPlayer()
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

    public void TakeDamage(int amount)
    {
        if(Health == null) return;
        Health.TakeDamage(amount);
        if(Health.IsDead)
        {
            Die();
        }
    }

    private void Die()
    {
        GD.Print("[Groomba] Destroyed!");
        QueueFree();
    }
}