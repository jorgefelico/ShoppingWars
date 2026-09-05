using Godot;

public partial class Product : RigidBody3D, IInteractable
{
    [Export] public StringName DisplayName;
    [Export] public int Price;
    [Export] public int Damage;
    [Export] public bool ScaleVariation = false;
    [Export] public Texture2D Icon;
    [Export] float MinDamageSpeed = 12f;
    [Export] public float ThrowMultiplier = 1.0f;
    [Export] public bool IsForSale = true;
    public bool CanBePickedUp = true;
    public bool WasBought = false;
    public string HoverText { get; set; } = "Buy";
    public Node3D Thrower;
    public MeshInstance3D Outline { get; set; }
    Vector3 _lastVelocity;
    public Label3D HoverLabel { get; set; }
    private MeshInstance3D mesh;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        GameManager.Instance.GamePhaseChanged += OnGamePhaseChanged;

        if (ScaleVariation)
        {
            RandomNumberGenerator rand = new RandomNumberGenerator();
            Scale = Vector3.One * rand.RandfRange(1f, 1.15f);
        }

        HoverText = $"{HoverText} ${Price}";

       

        mesh = Utils.FindMeshInstance(this);
        
        HoverLabel = Utils.CreateHoverLabel(HoverText);
        AddChild(HoverLabel);

        if (mesh != null)
        {
            Outline = new MeshInstance3D
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
                Outline.SetSurfaceOverrideMaterial(i, outlineMaterial);
            }
            mesh.AddChild(Outline);
        }

    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority())
        {
            if (LinearVelocity.LengthSquared() > 0.1f)
            {
                Freeze = false;
            }
            return;
        }

        _lastVelocity = LinearVelocity;
    }

    private void OnBodyEntered(Node body)
    {
        if (_lastVelocity.Length() < MinDamageSpeed) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase != GamePhase.BattleRoyale) return;

        // Ignore hitting the thrower!
        if (body == Thrower) return;

        if (body is IDamageable target) target.TakeDamage(Damage, Thrower);
    }

    public void Interact(PlayerController player)
    {
        if (GlobalPosition.DistanceTo(player.GlobalPosition) <= player.PickUpRange && GameManager.Instance.CurrentPhase != GamePhase.Lobby)
        {
            if (player.Inventory.IsInventoryFull()) return;
            if(GameManager.Instance?.CurrentPhase == GamePhase.BattleRoyale && !CanBePickedUp) return;
            if (IsForSale && GameManager.Instance?.CurrentPhase == GamePhase.Shopping)
            {
                if (!player.TryDeductMoney(Price))
                {
                    GD.Print($"[Store] Cannot afford {DisplayName}! Costs ${Price}, you have ${player.Money}");
                    return;
                }
            }

            IsForSale = false;
            WasBought = true;

            if (player.HeldItem != null)
            {
                player.HeldItem.Visible = false;
            }
            
            if(HoverLabel != null) HoverLabel.Visible = false;

            player.Rpc(nameof(player.RPCPickupItem), GetPath());
        }
    }

    private void OnGamePhaseChanged()
    {
       if(GameManager.Instance?.CurrentPhase != GamePhase.BattleRoyale) return;
        if(!WasBought) {
            CanBePickedUp = false;
            Outline?.QueueFree();
            Outline = null;
        }
        
        HoverLabel?.QueueFree();
        HoverLabel = null;
    }

    public override void _ExitTree()
    {
        if(GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        }
    }

}
