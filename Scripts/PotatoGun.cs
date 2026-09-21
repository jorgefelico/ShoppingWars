using Godot;
using System;

public partial class PotatoGun : Product
{
    [Export] public int MaxAmmo = 6;
    [Export] public int CurrentAmmo = 6;
    [Export] public float FireRate = 0.38f;
    [Export] public float ProjectileSpeed = 48.0f;
    [Export] public float ReloadTime = 1.4f;
    [Export] public AudioStream FireSound;
    [Export] public AudioStream ReloadSound;
    [Export] public AudioStream EmptySound;
    [Export] public bool HasSuperSpuds { get; set; } = false;

    public bool IsReloading { get; private set; } = false;
    private float _reloadTimer = 0f;
    private float _fireCooldown = 0f;
    private Marker3D _muzzle;
    private GpuParticles3D _muzzlePuff;
    private AudioStreamPlayer3D _audioPlayer;
    private PlayerController _currentShooter;

    public override void _Ready()
    {
        base._Ready();

        // Sturdy PVC pipe does not shatter easily
        DestroyOnImpact = false;
        MeleeDurability = 999;
        CurrentDurability = 999;

        _muzzle = GetNodeOrNull<Marker3D>("Muzzle");
        _muzzlePuff = GetNodeOrNull<GpuParticles3D>("Muzzle/MuzzlePuff");
        _audioPlayer = GetNodeOrNull<AudioStreamPlayer3D>("AudioStreamPlayer3D");

        if (FireSound == null && ResourceLoader.Exists("res://Sounds/potatogun_fire.wav"))
        {
            FireSound = GD.Load<AudioStream>("res://Sounds/potatogun_fire.wav");
        }
        if (ReloadSound == null && ResourceLoader.Exists("res://Sounds/potatogun_reload.wav"))
        {
            ReloadSound = GD.Load<AudioStream>("res://Sounds/potatogun_reload.wav");
        }
        if (EmptySound == null && ResourceLoader.Exists("res://Sounds/potatogun_empty.wav"))
        {
            EmptySound = GD.Load<AudioStream>("res://Sounds/potatogun_empty.wav");
        }

        if (_audioPlayer != null && FireSound != null)
        {
            _audioPlayer.Stream = FireSound;
            _audioPlayer.Bus = "SFX";
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        float dt = (float)delta;
        if (_fireCooldown > 0f)
        {
            _fireCooldown -= dt;
        }

        if (IsReloading)
        {
            _reloadTimer -= dt;
            if (_reloadTimer <= 0f)
            {
                FinishReload();
            }
        }
    }

    public bool CanFire()
    {
        return !IsReloading && _fireCooldown <= 0f && CurrentAmmo > 0;
    }

    public Vector3 GetMuzzleGlobalPosition()
    {
        if (_muzzle != null && GodotObject.IsInstanceValid(_muzzle))
        {
            return _muzzle.GlobalPosition;
        }
        return GlobalPosition + (-GlobalBasis.Z * 0.80f) + (GlobalBasis.Y * 0.145f);
    }

    public void OnFired(Vector3 muzzlePos, Vector3 launchVel)
    {
        CurrentAmmo = Mathf.Max(0, CurrentAmmo - 1);
        _fireCooldown = FireRate;

        // Play spatial audio
        if (_audioPlayer != null)
        {
            _audioPlayer.Stream = FireSound;
            _audioPlayer.PitchScale = (float)GD.RandRange(0.95, 1.05);
            _audioPlayer.Play();
        }

        // Trigger compressed air puff at barrel tip
        if (_muzzlePuff != null)
        {
            _muzzlePuff.Restart();
            _muzzlePuff.Emitting = true;
        }
    }

    public bool TryReload(PlayerController shooter)
    {
        if (IsReloading || CurrentAmmo >= MaxAmmo) return false;

        _currentShooter = shooter;
        IsReloading = true;
        _reloadTimer = ReloadTime;

        // Check if player has fresh store potatoes in inventory for super-spuds!
        bool loadedFreshPotato = false;
        if (shooter != null && shooter.Inventory != null)
        {
            Product storeSpud = shooter.Inventory.ConsumeItemByName("Potato");
            if (storeSpud != null)
            {
                loadedFreshPotato = true;
                HasSuperSpuds = true;
                storeSpud.QueueFree();
                shooter.ShowAmmoAlert("🥔 LOADED STORE SPUDS! (+15 SUPER DMG)");
            }
        }

        if (!loadedFreshPotato)
        {
            HasSuperSpuds = false;
            shooter?.ShowAmmoAlert("💨 PNEUMATIC PUMP RELOAD (6 SPUDS)");
        }

        if (_audioPlayer != null && ReloadSound != null)
        {
            _audioPlayer.Stream = ReloadSound;
            _audioPlayer.PitchScale = loadedFreshPotato ? 1.15f : 1.0f;
            _audioPlayer.Play();
        }

        return true;
    }

    public void PlayEmptySound()
    {
        if (_audioPlayer != null && EmptySound != null)
        {
            _audioPlayer.Stream = EmptySound;
            _audioPlayer.PitchScale = (float)GD.RandRange(0.96, 1.04);
            _audioPlayer.Play();
        }
    }

    private void FinishReload()
    {
        IsReloading = false;
        CurrentAmmo = MaxAmmo;

        if (_currentShooter != null && GodotObject.IsInstanceValid(_currentShooter))
        {
            _currentShooter.OnPotatoGunReloadComplete();
        }
    }
}
