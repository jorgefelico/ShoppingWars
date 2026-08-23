using Godot;

public partial class Product : Node3D
{
    [Export] public StringName DisplayName;
    [Export] public int Price;
    [Export] public int Damage;
    [Export] public float Mass;

    private RigidBody3D Body;

    public override void _Ready()
    {
        Body = FindRigidBody(this);
        if(Body != null && Mass > 0f)
        {
            GD.Print("Name: " + Body.Name);
            Body.Mass = Mass;
        }
    }

    private static RigidBody3D FindRigidBody(Node node)
    {
        if(node is RigidBody3D body)
        {
            return body;
        }

        foreach (Node child in node.GetChildren())
        {
            RigidBody3D found = FindRigidBody(child); 
            if(found != null)
            {
                return found;
            }
        }
        return null;
    }
}
