using Godot;

public enum PatrolEntityState
{
    Patrol,
    Search,
    Attack
}

public interface IPatrol
{
    NavigationAgent3D NavAgent{get; set;}
    PatrolEntityState PatrolState { get; set; }
    float ArenaBounds{get; set;}

    void MoveAlongPath(float speed, ref Vector3 velocity)
    {
        if(this is not Node3D node) return;
        if (NavAgent.IsNavigationFinished())
        {
            velocity.X = 0;
            velocity.Z = 0;
            return;
        }

        Vector3 nextPathPos = NavAgent.GetNextPathPosition();
        Vector3 dir = nextPathPos - node.GlobalPosition;
        dir.Y = 0;

        if (dir.LengthSquared() > 0.01f)
        {
            node.LookAt(node.GlobalPosition + dir, Vector3.Up);
            velocity.X = dir.Normalized().X * speed;
            velocity.Z = dir.Normalized().Z * speed;
        }
    }

    void SetRandomPatrolTarget()
    {
        if(this is not Node3D node) return;
        RandomNumberGenerator rng = new RandomNumberGenerator();
        Vector3 randomTarget = new Vector3(
            rng.RandfRange(-ArenaBounds, ArenaBounds),
            node.GlobalPosition.Y,
            rng.RandfRange(-ArenaBounds, ArenaBounds)
        );
        NavAgent.TargetPosition = randomTarget;
    }
    void DetectPlayer();
}