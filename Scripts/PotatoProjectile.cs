using Godot;
using System;

public partial class PotatoProjectile : RigidBody3D
{
    [Export] public int BaseDamage = 45;
    [Export] public float Lifespan = 4.5f;
    [Export] public PackedScene SplatterScene;
    [Export] public bool IsSuperSpud { get; set; } = false;

    public PlayerController Shooter { get; set; }
    private bool _hasImpacted = false;
    private float _lifeTimer = 0f;
    private Vector3 _lastVelocity;

    public override void _Ready()
    {
        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 4;
        GravityScale = 0.35f;

        BodyEntered += OnBodyEntered;

        // Tumble as it flies
        AngularVelocity = new Vector3(
            (float)GD.RandRange(-15.0, 15.0),
            (float)GD.RandRange(-12.0, 12.0),
            (float)GD.RandRange(-18.0, 18.0)
        );
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hasImpacted) return;

        _lifeTimer += (float)delta;
        if (_lifeTimer >= Lifespan)
        {
            QueueFree();
            return;
        }

        // Detect flying spud near-misses with Groomba hazards in real-time
        if (Shooter != null && LinearVelocity.LengthSquared() > 80.0f)
        {
            Groomba.CheckProjectileNearMiss(null, GlobalPosition, LinearVelocity, Shooter);
        }

        _lastVelocity = LinearVelocity;
    }

    private void OnBodyEntered(Node body)
    {
        if (_hasImpacted) return;
        if (body == Shooter) return;

        _hasImpacted = true;
        Vector3 impactPos = GlobalPosition;

        // Damage target (only damage other players if in Battle Royale or standalone testing)
        if (body is IDamageable target)
        {
            bool canDamage = (GameManager.Instance == null || GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale || !(body is PlayerController));
            if (canDamage)
            {
                float perkMultiplier = (Shooter != null && Shooter.CurrentPerk == PlayerPerk.PowerArm) ? 1.25f : 1.0f;
                int spudDmg = IsSuperSpud ? (BaseDamage + 15) : BaseDamage;
                int finalDmg = Mathf.RoundToInt(spudDmg * PlayerController.GlobalDamageMultiplier * perkMultiplier);

                target.TakeDamage(finalDmg, Shooter);

                if (Shooter != null && GodotObject.IsInstanceValid(Shooter))
                {
                    bool isKill = (target is PlayerController victim && victim.Health != null && victim.Health.CurrentHealth <= 0);
                    if (Shooter.IsMultiplayerAuthority())
                    {
                        Shooter.TriggerHitMarker(isKill);
                    }
                    else if (int.TryParse(Shooter.Name, out int shooterPeerId) && Multiplayer.HasMultiplayerPeer())
                    {
                        Shooter.RpcId(shooterPeerId, nameof(PlayerController.RpcConfirmHit), finalDmg, isKill);
                    }

                    if (isKill && target is PlayerController victimPlayer)
                    {
                        ManagerAnnouncer.Instance?.AnnounceElimination(Shooter.PlayerName, victimPlayer.PlayerName, "Potato Gun");
                    }
                }
            }
        }

        // Alert sentient Groomba of the disturbance
        Groomba.NotifyDisturbance(null, impactPos, Shooter);

        // Spawn mashed potato splatter effect
        SpawnSplatter(impactPos);

        QueueFree();
    }

    private void SpawnSplatter(Vector3 pos)
    {
        PackedScene splatScene = SplatterScene ?? GD.Load<PackedScene>("res://Prefabs/PotatoSplatter.tscn");
        if (splatScene != null)
        {
            Node3D effect = splatScene.Instantiate<Node3D>();
            Node scene = GetTree().CurrentScene ?? GetParent();
            if (scene != null && effect != null)
            {
                scene.AddChild(effect);
                effect.GlobalPosition = pos;
            }
        }
    }
}
