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
    [Export] public float ToonOutlineWidth = 0.0028f;
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
    private MultiplayerSynchronizer _synchronizer;
    private float _settleTimer = 0f;

    // Charged fastball & combat mechanics
    public bool IsFastball { get; set; } = false;
    public float LaunchPowerMultiplier { get; set; } = 1.0f;
    private CpuParticles3D _trailParticles;
    private OmniLight3D _trailLight;

    public void ResetImpact()
    {
        _hasImpacted = false;
    }

    private static PackedScene _cachedWatermelonSplatter;
    private static PackedScene _cachedFruitSplatter;
    private static bool _splatterChecked = false;

    private static void EnsureSplattersLoaded()
    {
        if (_splatterChecked) return;
        _splatterChecked = true;

        if (ResourceLoader.Exists("res://Prefabs/WatermelonSplatter.tscn"))
        {
            _cachedWatermelonSplatter = GD.Load<PackedScene>("res://Prefabs/WatermelonSplatter.tscn");
        }
        if (ResourceLoader.Exists("res://Prefabs/FruitSplatter.tscn"))
        {
            _cachedFruitSplatter = GD.Load<PackedScene>("res://Prefabs/FruitSplatter.tscn");
        }
    }

    public override void _Ready()
    {
        // Freeze physics on start so products stay safely in place on shelves without rolling off
        Freeze = true;
        FreezeMode = FreezeModeEnum.Static;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;

        // Shelf performance: disable contact monitoring & CCD while sitting dormant on shelf
        ContactMonitor = false;
        MaxContactsReported = 0;
        ContinuousCd = false;

        _synchronizer = GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer");
        if (_synchronizer != null && IsForSale && Freeze)
        {
            _synchronizer.ProcessMode = ProcessModeEnum.Disabled;
        }

        BodyEntered += OnBodyEntered;
        AddToGroup("Products");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged += OnGamePhaseChanged;
        }

        if (ImpactEffect == null)
        {
            EnsureSplattersLoaded();
            if (DisplayName == "Watermelon" || Name.ToString().Contains("Watermelon"))
            {
                ImpactEffect = _cachedWatermelonSplatter;
            }
            else if (IsFruitProduct())
            {
                ImpactEffect = _cachedFruitSplatter;
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
    }

    public void ActivatePhysicsAndSync()
    {
        if (_synchronizer != null && GodotObject.IsInstanceValid(_synchronizer))
        {
            _synchronizer.ProcessMode = ProcessModeEnum.Inherit;
        }
        ContactMonitor = true;
        MaxContactsReported = 4;
        ContinuousCd = true;
    }

    public void DeactivatePhysicsAndSync()
    {
        DeactivateTrail();
        if (_synchronizer != null && GodotObject.IsInstanceValid(_synchronizer))
        {
            _synchronizer.ProcessMode = ProcessModeEnum.Disabled;
        }
        ContactMonitor = false;
        MaxContactsReported = 0;
        ContinuousCd = false;
    }

    private void EnsureOutlineAndLabel()
    {
        if (HoverLabel == null)
        {
            HoverLabel = Utils.CreateHoverLabel(HoverText);
            HoverLabel.Visible = false;
            AddChild(HoverLabel);
        }

        if (mesh == null)
        {
            mesh = Utils.FindMeshInstance(this);
        }

        if (Outline == null && mesh != null)
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
                ActivatePhysicsAndSync();
            }
        }
        else
        {
            // Settle check for thrown/dropped items at rest on ground
            if (!IsForSale && !Freeze && Thrower == null && LinearVelocity.LengthSquared() < 0.01f && AngularVelocity.LengthSquared() < 0.01f)
            {
                _settleTimer += (float)delta;
                if (_settleTimer > 2.0f)
                {
                    Freeze = true;
                    FreezeMode = FreezeModeEnum.Static;
                    DeactivatePhysicsAndSync();
                }
            }
            else if (!Freeze)
            {
                _settleTimer = 0f;
            }

            // Detect flying projectile near-misses with Groomba hazards in real-time
            if (Thrower != null && !_hasImpacted && LinearVelocity.LengthSquared() > 80.0f)
            {
                Groomba.CheckProjectileNearMiss(this, GlobalPosition, LinearVelocity, Thrower as PlayerController);
            }
        }

        _lastVelocity = LinearVelocity;
    }

    private void OnBodyEntered(Node body)
    {
        if (_hasImpacted) return;
        float effectiveMinSpeed = (Thrower != null) ? 4.0f : MinDamageSpeed;
        if (_lastVelocity.Length() < effectiveMinSpeed) return;

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
            int actualDamage = (int)(Damage * PlayerController.GlobalDamageMultiplier * perkDmgMult * LaunchPowerMultiplier);
            target.TakeDamage(actualDamage, Thrower);

            if (IsFastball)
            {
                FloatingDamageNumber.SpawnText(this, impactPos + Vector3.Up * 0.45f, "FASTBALL!", UITheme.ActionRed, 46);
            }

            // Confirm hit to Thrower for hit marker & audio tick
            if (Thrower is PlayerController throwerPlayer && GodotObject.IsInstanceValid(throwerPlayer))
            {
                bool isKill = (target is PlayerController victimPlayer && victimPlayer.Health != null && victimPlayer.Health.CurrentHealth <= 0);
                if (throwerPlayer.IsMultiplayerAuthority())
                {
                    throwerPlayer.TriggerHitMarker(isKill);
                }
                else if (int.TryParse(throwerPlayer.Name, out int throwerPeerId) && Multiplayer.HasMultiplayerPeer())
                {
                    throwerPlayer.RpcId(throwerPeerId, nameof(PlayerController.RpcConfirmHit), actualDamage, isKill);
                }
            }
        }

        // Alert sentient Groombas of the acoustic disturbance / thrown item landing nearby
        Groomba.NotifyDisturbance(this, impactPos, Thrower as PlayerController);

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

        DeactivateTrail();
        _hasImpacted = true;
        Visible = false;
        Freeze = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        DeactivatePhysicsAndSync();

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
        else
        {
            SpawnSpecializedSplatter(spawnPos);
        }

        if (DestroyOnImpact)
        {
            QueueFree();
        }
    }

    private void SpawnSpecializedSplatter(Vector3 spawnPos)
    {
        string pName = (DisplayName != null && !string.IsNullOrEmpty(DisplayName.ToString())) ? DisplayName.ToString() : Name.ToString();
        if (pName.Contains("Bleach", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Detergent", System.StringComparison.OrdinalIgnoreCase))
        {
            SpecialSplatters.SpawnSoap(this, spawnPos);
        }
        else if (pName.Contains("Soda", System.StringComparison.OrdinalIgnoreCase))
        {
            SpecialSplatters.SpawnSoda(this, spawnPos);
        }
        else if (pName.Contains("Cereal", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Bread", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Pizza", System.StringComparison.OrdinalIgnoreCase))
        {
            SpecialSplatters.SpawnCrumbs(this, spawnPos);
        }
        else if (pName.Contains("TV", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Drill", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Toaster", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Blender", System.StringComparison.OrdinalIgnoreCase))
        {
            SpecialSplatters.SpawnZap(this, spawnPos);
        }
        else
        {
            CombatHitEffect.Spawn(this, spawnPos);
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
                    if (player.CurrentPerk == PlayerPerk.Scavenger)
                    {
                        GD.Print($"[Store] Scavenged {DisplayName} during Battle Royale!");
                    }
                    else
                    {
                        int effectivePrice = player.GetDiscountedPrice(Price);
                        if (!player.TryDeductMoney(effectivePrice))
                        {
                            GD.Print($"[Store] Cannot afford {DisplayName}! Costs ${effectivePrice}, you have ${player.Money}");
                            return;
                        }
                        GD.Print($"[Store] Purchased {DisplayName} during Battle Royale for ${effectivePrice}!");
                    }
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
        EnsureOutlineAndLabel();
        if (Outline != null) Outline.Visible = true;
        if (HoverLabel != null)
        {
            UpdateHoverLabelForPlayer(PlayerController.Instance);
            HoverLabel.Visible = false; // HUD hover box displays item card and effects
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
                    HoverLabel.Text = $"[E] 🎒 Scavenge {name} (FREE)";
                }
                else
                {
                    int effectivePrice = player != null ? player.GetDiscountedPrice(Price) : Price;
                    string discountTag = (player != null && player.CurrentPerk == PlayerPerk.BargainHunter) ? " (Bargain -25%)" : "";
                    string healTag = (IsConsumable && HealAmount > 0) ? $" (+{HealAmount} HP)" : "";
                    HoverLabel.Text = $"[E] Buy {name} - ${effectivePrice}{discountTag}{healTag}";
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

    public void ActivateTrail(bool isFastball)
    {
        IsFastball = isFastball;
        DeactivateTrail();

        string pName = (DisplayName != null && !string.IsNullOrEmpty(DisplayName.ToString())) ? DisplayName.ToString() : Name.ToString();
        _trailParticles = new CpuParticles3D
        {
            Name = "SpeedTrail",
            Emitting = true,
            OneShot = false,
            LocalCoords = false,
            Gravity = Vector3.Zero,
            Lifetime = isFastball ? 0.38f : 0.28f,
            Amount = isFastball ? 36 : 22,
            InitialVelocityMin = 0.2f,
            InitialVelocityMax = 0.6f,
            Spread = 15.0f
        };

        if (pName.Contains("Soda", System.StringComparison.OrdinalIgnoreCase))
        {
            // Carbonated fizzy cola bubbles
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.45f, 0.15f, 0.8f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.1f,
                Metallic = 0.1f,
                RimEnabled = true,
                Rim = 0.8f,
                RimTint = 0.4f
            };
            _trailParticles.Mesh = new SphereMesh
            {
                Radius = 0.045f,
                Height = 0.09f,
                Material = mat
            };
        }
        else if (pName.Contains("TV", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Drill", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Toaster", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Alarm", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Boombox", System.StringComparison.OrdinalIgnoreCase))
        {
            // Electric blue / cyan zap sparks
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 0.85f, 1.0f),
                EmissionEnabled = true,
                Emission = new Color(0.25f, 0.9f, 1.0f),
                EmissionEnergyMultiplier = 4.0f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            _trailParticles.Mesh = new BoxMesh
            {
                Size = new Vector3(0.04f, 0.04f, 0.04f),
                Material = mat
            };
        }
        else if (pName.Contains("Bleach", System.StringComparison.OrdinalIgnoreCase) || pName.Contains("Detergent", System.StringComparison.OrdinalIgnoreCase))
        {
            // Bubbly soapy suds
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.85f, 0.98f, 1.0f, 0.85f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.1f,
                RimEnabled = true,
                Rim = 0.7f
            };
            _trailParticles.Mesh = new SphereMesh
            {
                Radius = 0.05f,
                Height = 0.10f,
                Material = mat
            };
        }
        else if (IsFruitProduct() || pName.Contains("Watermelon", System.StringComparison.OrdinalIgnoreCase))
        {
            // Juicy fruit droplets
            Color fruitCol = pName.Contains("Watermelon", System.StringComparison.OrdinalIgnoreCase)
                ? new Color(0.92f, 0.15f, 0.18f)
                : GetFruitSplatColor();
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(fruitCol.R, fruitCol.G, fruitCol.B, 0.85f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.2f
            };
            _trailParticles.Mesh = new SphereMesh
            {
                Radius = 0.04f,
                Height = 0.08f,
                Material = mat
            };
        }
        else
        {
            // Comic wind streaks / fastball speed lines
            Color trailColor = isFastball ? new Color(1.0f, 0.88f, 0.35f, 0.9f) : new Color(0.95f, 0.95f, 1.0f, 0.75f);
            var mat = new StandardMaterial3D
            {
                AlbedoColor = trailColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = isFastball,
                Emission = isFastball ? new Color(1.0f, 0.75f, 0.2f) : Colors.White,
                EmissionEnergyMultiplier = isFastball ? 2.5f : 0.8f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            _trailParticles.Mesh = new BoxMesh
            {
                Size = isFastball ? new Vector3(0.04f, 0.04f, 0.12f) : new Vector3(0.035f, 0.035f, 0.08f),
                Material = mat
            };
        }

        AddChild(_trailParticles);

        if (isFastball && _trailLight == null)
        {
            _trailLight = new OmniLight3D
            {
                LightColor = new Color(1.0f, 0.85f, 0.35f),
                LightEnergy = 1.6f,
                OmniRange = 3.0f,
                OmniAttenuation = 1.6f
            };
            AddChild(_trailLight);
        }
    }

    public void DeactivateTrail()
    {
        if (_trailLight != null && GodotObject.IsInstanceValid(_trailLight))
        {
            _trailLight.QueueFree();
            _trailLight = null;
        }

        if (_trailParticles != null && GodotObject.IsInstanceValid(_trailParticles))
        {
            _trailParticles.Emitting = false;
            if (_trailParticles.GetParent() == this)
            {
                Node currentScene = GetTree()?.CurrentScene;
                if (currentScene != null && GodotObject.IsInstanceValid(currentScene))
                {
                    Vector3 gpos = _trailParticles.GlobalPosition;
                    _trailParticles.Reparent(currentScene);
                    _trailParticles.GlobalPosition = gpos;
                    var particlesRef = _trailParticles;
                    _trailParticles.GetTree()?.CreateTimer(0.45f).Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(particlesRef))
                        {
                            particlesRef.QueueFree();
                        }
                    }));
                }
                else
                {
                    _trailParticles.QueueFree();
                }
            }
            _trailParticles = null;
        }
    }

    public override void _ExitTree()
    {
        DeactivateTrail();
        RemoveFromGroup("Products");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        }
    }

}
