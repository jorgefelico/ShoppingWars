using System.Diagnostics;
using Godot;

public partial class Groomba : PatrolEnemy
{
    public override void _Ready()
    {
        // Wait for first physics frame so Navigation map is synched.
        Callable.From(SetRandomPatrolTarget).CallDeferred();
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

        SyncPosition = GlobalPosition;
        SyncRotation = Rotation;
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