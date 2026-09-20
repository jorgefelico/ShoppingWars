using System;
using Godot;

public partial class ManagersSpecialDrop : StaticBody3D, IInteractable, IDamageable
{
    public string HoverText { get; set; } = "[E] Claim Special";
    public MeshInstance3D Outline { get; set; }
    public Label3D HoverLabel { get; set; }

    private bool _isClaimed = false;
    private MeshInstance3D _crateMesh;
    private SpotLight3D _spotlight;
    private MeshInstance3D _beamMesh;
    private Label3D _bannerLabel;
    private Node3D _starPivot;
    private float _time = 0f;

    private static readonly string[] RareLootPaths = new[]
    {
        "res://Prefabs/Products/Sledgehammer.tscn",
        "res://Prefabs/Products/PropaneTank.tscn",
        "res://Prefabs/Products/FryingPan.tscn",
        "res://Prefabs/Products/PillBottle.tscn",
        "res://Prefabs/Products/Watermelon.tscn",
        "res://Prefabs/Products/FlatScreenTV.tscn",
        "res://Prefabs/Products/FireExtinguisher.tscn"
    };

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 3;

        BuildVisuals();
        HoverLabel = Utils.CreateHoverLabel(HoverText);
        if (HoverLabel != null)
        {
            HoverLabel.Position = new Vector3(0, 1.8f, 0);
            AddChild(HoverLabel);
        }
    }

    private void BuildVisuals()
    {
        // 1. Collision Shape
        var colShape = new CollisionShape3D();
        colShape.Shape = new BoxShape3D { Size = new Vector3(1.6f, 1.4f, 1.6f) };
        colShape.Position = new Vector3(0, 0.7f, 0);
        AddChild(colShape);

        // 2. Golden Crate Mesh
        var goldMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.82f, 0.15f),
            Metallic = 0.65f,
            Roughness = 0.25f,
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.75f, 0.1f),
            EmissionEnergyMultiplier = 0.8f
        };

        _crateMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.5f, 1.3f, 1.5f) },
            Position = new Vector3(0, 0.65f, 0),
            MaterialOverride = goldMat
        };
        AddChild(_crateMesh);

        // Golden Trim Corners
        var darkGoldMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.65f, 0.1f),
            Metallic = 0.85f,
            Roughness = 0.3f
        };
        var lidMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.18f, 1.6f) },
            Position = new Vector3(0, 1.3f, 0),
            MaterialOverride = darkGoldMat
        };
        AddChild(lidMesh);

        // 3. Rotating Star Pivot
        _starPivot = new Node3D { Position = new Vector3(0, 1.7f, 0) };
        AddChild(_starPivot);

        // 4. Overhead Spotlight (from ceiling at Y = 8.5)
        _spotlight = new SpotLight3D
        {
            Position = new Vector3(0, 8.5f, 0),
            RotationDegrees = new Vector3(-90, 0, 0),
            LightColor = new Color(1.0f, 0.9f, 0.4f),
            LightEnergy = 6.5f,
            LightSpecular = 1.5f,
            LightVolumetricFogEnergy = 2.5f,
            ShadowEnabled = true,
            ShadowBlur = 1.5f,
            SpotRange = 12.0f,
            SpotAngle = 26.0f
        };
        AddChild(_spotlight);

        // Semi-transparent volumetric beam visual
        var beamMat = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 0.88f, 0.3f, 0.08f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _beamMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 3.2f, Height = 8.5f },
            Position = new Vector3(0, 4.25f, 0),
            MaterialOverride = beamMat
        };
        AddChild(_beamMesh);

        // 5. Floating Billboard Label
        _bannerLabel = new Label3D
        {
            Text = "⭐ MANAGER'S SPECIAL ⭐\n[E] CLAIM DROP",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 24,
            OutlineSize = 6,
            Modulate = new Color(1.0f, 0.9f, 0.2f),
            OutlineModulate = Colors.Black,
            Position = new Vector3(0, 2.1f, 0)
        };
        AddChild(_bannerLabel);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;

        if (_starPivot != null)
        {
            _starPivot.RotateY(dt * 1.5f);
        }

        if (_crateMesh != null && _crateMesh.MaterialOverride is StandardMaterial3D mat)
        {
            float pulse = 0.8f + Mathf.Sin(_time * 4.0f) * 0.4f;
            mat.EmissionEnergyMultiplier = pulse;
        }

        if (_bannerLabel != null)
        {
            _bannerLabel.Position = new Vector3(0, 2.1f + Mathf.Sin(_time * 3.0f) * 0.08f, 0);
        }
    }

    public void Interact(PlayerController player)
    {
        if (_isClaimed) return;
        Claim(player);
    }

    public void TakeDamage(int amount, Node3D source = null)
    {
        if (_isClaimed) return;
        PlayerController player = source as PlayerController;
        Claim(player);
    }

    private void Claim(PlayerController claimer)
    {
        if (_isClaimed) return;
        _isClaimed = true;

        string claimerName = claimer != null ? claimer.PlayerName : "A Shopper";
        long claimerId = claimer != null ? claimer.GetMultiplayerAuthority() : 0;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncClaimed), claimerName, claimerId);
        }
        else
        {
            RpcSyncClaimed(claimerName, claimerId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncClaimed(string claimerName, long claimerId)
    {
        _isClaimed = true;

        // Visual fanfare: pop animation
        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "scale", new Vector3(1.3f, 1.3f, 1.3f), 0.15f);
        tween.TweenProperty(this, "scale", Vector3.Zero, 0.25f);

        // Announce via Mr. Henderson
        ManagerAnnouncer.Instance?.Announce(
            $"Manager's Special claimed by {claimerName}! May God have mercy on whoever is in that aisle!",
            ManagerEmotion.Panicked,
            "res://Sounds/henderson_special_claimed_01.wav",
            5.0f,
            priority: 4
        );

        // Spawn explosion/confetti burst
        SpawnImpactBurst();

        // Server-only rewards and loot dispersion
        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
        {
            DispenseLoot();

            // Reward opener
            PlayerController opener = null;
            if (claimerId != 0)
            {
                foreach (Node node in GetTree().GetNodesInGroup("Players"))
                {
                    if (node is PlayerController pc && pc.GetMultiplayerAuthority() == claimerId)
                    {
                        opener = pc;
                        break;
                    }
                }
            }
            opener ??= PlayerController.Instance;

            if (opener != null && GodotObject.IsInstanceValid(opener))
            {
                opener.AddMoney(100);
                opener.Health?.Heal(35);
                opener.SpeedModifier = 1.3f;
            }
        }

        // Cleanup drop after animation completes
        GetTree().CreateTimer(0.5f).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(this))
            {
                QueueFree();
            }
        };
    }

    private void SpawnImpactBurst()
    {
        string explPath = "res://Prefabs/Explosion.tscn";
        if (ResourceLoader.Exists(explPath) || FileAccess.FileExists(explPath))
        {
            var explScene = GD.Load<PackedScene>(explPath);
            if (explScene != null)
            {
                Node3D expl = explScene.Instantiate<Node3D>();
                GetTree().CurrentScene.AddChild(expl);
                expl.GlobalPosition = GlobalPosition + Vector3.Up * 0.8f;
            }
        }
    }

    private void DispenseLoot()
    {
        RandomNumberGenerator rng = new();
        rng.Randomize();

        int itemCount = rng.RandiRange(3, 5);
        for (int i = 0; i < itemCount; i++)
        {
            int lootIndex = rng.RandiRange(0, RareLootPaths.Length - 1);
            string path = RareLootPaths[lootIndex];

            if (!ResourceLoader.Exists(path) && !FileAccess.FileExists(path)) continue;

            PackedScene scene = GD.Load<PackedScene>(path);
            if (scene == null) continue;

            Node spawned = scene.Instantiate();
            if (spawned is Product product)
            {
                GetTree().CurrentScene.AddChild(product);
                product.GlobalPosition = GlobalPosition + Vector3.Up * 1.0f;

                // Eject outward in a radial circle
                float angle = (Mathf.Tau / itemCount) * i + (float)rng.RandfRange(-0.2f, 0.2f);
                Vector3 impulseDir = new Vector3(Mathf.Cos(angle), 1.2f, Mathf.Sin(angle)).Normalized();
                float impulseForce = (float)rng.RandfRange(5.0f, 8.5f);

                product.Freeze = false;
                product.ActivatePhysicsAndSync();
                product.CollisionLayer = 1;
                product.CollisionMask = 3;
                product.Visible = true;
                product.IsForSale = false;
                product.WasBought = true;
                product.CanBePickedUp = true;
                product.ApplyCentralImpulse(impulseDir * impulseForce);
            }
        }
    }
}
