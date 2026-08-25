using Godot;

public partial class Product : RigidBody3D
{
    [Export] public StringName DisplayName;
    [Export] public int Price;
    [Export] public int Damage;
    [Export] public bool ScaleVariation = false;

    public override void _Ready()
    {
        if (ScaleVariation)
        {
            RandomNumberGenerator rand = new RandomNumberGenerator();
            Scale = Vector3.One * rand.RandfRange(1f, 1.15f);
        }
    }

    public void PickedUp()
    {
        SetDeferred(RigidBody3D.PropertyName.ProcessMode, (int)ProcessModeEnum.Disabled);
    }

    public void Dropped()
    {
        Node sceneRoot = GetTree().CurrentScene;
        Reparent(sceneRoot);
        SetDeferred(RigidBody3D.PropertyName.ProcessMode, (int)ProcessModeEnum.Pausable);
    }

    public void Throw()
    {
        SetDeferred(RigidBody3D.PropertyName.ProcessMode, (int)ProcessModeEnum.Pausable);
        GD.Print("Throwing " + DisplayName);
    }
}
