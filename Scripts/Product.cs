using Godot;

[Tool]
public partial class Product : Area3D
{
    [Export] public ItemData Item;

    public override void _Ready()
    {
        MeshInstance3D meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
        if (Item.Mesh == null)
        {
            GD.PushError(Item.DisplayName + " has no Mesh assigned");
        }
        else
        {
            meshInstance.Mesh = Item.Mesh;
        }
    }
}
