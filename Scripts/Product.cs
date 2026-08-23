using Godot;

public partial class Product : Node3D
{
    [Export] public StringName DisplayName;
    [Export] public int Price;
    [Export] public int Damage;
    [Export] public float Mass;

    public override void _Ready()
    {
       
    }
}
