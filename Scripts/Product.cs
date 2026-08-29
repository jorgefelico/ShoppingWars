using Godot;

public partial class Product : RigidBody3D
{
    [Export] public StringName DisplayName;
    [Export] public int Price;
    [Export] public int Damage;
    [Export] public bool ScaleVariation = false;
    [Export] public Texture2D Icon;
    [Export] float MinDamageSpeed = 12f;
    MeshInstance3D _outline;
    Vector3 _lastVelocity;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        if (ScaleVariation)
        {
            RandomNumberGenerator rand = new RandomNumberGenerator();
            Scale = Vector3.One * rand.RandfRange(1f, 1.15f);
        }

        MeshInstance3D mesh = FindMeshInstance(this);
        if (mesh != null)
        {
            _outline = new MeshInstance3D
            {
                Mesh = mesh.Mesh,
                Visible = false,
            };
            ShaderMaterial outlineMaterial = new()
            {
                Shader = GD.Load<Shader>("res://Shaders/outline.gdshader"),
            };
            for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
            {
                _outline.SetSurfaceOverrideMaterial(i, outlineMaterial);
            }
            mesh.AddChild(_outline);
        }

    }

    public override void _PhysicsProcess(double delta)
    {
        _lastVelocity = LinearVelocity;
    }

    static MeshInstance3D FindMeshInstance(Node node)
    {
        if (node is MeshInstance3D mi)
            return mi;
        foreach (Node child in node.GetChildren())
        {
            MeshInstance3D found = FindMeshInstance(child);
            if (found != null)
                return found;
        }
        return null;
    }

    public void OutlineOn()
    {
        _outline.Visible = true;
    }

    public void OutlineOff()
    {
        _outline.Visible = false;
    }

    private void OnBodyEntered(Node body)
    {
        if (_lastVelocity.Length() < MinDamageSpeed) return;
        
        if(GameManager.Instance != null && GameManager.Instance.CurrentPhase != GamePhase.BattleRoyale) return;

        if (body is PlayerController player)
        {
            player.Health.TakeDamage(Damage);
        }
    }
}
