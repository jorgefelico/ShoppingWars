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
    [Export] public PackedScene ImpactEffect;
    [Export] public bool DestroyOnImpact = true;
    [Export] public bool IsConsumable = false;
    [Export] public int HealAmount = 0;
    [Export] public int MeleeDurability = 4;
    public int CurrentDurability = 4;
    public bool CanBePickedUp = true;
    public bool WasBought = false;
    public string HoverText { get; set; } = "Buy";
    public Node3D Thrower;
    public MeshInstance3D Outline { get; set; }
    Vector3 _lastVelocity;
    public Label3D HoverLabel { get; set; }
    private MeshInstance3D mesh;
    private bool _hasImpacted = false;

    public void ResetImpact()
    {
        _hasImpacted = false;
    }

    public override void _Ready()
    {
        // Freeze physics on start so products stay safely in place on shelves without rolling off
        Freeze = true;
        FreezeMode = FreezeModeEnum.Static;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;

        BodyEntered += OnBodyEntered;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged += OnGamePhaseChanged;
        }

        if (ImpactEffect == null)
        {
            if (DisplayName == "Watermelon" || Name.ToString().Contains("Watermelon"))
            {
                if (ResourceLoader.Exists("res://Prefabs/WatermelonSplatter.tscn") || FileAccess.FileExists("res://Prefabs/WatermelonSplatter.tscn"))
                {
                    ImpactEffect = GD.Load<PackedScene>("res://Prefabs/WatermelonSplatter.tscn");
                }
            }
            else if (IsFruitProduct())
            {
                if (ResourceLoader.Exists("res://Prefabs/FruitSplatter.tscn") || FileAccess.FileExists("res://Prefabs/FruitSplatter.tscn"))
                {
                    ImpactEffect = GD.Load<PackedScene>("res://Prefabs/FruitSplatter.tscn");
                }
            }
        }

        if (ScaleVariation)
        {
            RandomNumberGenerator rand = new RandomNumberGenerator();
            Scale = Vector3.One * rand.RandfRange(1f, 1.15f);
        }
        HoverText = $"{HoverText} ${Price}";
        if (IsConsumable && HealAmount > 0)
        {
            HoverText += $" (Heals {HealAmount} HP)";
        }

       

        mesh = Utils.FindMeshInstance(this);
        
        HoverLabel = Utils.CreateHoverLabel(HoverText);
        AddChild(HoverLabel);

        if (mesh != null)
        {
            ShaderMaterial outlineMaterial = new()
            {
                Shader = GD.Load<Shader>("res://Shaders/outline.gdshader"),
            };
            Outline = new MeshInstance3D
            {
                Mesh = mesh.Mesh,
                Visible = false,
                MaterialOverride = outlineMaterial
            };
            int surfaceCount = mesh.Mesh != null ? mesh.Mesh.GetSurfaceCount() : mesh.GetSurfaceOverrideMaterialCount();
            for (int i = 0; i < surfaceCount; i++)
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
            if ((WasBought || Thrower != null || !IsForSale) && LinearVelocity.LengthSquared() > 0.1f)
            {
                Freeze = false;
            }
        }

        _lastVelocity = LinearVelocity;
    }

    private void OnBodyEntered(Node body)
    {
        if (_hasImpacted) return;
        if (_lastVelocity.Length() < MinDamageSpeed) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase != GamePhase.BattleRoyale) return;

        // Ignore hitting the thrower!
        if (body == Thrower) return;

        _hasImpacted = true;

        Vector3 impactPos = GlobalPosition;
        _lastVelocity = Vector3.Zero;
        LinearVelocity = Vector3.Zero;

        if (body is IDamageable target)
        {
            float perkDmgMult = (Thrower is PlayerController pc && pc.CurrentPerk == PlayerPerk.PowerArm) ? 1.25f : 1.0f;
            int actualDamage = (int)(Damage * PlayerController.GlobalDamageMultiplier * perkDmgMult);
            target.TakeDamage(actualDamage, Thrower);
        }

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcOnImpact), impactPos);
        }
        else
        {
            RpcOnImpact(impactPos);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcOnImpact(Vector3 spawnPos)
    {
        if (IsQueuedForDeletion()) return;

        _hasImpacted = true;
        Visible = false;
        Freeze = true;
        CollisionLayer = 0;
        CollisionMask = 0;

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(RpcOnImpact), spawnPos);
                    }
                }
            }
        }

        if (ImpactEffect != null)
        {
            Node3D effect = ImpactEffect.Instantiate<Node3D>();
            Node scene = GetTree().CurrentScene ?? GetParent();
            if (scene != null && effect != null)
            {
                scene.AddChild(effect);
                effect.GlobalPosition = spawnPos;
                if (effect is FruitSplatter fruitSplatter)
                {
                    fruitSplatter.SetSplatColor(GetFruitSplatColor());
                }
            }
        }

        if (DestroyOnImpact)
        {
            QueueFree();
        }
    }

    public bool IsFruitProduct()
    {
        string name = (DisplayName != null && !string.IsNullOrEmpty(DisplayName.ToString())) ? DisplayName.ToString() : Name.ToString();
        return name.Contains("Apple", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Lemon", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Banana", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Avocado", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Orange", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Peach", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Berry", System.StringComparison.OrdinalIgnoreCase);
    }

    public Color GetFruitSplatColor()
    {
        string name = (DisplayName != null && !string.IsNullOrEmpty(DisplayName.ToString())) ? DisplayName.ToString() : Name.ToString();
        if (name.Contains("Apple", System.StringComparison.OrdinalIgnoreCase))
            return new Color(0.92f, 0.12f, 0.15f); // Crisp apple red
        if (name.Contains("Lemon", System.StringComparison.OrdinalIgnoreCase))
            return new Color(0.96f, 0.86f, 0.12f); // Zesty lemon yellow
        if (name.Contains("Banana", System.StringComparison.OrdinalIgnoreCase))
            return new Color(0.95f, 0.82f, 0.35f); // Banana cream/yellow
        if (name.Contains("Avocado", System.StringComparison.OrdinalIgnoreCase))
            return new Color(0.42f, 0.65f, 0.22f); // Avocado green
        return new Color(0.98f, 0.50f, 0.15f); // Vibrant citrus / fruit punch default
    }

    public void Interact(PlayerController player)
    {
        if (GameManager.Instance == null) return;
        Vector3 playerPos = player.Camera != null ? player.Camera.GlobalPosition : player.GlobalPosition;
        if (GlobalPosition.DistanceTo(playerPos) <= player.PickUpRange && GameManager.Instance.CurrentPhase != GamePhase.Lobby)
        {
            if (!player.Inventory.CanAddItem(this)) return;

            if (IsForSale)
            {
                if (GameManager.Instance.CurrentPhase == GamePhase.Shopping)
                {
                    int effectivePrice = player.GetDiscountedPrice(Price);
                    if (!player.TryDeductMoney(effectivePrice))
                    {
                        GD.Print($"[Store] Cannot afford {DisplayName}! Costs ${effectivePrice}, you have ${player.Money}");
                        return;
                    }
                }
                else if (GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale)
                {
                    if (player.CurrentPerk != PlayerPerk.Scavenger)
                    {
                        GD.Print($"[Store] Cannot scavenge shelf item {DisplayName} without Scavenger perk!");
                        return;
                    }
                    GD.Print($"[Store] Scavenged {DisplayName} during Battle Royale!");
                }
                else
                {
                    return;
                }
            }
            else
            {
                if ((GameManager.Instance.CurrentPhase == GamePhase.BattleTransition || GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale) && !CanBePickedUp) return;
            }

            IsForSale = false;
            WasBought = true;

            if (HoverLabel != null) HoverLabel.Visible = false;

            player.Rpc(nameof(player.RPCPickupItem), GetPath());
        }
    }

    public void OutlineOn()
    {
        if (Outline != null) Outline.Visible = true;
        if (HoverLabel != null)
        {
            UpdateHoverLabelForPlayer(PlayerController.Instance);
            HoverLabel.Visible = true;
        }
    }

    public void OutlineOff()
    {
        if (Outline != null) Outline.Visible = false;
        if (HoverLabel != null) HoverLabel.Visible = false;
    }

    public void UpdateHoverLabelForPlayer(PlayerController player)
    {
        if (HoverLabel == null) return;

        string name = (DisplayName != null && !string.IsNullOrEmpty(DisplayName.ToString())) ? DisplayName.ToString() : Name.ToString();

        if (IsForSale)
        {
            var phase = GameManager.Instance?.CurrentPhase;
            if (phase == GamePhase.Shopping)
            {
                int effectivePrice = player != null ? player.GetDiscountedPrice(Price) : Price;
                string discountTag = (player != null && player.CurrentPerk == PlayerPerk.BargainHunter) ? " (Bargain -25%)" : "";
                string healTag = (IsConsumable && HealAmount > 0) ? $" (+{HealAmount} HP)" : "";
                HoverLabel.Text = $"[E] Buy {name} - ${effectivePrice}{discountTag}{healTag}";
            }
            else if (phase == GamePhase.BattleRoyale)
            {
                if (player != null && player.CurrentPerk == PlayerPerk.Scavenger)
                {
                    HoverLabel.Text = $"[E] 🎒 Scavenge {name}";
                }
                else
                {
                    HoverLabel.Text = $"🔒 Locked: {name}";
                }
            }
            else if (phase == GamePhase.BattleTransition)
            {
                HoverLabel.Text = $"🔒 Locked: {name}";
            }
            else
            {
                HoverLabel.Text = $"{name} - ${Price}";
            }
        }
        else
        {
            string healTag = (IsConsumable && HealAmount > 0) ? $" (+{HealAmount} HP)" : "";
            HoverLabel.Text = $"[E] Pick Up {name}{healTag}";
        }
    }

    private void OnGamePhaseChanged()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale)
        {
            CanBePickedUp = true;
        }
        else if (GameManager.Instance.CurrentPhase == GamePhase.BattleTransition)
        {
            if (HoverLabel != null && GodotObject.IsInstanceValid(HoverLabel))
            {
                HoverLabel.Visible = false;
            }
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        }
    }

}
