using Godot;

public partial class ReadyUp : StaticBody3D, IInteractable
{
    public string HoverText { get; set; } = "Press E";
    [Export] private MeshInstance3D ButtonMesh;

    public MeshInstance3D Outline { get; set; }

    public override void _Ready()
    {
        if (ButtonMesh != null)
        {
            Outline = new MeshInstance3D
            {
                Mesh = ButtonMesh.Mesh,
                Visible = false,
            };
            ShaderMaterial outlineMaterial = new()
            {
                Shader = GD.Load<Shader>("res://Shaders/outline.gdshader"),
            };
            for (int i = 0; i < ButtonMesh.GetSurfaceOverrideMaterialCount(); i++)
            {
                Outline.SetSurfaceOverrideMaterial(i, outlineMaterial);
            }
            ButtonMesh.AddChild(Outline);
        }
    }


    public void Interact(PlayerController player)
    {
        if (Multiplayer.IsServer() && GameManager.Instance?.CurrentPhase == GamePhase.Lobby)
        {
            GameManager.Instance?.StartShoppingPhase();
            if (ButtonMesh.GetActiveMaterial(0) is StandardMaterial3D material)
            {
                // 3. Set the Albedo color (Red in this example)
                material.AlbedoColor = new Color(1, 0, 0);
                material.Emission = new Color(1, 0, 0);
            }
        }
    }
}
