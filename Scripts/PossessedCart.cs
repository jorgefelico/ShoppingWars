using Godot;
using System.Collections.Generic;

public partial class PossessedCart : CharacterBody3D, IDamageable, IPatrol
{
    public enum CartState
    {
        Patrol,
        Windup,
        Charge,
        Stunned
    }

    [Export] public NavigationAgent3D NavAgent { get; set; }
    [Export] public Health Health;
    [Export] public float PatrolSpeed = 2.5f;
    [Export] public float ChargeSpeed = 10.5f;
    [Export] public float WindupDuration = 0.75f;
    [Export] public float StunDuration = 2.2f;
    [Export] public int ChargeDamage = 35;
    [Export] public float KnockbackForce = 16.0f;
    [Export] public float DetectionRange = 22.0f;
    [Export] public float DetectionFov = 130.0f;
    [Export] public PackedScene ExplosionScene;
    [Export] public PackedScene CartModelScene;
    [Export] public float ModelScale = 1.05f;

    public PatrolEntityState PatrolState { get; set; } = PatrolEntityState.Patrol;
    public float SpeedMultiplier { get; set; } = 1.0f;
    public bool IsDestroyed => _isDestroyed;

    private CartState _state = CartState.Patrol;
    private PlayerController _targetPlayer;
    private Vector3 _chargeDirection = Vector3.Forward;
    private float _stateTimer = 0f;
    private float _chargeCooldown = 0f;
    private float _wobblyWheelTimer = 0f;
    private bool _isDestroyed = false;

    // Visual nodes
    private Node3D _cartVisuals;
    private MeshInstance3D _wobblyWheelMesh;
    private MeshInstance3D _headlightLeftMesh;
    private MeshInstance3D _headlightRightMesh;
    private SpotLight3D _spotLightLeft;
    private SpotLight3D _spotLightRight;
    private OmniLight3D _underglow;
    private CpuParticles3D _sparkParticles;
    private Label3D _tagPlate;
    private Node3D _dizzyStars;
    private StandardMaterial3D _headlightMat;

    // Audio components
    private AudioStreamPlayer3D _rattlePlayer;
    private AudioStreamPlayer3D _bellPlayer;
    private AudioStreamPlayer3D _crashPlayer;

    // Multiplayer sync
    [Export] public Vector3 SyncPosition = Vector3.Zero;
    [Export] public Vector3 SyncRotation = Vector3.Zero;
    private float _networkSyncTimer = 0f;
    private const float NetworkSyncInterval = 0.05f; // 20 Hz sync rate
    private Vector3 _lastSentPos = Vector3.Zero;
    private Vector3 _lastSentRot = Vector3.Zero;
    private float _heartbeatTimer = 0f;

    public override void _Ready()
    {
        AddToGroup("PatrolEnemies");
        AddToGroup("PossessedCarts");

        if (Health == null) Health = GetNodeOrNull<Health>("Health");
        if (NavAgent == null) NavAgent = GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");

        BuildCartVisuals();
        InitializeAudio();

        if (Multiplayer != null && Multiplayer.IsServer())
        {
            Callable.From(SetRandomPatrolTarget).CallDeferred();
        }

        SyncPosition = IsInsideTree() ? GlobalPosition : Position;
        SyncRotation = Rotation;
    }

    #region Cart Visuals & 3D Model Construction

    private void BuildCartVisuals()
    {
        _cartVisuals = new Node3D { Name = "CartVisuals" };
        AddChild(_cartVisuals);

        _headlightMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.15f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(2.5f, 0.2f, 0.2f),
            Roughness = 0.2f
        };

        if (CartModelScene == null && ResourceLoader.Exists("res://Models/cart.tscn"))
        {
            CartModelScene = GD.Load<PackedScene>("res://Models/cart.tscn");
        }

        bool modelLoaded = false;
        if (CartModelScene != null)
        {
            try
            {
                var cartModelInstance = CartModelScene.Instantiate<Node3D>();
                if (cartModelInstance != null)
                {
                    cartModelInstance.Name = "CartModel";
                    _cartVisuals.AddChild(cartModelInstance);

                    // Align model: Rotate 90 deg around Y so front (+X in model) points along -Z (Godot forward)
                    cartModelInstance.RotationDegrees = new Vector3(0, 90, 0);
                    cartModelInstance.Scale = new Vector3(ModelScale, ModelScale, ModelScale);
                    // Adjust Y so tires rest on the floor (Y = 0) and center along Z
                    cartModelInstance.Position = new Vector3(0, -0.245f * (ModelScale / 1.05f), -0.022f * (ModelScale / 1.05f));

                    ApplyStylizedMaterials(cartModelInstance);
                    modelLoaded = true;
                    GD.Print("[PossessedCart] Loaded 3D cart model successfully.");
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[PossessedCart] Error instantiating cart model: {ex.Message}");
            }
        }

        if (!modelLoaded)
        {
            BuildProceduralCartMesh(_cartVisuals);
        }

        AttachDemonicAccessories(_cartVisuals);
    }

    private void ApplyStylizedMaterials(Node3D modelRoot)
    {
        var chromeMetal = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.88f, 0.92f),
            Metallic = 0.9f,
            Roughness = 0.22f
        };

        var darkMetal = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.38f, 0.42f),
            Metallic = 0.8f,
            Roughness = 0.35f
        };

        var redPlastic = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.12f, 0.12f),
            Metallic = 0.05f,
            Roughness = 0.35f
        };

        var tyreRubber = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.18f, 0.2f),
            Metallic = 0.1f,
            Roughness = 0.85f
        };

        // Cart meshes: cart_handle, cart_base, cart_lower, cart_tyres
        var handleMesh = modelRoot.FindChild("cart_handle", true, false) as MeshInstance3D;
        if (handleMesh != null)
        {
            handleMesh.SetSurfaceOverrideMaterial(0, darkMetal);
            handleMesh.SetSurfaceOverrideMaterial(1, redPlastic);
        }

        var baseMesh = modelRoot.FindChild("cart_base", true, false) as MeshInstance3D;
        if (baseMesh != null)
        {
            baseMesh.SetSurfaceOverrideMaterial(0, chromeMetal);
        }

        var lowerMesh = modelRoot.FindChild("cart_lower", true, false) as MeshInstance3D;
        if (lowerMesh != null)
        {
            lowerMesh.SetSurfaceOverrideMaterial(0, darkMetal);
        }

        var tyresMesh = modelRoot.FindChild("cart_tyres", true, false) as MeshInstance3D;
        if (tyresMesh != null)
        {
            tyresMesh.SetSurfaceOverrideMaterial(0, tyreRubber);
            tyresMesh.SetSurfaceOverrideMaterial(1, darkMetal);
            tyresMesh.SetSurfaceOverrideMaterial(2, chromeMetal);
        }
    }

    private void BuildProceduralCartMesh(Node3D parent)
    {
        var chromeMetal = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.88f, 0.92f),
            Metallic = 0.85f,
            Roughness = 0.25f
        };

        var darkMetal = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.24f, 0.28f),
            Metallic = 0.7f,
            Roughness = 0.4f
        };

        var redPlastic = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.12f, 0.12f),
            Metallic = 0.05f,
            Roughness = 0.35f
        };

        // Chassis Base
        var chassis = new MeshInstance3D
        {
            Name = "Chassis",
            Mesh = new BoxMesh { Size = new Vector3(0.72f, 0.08f, 1.15f) },
            Position = new Vector3(0, 0.18f, 0),
            MaterialOverride = darkMetal
        };
        parent.AddChild(chassis);

        var tray = new MeshInstance3D
        {
            Name = "BottomTray",
            Mesh = new BoxMesh { Size = new Vector3(0.66f, 0.02f, 0.95f) },
            Position = new Vector3(0, 0.22f, 0.05f),
            MaterialOverride = chromeMetal
        };
        parent.AddChild(tray);

        // Basket
        var basketNode = new Node3D { Name = "Basket", Position = new Vector3(0, 0.62f, 0) };
        parent.AddChild(basketNode);

        basketNode.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.68f, 0.02f, 1.05f) },
            MaterialOverride = chromeMetal
        });
        basketNode.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.52f, 1.05f) },
            Position = new Vector3(-0.34f, 0.26f, 0),
            MaterialOverride = chromeMetal
        });
        basketNode.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.52f, 1.05f) },
            Position = new Vector3(0.34f, 0.26f, 0),
            MaterialOverride = chromeMetal
        });
        basketNode.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.68f, 0.44f, 0.02f) },
            Position = new Vector3(0, 0.22f, -0.52f),
            MaterialOverride = chromeMetal
        });
        basketNode.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.68f, 0.56f, 0.02f) },
            Position = new Vector3(0, 0.28f, 0.52f),
            MaterialOverride = chromeMetal
        });
        basketNode.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.72f, 0.035f, 1.08f) },
            Position = new Vector3(0, 0.53f, 0),
            MaterialOverride = chromeMetal
        });

        var handle = new MeshInstance3D
        {
            Name = "HandleBar",
            Mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.68f },
            Position = new Vector3(0, 0.56f, 0.64f),
            RotationDegrees = new Vector3(0, 0, 90),
            MaterialOverride = redPlastic
        };
        basketNode.AddChild(handle);

        // Wheels
        var wheelsNode = new Node3D { Name = "Wheels" };
        parent.AddChild(wheelsNode);
        CreateWheel(wheelsNode, new Vector3(0.32f, 0.09f, -0.44f), "Wheel_FrontRight", darkMetal);
        CreateWheel(wheelsNode, new Vector3(-0.32f, 0.09f, 0.44f), "Wheel_RearLeft", darkMetal);
        CreateWheel(wheelsNode, new Vector3(0.32f, 0.09f, 0.44f), "Wheel_RearRight", darkMetal);
        _wobblyWheelMesh = CreateWheel(wheelsNode, new Vector3(-0.32f, 0.09f, -0.44f), "WobblyWheel_FrontLeft", darkMetal);
        _wobblyWheelMesh.RotationDegrees = new Vector3(0, 18, 90);
    }

    private void AttachDemonicAccessories(Node3D parent)
    {
        // 1. Demonic Headlights & Underglow
        _headlightLeftMesh = new MeshInstance3D
        {
            Name = "HeadlightLeft",
            Mesh = new SphereMesh { Radius = 0.065f, Height = 0.13f },
            Position = new Vector3(-0.24f, 0.56f, -0.63f),
            MaterialOverride = _headlightMat
        };
        parent.AddChild(_headlightLeftMesh);

        _headlightRightMesh = new MeshInstance3D
        {
            Name = "HeadlightRight",
            Mesh = new SphereMesh { Radius = 0.065f, Height = 0.13f },
            Position = new Vector3(0.24f, 0.56f, -0.63f),
            MaterialOverride = _headlightMat
        };
        parent.AddChild(_headlightRightMesh);

        _spotLightLeft = new SpotLight3D
        {
            LightColor = new Color(1.0f, 0.18f, 0.18f),
            LightEnergy = 2.4f,
            SpotRange = 24.0f,
            SpotAngle = 36.0f,
            Position = new Vector3(-0.24f, 0.56f, -0.65f),
            RotationDegrees = new Vector3(-4f, 0, 0)
        };
        parent.AddChild(_spotLightLeft);

        _spotLightRight = new SpotLight3D
        {
            LightColor = new Color(1.0f, 0.18f, 0.18f),
            LightEnergy = 2.4f,
            SpotRange = 24.0f,
            SpotAngle = 36.0f,
            Position = new Vector3(0.24f, 0.56f, -0.65f),
            RotationDegrees = new Vector3(-4f, 0, 0)
        };
        parent.AddChild(_spotLightRight);

        _underglow = new OmniLight3D
        {
            LightColor = new Color(1.0f, 0.12f, 0.12f),
            LightEnergy = 1.8f,
            OmniRange = 4.2f,
            Position = new Vector3(0, 0.16f, 0)
        };
        parent.AddChild(_underglow);

        // 2. Wheel Sparks Particles
        _sparkParticles = new CpuParticles3D
        {
            Name = "SparkParticles",
            Emitting = false,
            Amount = 24,
            Lifetime = 0.35f,
            Direction = new Vector3(0, 0.35f, 1.0f),
            Spread = 45.0f,
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 5.0f,
            Gravity = new Vector3(0, -9.8f, 0),
            Position = new Vector3(0, 0.1f, 0.35f)
        };
        var sparkMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.85f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(3.5f, 2.5f, 0.2f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _sparkParticles.Mesh = new SphereMesh
        {
            Radius = 0.025f,
            Height = 0.05f,
            Material = sparkMat
        };
        parent.AddChild(_sparkParticles);

        // 3. Front Digital License Plate / Tag
        _tagPlate = new Label3D
        {
            Name = "TagPlate",
            Text = "[ 6 6 6 ]",
            FontSize = 38,
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Modulate = new Color(1.0f, 0.2f, 0.2f),
            Position = new Vector3(0, 0.64f, -0.64f),
            PixelSize = 0.005f,
            RenderPriority = 6
        };
        parent.AddChild(_tagPlate);

        // 4. Stunned Dizzy Stars Node
        _dizzyStars = new Node3D
        {
            Name = "DizzyStars",
            Position = new Vector3(0, 1.35f, 0.15f),
            Visible = false
        };
        var starLabel = new Label3D
        {
            Text = "✨ @ _ @ ✨",
            FontSize = 36,
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Modulate = new Color(1.0f, 0.9f, 0.2f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.006f,
            RenderPriority = 7
        };
        _dizzyStars.AddChild(starLabel);
        parent.AddChild(_dizzyStars);
    }

    private MeshInstance3D CreateWheel(Node parent, Vector3 pos, string name, Material mat)
    {
        var wheel = new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh
            {
                TopRadius = 0.08f,
                BottomRadius = 0.08f,
                Height = 0.05f
            },
            Position = pos,
            RotationDegrees = new Vector3(0, 0, 90),
            MaterialOverride = mat
        };
        parent.AddChild(wheel);
        return wheel;
    }

    #endregion

    #region Procedural Audio Synthesis

    private void InitializeAudio()
    {
        _rattlePlayer = new AudioStreamPlayer3D
        {
            Name = "RattlePlayer",
            UnitSize = 12.0f,
            MaxDistance = 35.0f,
            VolumeDb = -4.0f,
            Bus = "SFX"
        };
        _rattlePlayer.Stream = GenerateRattleWav();
        AddChild(_rattlePlayer);

        _bellPlayer = new AudioStreamPlayer3D
        {
            Name = "BellPlayer",
            UnitSize = 18.0f,
            MaxDistance = 45.0f,
            VolumeDb = 2.0f,
            Bus = "SFX"
        };
        _bellPlayer.Stream = GenerateBellWav();
        AddChild(_bellPlayer);

        _crashPlayer = new AudioStreamPlayer3D
        {
            Name = "CrashPlayer",
            UnitSize = 20.0f,
            MaxDistance = 50.0f,
            VolumeDb = 3.0f,
            Bus = "SFX"
        };
        _crashPlayer.Stream = GenerateCrashWav();
        AddChild(_crashPlayer);
    }

    private static AudioStreamWav GenerateRattleWav()
    {
        const int sampleRate = 44100;
        const float duration = 1.2f;
        int sampleCount = (int)(sampleRate * duration);
        byte[] data = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;

            // Clattering metal clicks
            float clickPattern = Mathf.Sin(t * Mathf.Tau * 24.0f);
            float click = (clickPattern > 0.85f ? 0.45f : 0f) * Mathf.Sin(t * Mathf.Tau * 850.0f);

            // Squeaky wobbly wheel chirp every 0.3s
            float squeakCycle = t % 0.3f;
            float squeak = 0f;
            if (squeakCycle < 0.08f)
            {
                float freq = Mathf.Lerp(1800f, 2600f, squeakCycle / 0.08f);
                squeak = Mathf.Sin(squeakCycle * Mathf.Tau * freq) * (1f - squeakCycle / 0.08f) * 0.4f;
            }

            float sample = Mathf.Clamp(click + squeak, -1f, 1f);
            short val = (short)(sample * 32767f);
            data[i * 2] = (byte)(val & 0xFF);
            data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopEnd = sampleCount,
            Data = data
        };
    }

    private static AudioStreamWav GenerateBellWav()
    {
        const int sampleRate = 44100;
        const float duration = 0.75f;
        int sampleCount = (int)(sampleRate * duration);
        byte[] data = new byte[sampleCount * 2];

        // 4 rapid bell rings: DING-DING-DING-DING!
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float ringCycle = t % 0.18f;
            float decay = Mathf.Exp(-ringCycle * 25.0f);

            float bellTone = Mathf.Sin(ringCycle * Mathf.Tau * 2200f) * 0.7f +
                             Mathf.Sin(ringCycle * Mathf.Tau * 4400f) * 0.3f;

            float sample = Mathf.Clamp(bellTone * decay, -1f, 1f);
            short val = (short)(sample * 32767f);
            data[i * 2] = (byte)(val & 0xFF);
            data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Data = data
        };
    }

    private static AudioStreamWav GenerateCrashWav()
    {
        const int sampleRate = 44100;
        const float duration = 0.65f;
        int sampleCount = (int)(sampleRate * duration);
        byte[] data = new byte[sampleCount * 2];
        var rng = new RandomNumberGenerator();

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float decay = Mathf.Exp(-t * 8.0f);

            float bassThump = Mathf.Sin(t * Mathf.Tau * 75.0f) * 0.5f;
            float metalClang = Mathf.Sin(t * Mathf.Tau * 680.0f) * 0.35f + Mathf.Sin(t * Mathf.Tau * 1420.0f) * 0.2f;
            float noise = rng.RandfRange(-0.35f, 0.35f);

            float sample = Mathf.Clamp((bassThump + metalClang + noise) * decay, -1f, 1f);
            short val = (short)(sample * 32767f);
            data[i * 2] = (byte)(val & 0xFF);
            data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Data = data
        };
    }

    #endregion

    #region Process & Physics Process

    public override void _Process(double delta)
    {
        // Animate wobbly caster wheel vibration / authentic cart rumble
        if (Velocity.LengthSquared() > 0.05f)
        {
            _wobblyWheelTimer += (float)delta * 32.0f;
            float wobble = Mathf.Sin(_wobblyWheelTimer);
            if (_wobblyWheelMesh != null)
            {
                _wobblyWheelMesh.RotationDegrees = new Vector3(0, wobble * 16.0f, 90);
            }
            if (_cartVisuals != null)
            {
                // Subtle high-frequency shopping cart rattle while rolling
                _cartVisuals.RotationDegrees = new Vector3(wobble * 0.75f, 0, Mathf.Cos(_wobblyWheelTimer * 0.8f) * 0.6f);
            }
        }
        else if (_cartVisuals != null && _cartVisuals.RotationDegrees != Vector3.Zero)
        {
            _cartVisuals.RotationDegrees = Vector3.Zero;
        }

        // Modulate squeaky wheel audio pitch based on speed
        if (_rattlePlayer != null)
        {
            float speedRatio = Velocity.Length() / PatrolSpeed;
            if (speedRatio > 0.1f && !_rattlePlayer.Playing)
            {
                _rattlePlayer.Play();
            }
            else if (speedRatio <= 0.1f && _rattlePlayer.Playing)
            {
                _rattlePlayer.Stop();
            }

            if (_rattlePlayer.Playing)
            {
                _rattlePlayer.PitchScale = Mathf.Clamp(0.85f + speedRatio * 0.35f, 0.8f, 2.0f);
            }
        }

        // Spin dizzy stars if stunned
        if (_dizzyStars != null && _dizzyStars.Visible)
        {
            _dizzyStars.RotateY((float)delta * 4.5f);
        }

        if (Multiplayer != null && Multiplayer.IsServer()) return;

        // Remote client transform interpolation
        float dist = GlobalPosition.DistanceTo(SyncPosition);
        if (dist > 5.0f)
        {
            GlobalPosition = SyncPosition;
        }
        else
        {
            float lerpWeight = (float)Mathf.Clamp(delta * 20.0, 0.0, 1.0);
            GlobalPosition = GlobalPosition.Lerp(SyncPosition, lerpWeight);
        }

        float rotLerpWeight = (float)Mathf.Clamp(delta * 20.0, 0.0, 1.0);
        Rotation = new Vector3(
            Mathf.LerpAngle(Rotation.X, SyncRotation.X, rotLerpWeight),
            Mathf.LerpAngle(Rotation.Y, SyncRotation.Y, rotLerpWeight),
            Mathf.LerpAngle(Rotation.Z, SyncRotation.Z, rotLerpWeight)
        );
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Multiplayer == null || !Multiplayer.IsServer()) return;
        if (GameManager.Instance?.CurrentPhase != GamePhase.BattleRoyale) return;

        if (_chargeCooldown > 0f) _chargeCooldown -= (float)delta;

        Vector3 velocity = Velocity;
        if (!IsOnFloor())
        {
            velocity.Y -= 9.8f * (float)delta;
        }

        switch (_state)
        {
            case CartState.Patrol:
                HandlePatrolState(ref velocity);
                break;

            case CartState.Windup:
                HandleWindupState(delta, ref velocity);
                break;

            case CartState.Charge:
                HandleChargeState(delta, ref velocity);
                break;

            case CartState.Stunned:
                HandleStunnedState(delta, ref velocity);
                break;
        }

        Velocity = velocity;
        MoveAndSlide();

        SyncPosition = GlobalPosition;
        SyncRotation = Rotation;

        _networkSyncTimer += (float)delta;
        _heartbeatTimer += (float)delta;

        if (_networkSyncTimer >= NetworkSyncInterval && Multiplayer.HasMultiplayerPeer())
        {
            bool moved = GlobalPosition.DistanceSquaredTo(_lastSentPos) > 0.0004f;
            bool turned = Rotation.DistanceSquaredTo(_lastSentRot) > 0.0004f;
            bool heartbeat = _heartbeatTimer >= 0.4f;

            if (moved || turned || heartbeat)
            {
                _networkSyncTimer = 0f;
                _heartbeatTimer = 0f;
                _lastSentPos = GlobalPosition;
                _lastSentRot = Rotation;
                Rpc(nameof(RpcSyncTransform), SyncPosition, SyncRotation);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void RpcSyncTransform(Vector3 position, Vector3 rotation)
    {
        SyncPosition = position;
        SyncRotation = rotation;
    }

    #endregion

    #region AI State Handlers

    private void HandlePatrolState(ref Vector3 velocity)
    {
        // Detect players to ram!
        if (_chargeCooldown <= 0f)
        {
            DetectPlayer();
            if (_targetPlayer != null && !_targetPlayer.Health.IsDead)
            {
                SetCartState(CartState.Windup);
                return;
            }
        }

        if (NavAgent != null && NavAgent.IsNavigationFinished())
        {
            SetRandomPatrolTarget();
        }

        MoveAlongNavPath(PatrolSpeed, ref velocity);
    }

    private void HandleWindupState(double delta, ref Vector3 velocity)
    {
        _stateTimer -= (float)delta;
        velocity.X = 0;
        velocity.Z = 0;

        if (_targetPlayer != null && GodotObject.IsInstanceValid(_targetPlayer) && !_targetPlayer.Health.IsDead)
        {
            Vector3 toPlayer = _targetPlayer.GlobalPosition - GlobalPosition;
            toPlayer.Y = 0;
            if (toPlayer.LengthSquared() > 0.01f)
            {
                LookAt(GlobalPosition + toPlayer.Normalized(), Vector3.Up);
            }
        }

        if (_stateTimer <= 0f)
        {
            if (_targetPlayer != null && GodotObject.IsInstanceValid(_targetPlayer))
            {
                Vector3 dir = (_targetPlayer.GlobalPosition - GlobalPosition);
                dir.Y = 0;
                _chargeDirection = dir.LengthSquared() > 0.01f ? dir.Normalized() : -GlobalTransform.Basis.Z;
            }
            else
            {
                _chargeDirection = -GlobalTransform.Basis.Z;
            }
            SetCartState(CartState.Charge);
        }
    }

    private void HandleChargeState(double delta, ref Vector3 velocity)
    {
        _stateTimer -= (float)delta;

        // Slight homing steering towards target player
        if (_targetPlayer != null && GodotObject.IsInstanceValid(_targetPlayer) && !_targetPlayer.Health.IsDead)
        {
            Vector3 toTarget = _targetPlayer.GlobalPosition - GlobalPosition;
            toTarget.Y = 0;
            if (toTarget.LengthSquared() > 0.05f)
            {
                _chargeDirection = _chargeDirection.Lerp(toTarget.Normalized(), (float)delta * 2.2f).Normalized();
            }
        }

        LookAt(GlobalPosition + _chargeDirection, Vector3.Up);
        velocity.X = _chargeDirection.X * ChargeSpeed * SpeedMultiplier;
        velocity.Z = _chargeDirection.Z * ChargeSpeed * SpeedMultiplier;

        // Check for ramming impacts
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            KinematicCollision3D col = GetSlideCollision(i);
            Node collider = col.GetCollider() as Node;

            if (collider is PlayerController victim && victim.Health != null && !victim.Health.IsDead)
            {
                // Ram the player!
                victim.Health.TakeDamage(ChargeDamage);
                victim.ApplyKnockback(_chargeDirection * KnockbackForce + Vector3.Up * 4.0f);
                Product dropped = victim.KnockDropItem();

                _crashPlayer?.Play();

                string[] ramTexts = { "WHAM!", "RAMMED!", "POW!", "CART CHECK!" };
                string text = ramTexts[GD.Randi() % ramTexts.Length];
                RpcShowReaction(text, new Color(1.0f, 0.2f, 0.1f));

                if (dropped != null)
                {
                    FloatingDamageNumber.SpawnText(victim, victim.GlobalPosition + Vector3.Up * 1.8f, "ITEM DROPPED!", new Color(1.0f, 0.5f, 0.1f), 42);
                }

                ManagerAnnouncer.Instance?.AnnounceCartRam(victim.PlayerName);

                _chargeCooldown = 3.5f;
                SetCartState(CartState.Patrol);
                return;
            }
            else if (col.GetNormal().Dot(-_chargeDirection) > 0.45f)
            {
                // Slammed into solid wall or gondola shelf!
                _crashPlayer?.Play();
                velocity = col.GetNormal() * 3.0f; // Small recoil bounce
                RpcShowReaction("CLANG!", new Color(1.0f, 0.85f, 0.2f));
                SetCartState(CartState.Stunned);
                return;
            }
        }

        if (_stateTimer <= 0f)
        {
            _chargeCooldown = 2.5f;
            SetCartState(CartState.Patrol);
        }
    }

    private void HandleStunnedState(double delta, ref Vector3 velocity)
    {
        _stateTimer -= (float)delta;
        velocity.X = 0;
        velocity.Z = 0;

        if (_stateTimer <= 0f)
        {
            _chargeCooldown = 2.0f;
            SetCartState(CartState.Patrol);
        }
    }

    private void SetCartState(CartState newState)
    {
        if (_state == newState) return;
        _state = newState;

        switch (newState)
        {
            case CartState.Patrol:
                _targetPlayer = null;
                _sparkParticles.Emitting = false;
                _dizzyStars.Visible = false;
                _tagPlate.Text = "[ 6 6 6 ]";
                _tagPlate.Modulate = new Color(1.0f, 0.2f, 0.2f);
                SetHeadlightEmission(2.0f);
                SetRandomPatrolTarget();
                break;

            case CartState.Windup:
                _stateTimer = WindupDuration;
                _bellPlayer?.Play();
                _sparkParticles.Emitting = true;
                _dizzyStars.Visible = false;
                _tagPlate.Text = "[ ! ! ! ]";
                _tagPlate.Modulate = new Color(1.0f, 0.9f, 0.1f);
                SetHeadlightEmission(5.0f);

                string[] revTexts = { "RAMMING SPEED!", "DING DING!", "OUT OF MY WAY!", "SPEED RUN!" };
                RpcShowReaction(revTexts[GD.Randi() % revTexts.Length], new Color(1.0f, 0.3f, 0.1f));
                break;

            case CartState.Charge:
                _stateTimer = 3.5f; // Max charge duration
                _sparkParticles.Emitting = true;
                _dizzyStars.Visible = false;
                _tagPlate.Text = "[ D I E ]";
                _tagPlate.Modulate = new Color(1.0f, 0.1f, 0.1f);
                SetHeadlightEmission(6.0f);
                break;

            case CartState.Stunned:
                _stateTimer = StunDuration;
                _sparkParticles.Emitting = false;
                _dizzyStars.Visible = true;
                _tagPlate.Text = "[ 4 0 4 ]";
                _tagPlate.Modulate = new Color(0.6f, 0.6f, 0.6f);
                SetHeadlightEmission(0.4f);
                break;
        }

        if (Multiplayer.IsServer() && Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncCartState), (int)newState);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcSyncCartState(int stateIndex)
    {
        _state = (CartState)stateIndex;
        switch (_state)
        {
            case CartState.Patrol:
                if (_sparkParticles != null) _sparkParticles.Emitting = false;
                if (_dizzyStars != null) _dizzyStars.Visible = false;
                if (_tagPlate != null) { _tagPlate.Text = "[ 6 6 6 ]"; _tagPlate.Modulate = new Color(1.0f, 0.2f, 0.2f); }
                SetHeadlightEmission(2.0f);
                break;

            case CartState.Windup:
                _bellPlayer?.Play();
                if (_sparkParticles != null) _sparkParticles.Emitting = true;
                if (_dizzyStars != null) _dizzyStars.Visible = false;
                if (_tagPlate != null) { _tagPlate.Text = "[ ! ! ! ]"; _tagPlate.Modulate = new Color(1.0f, 0.9f, 0.1f); }
                SetHeadlightEmission(5.0f);
                break;

            case CartState.Charge:
                if (_sparkParticles != null) _sparkParticles.Emitting = true;
                if (_dizzyStars != null) _dizzyStars.Visible = false;
                if (_tagPlate != null) { _tagPlate.Text = "[ D I E ]"; _tagPlate.Modulate = new Color(1.0f, 0.1f, 0.1f); }
                SetHeadlightEmission(6.0f);
                break;

            case CartState.Stunned:
                _crashPlayer?.Play();
                if (_sparkParticles != null) _sparkParticles.Emitting = false;
                if (_dizzyStars != null) _dizzyStars.Visible = true;
                if (_tagPlate != null) { _tagPlate.Text = "[ 4 0 4 ]"; _tagPlate.Modulate = new Color(0.6f, 0.6f, 0.6f); }
                SetHeadlightEmission(0.4f);
                break;
        }
    }

    private void SetHeadlightEmission(float energy)
    {
        if (_headlightMat != null)
        {
            _headlightMat.Emission = new Color(energy, 0.1f * energy, 0.1f * energy);
        }
        if (_spotLightLeft != null) _spotLightLeft.LightEnergy = energy * 0.8f;
        if (_spotLightRight != null) _spotLightRight.LightEnergy = energy * 0.8f;
        if (_underglow != null) _underglow.LightEnergy = energy * 0.5f;
    }

    private void MoveAlongNavPath(float speed, ref Vector3 velocity)
    {
        if (NavAgent == null || NavAgent.IsNavigationFinished())
        {
            velocity.X = 0;
            velocity.Z = 0;
            return;
        }

        Vector3 nextPos = NavAgent.GetNextPathPosition();
        Vector3 dir = nextPos - GlobalPosition;
        dir.Y = 0;

        if (dir.LengthSquared() > 0.01f)
        {
            LookAt(GlobalPosition + dir, Vector3.Up);
            velocity.X = dir.Normalized().X * speed * SpeedMultiplier;
            velocity.Z = dir.Normalized().Z * speed * SpeedMultiplier;
        }
    }

    public void SetRandomPatrolTarget()
    {
        if (NavAgent == null) return;
        Rid map = NavAgent.GetNavigationMap();
        Vector3 randomPoint = NavigationServer3D.MapGetRandomPoint(map, NavAgent.NavigationLayers, false);
        NavAgent.TargetPosition = randomPoint;
    }

    #endregion

    #region Perception & Senses

    public void DetectPlayer()
    {
        _targetPlayer = null;
        float closestDist = DetectionRange;

        foreach (Node node in GetTree().GetNodesInGroup("Players"))
        {
            if (node is PlayerController player && player.Health != null && !player.Health.IsDead)
            {
                float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
                if (dist <= closestDist && CanSeePlayer(player, DetectionRange, DetectionFov))
                {
                    closestDist = dist;
                    _targetPlayer = player;
                }
            }
        }
    }

    public bool CanSeePlayer(PlayerController player, float maxRange, float fovDegrees = 130f)
    {
        if (player == null || !GodotObject.IsInstanceValid(player) || player.Health == null || player.Health.IsDead)
            return false;

        Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
        float dist = toPlayer.Length();
        if (dist > maxRange) return false;

        // FOV cone check
        if (fovDegrees < 350f)
        {
            Vector3 forward = -GlobalTransform.Basis.Z;
            forward.Y = 0;
            Vector3 dirFlat = new Vector3(toPlayer.X, 0, toPlayer.Z);
            if (forward.LengthSquared() > 0.001f && dirFlat.LengthSquared() > 0.001f)
            {
                float angle = Mathf.RadToDeg(forward.Normalized().AngleTo(dirFlat.Normalized()));
                if (angle > fovDegrees * 0.5f) return false;
            }
        }

        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return false;

        Vector3 eyePos = GlobalPosition + Vector3.Up * 0.6f;
        Vector3 targetPos = player.GlobalPosition + Vector3.Up * 0.9f;

        var query = PhysicsRayQueryParameters3D.Create(eyePos, targetPos);
        query.CollisionMask = 1; // Layer 1: World geometry (shelves, walls)
        var result = spaceState.IntersectRay(query);
        return result.Count == 0;
    }

    #endregion

    #region Damage & Destruction

    public void TakeDamage(int amount, Node3D source = null)
    {
        if (_isDestroyed) return;

        if (!Multiplayer.IsServer())
        {
            if (Multiplayer.HasMultiplayerPeer())
            {
                RpcId(1, nameof(RpcRequestCartDamage), amount, source != null ? source.GetPath() : new NodePath());
            }
            return;
        }

        if (Health == null) return;
        Health.TakeDamage(amount);

        // Interrupt charging if hit with a heavy weapon!
        if (_state == CartState.Charge && amount >= 28)
        {
            _crashPlayer?.Play();
            RpcShowReaction("INTERRUPTED!", new Color(1.0f, 0.85f, 0.1f));
            SetCartState(CartState.Stunned);
            return;
        }

        // If attacked while idling/patrolling, retaliate by turning towards the attacker!
        if (_state == CartState.Patrol && source is PlayerController attacker && !attacker.Health.IsDead)
        {
            _targetPlayer = attacker;
            SetCartState(CartState.Windup);
        }

        if (Health.IsDead)
        {
            Die(source as PlayerController);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void RpcRequestCartDamage(int amount, NodePath sourcePath)
    {
        if (!Multiplayer.IsServer()) return;
        Node3D source = !sourcePath.IsEmpty ? GetNodeOrNull<Node3D>(sourcePath) : null;
        TakeDamage(amount, source);
    }

    public void Die(PlayerController killer = null)
    {
        if (_isDestroyed) return;
        _isDestroyed = true;
        if (!Multiplayer.IsServer()) return;

        GD.Print("[PossessedCart] Cartsferatu destroyed!");

        // Reward killer
        if (killer != null && GodotObject.IsInstanceValid(killer))
        {
            killer.AddMoney(50);
            FloatingDamageNumber.SpawnText(killer, killer.GlobalPosition + Vector3.Up * 1.8f, "CARTSFERATU KILLED! +$50", new Color(1.0f, 0.85f, 0.1f), 48);
        }

        ManagerAnnouncer.Instance?.AnnounceCartDestroyed();
        SpawnLootDrop();

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcDestroyCart));
        }
        else
        {
            RpcDestroyCart();
        }
    }

    public void Die()
    {
        Die(null);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcDestroyCart()
    {
        _isDestroyed = true;
        FloatingDamageNumber.SpawnText(this, GlobalPosition + Vector3.Up * 1.1f, "WRECKED!", new Color(1.0f, 0.15f, 0.2f), 56);
        SpawnExplosion();
        QueueFree();
    }

    private void SpawnExplosion()
    {
        PackedScene scene = ExplosionScene ?? GD.Load<PackedScene>("res://Prefabs/Explosion.tscn");
        if (scene != null)
        {
            Node3D explosion = scene.Instantiate<Node3D>();
            Node parent = GetTree().CurrentScene ?? GetParent();
            if (parent != null)
            {
                parent.AddChild(explosion);
                explosion.GlobalPosition = GlobalPosition + Vector3.Up * 0.5f;
            }
        }
    }

    private void SpawnLootDrop()
    {
        string[] highTierWeapons = {
            "res://Prefabs/Products/Sledgehammer.tscn",
            "res://Prefabs/Products/Watermelon.tscn",
            "res://Prefabs/Products/FryingPan.tscn",
            "res://Prefabs/Products/SodaCan.tscn"
        };

        Node parent = GetTree().CurrentScene ?? GetParent();
        if (parent == null) return;

        var rng = new RandomNumberGenerator();
        // Drop 2 items from the cart basket
        for (int i = 0; i < 2; i++)
        {
            string path = highTierWeapons[rng.Randi() % highTierWeapons.Length];
            if (ResourceLoader.Exists(path))
            {
                PackedScene scene = GD.Load<PackedScene>(path);
                if (scene != null && scene.Instantiate() is Product item)
                {
                    parent.AddChild(item);
                    item.GlobalPosition = GlobalPosition + Vector3.Up * (0.8f + i * 0.3f);
                    item.IsForSale = false;
                    item.WasBought = true;
                    item.CanBePickedUp = true;
                    item.Freeze = false;
                    item.ActivatePhysicsAndSync();
                    item.LinearVelocity = new Vector3(rng.RandfRange(-3f, 3f), 4.5f, rng.RandfRange(-3f, 3f));
                }
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcShowReaction(string text, Color color)
    {
        FloatingDamageNumber.SpawnText(this, GlobalPosition + Vector3.Up * 1.35f, text, color, 46);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        SpeedMultiplier = multiplier;
    }

    #endregion
}
