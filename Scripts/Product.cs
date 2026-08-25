using Godot;

public partial class Product : RigidBody3D
{
    [Export] public StringName DisplayName;
    [Export] public int Price;
    [Export] public int Damage;
    [Export] public bool ScaleVariation = false;
    private RigidBody3D Body;

    public override void _Ready()
    {
        if (ScaleVariation)
        {
            RandomNumberGenerator rand = new RandomNumberGenerator();
            Scale = Vector3.One * rand.RandfRange(1f, 1.15f);
        }
    }

    private static RigidBody3D FindRigidBody(Node node)
    {
        if (node is RigidBody3D body)
        {
            return body;
        }

        foreach (Node child in node.GetChildren())
        {
            RigidBody3D found = FindRigidBody(child);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
