using Godot;

[GlobalClass]
[Tool]
public partial class ItemData : Resource
{
    [Export] public StringName DisplayName;
    [Export] public Mesh Mesh;
    [Export] public int Price;
    [Export] public int Damage;
    [Export] public float Mass;
}
