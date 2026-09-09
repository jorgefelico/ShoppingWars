using Godot;
using System.Collections.Generic;

public partial class ArenaZoneManager : Node3D
{
    public static ArenaZoneManager Instance { get; private set; }

    [Export] public float InitialRadius = 116.0f;
    [Export] public float Stage1Radius = 58.0f;
    [Export] public float Stage2Radius = 28.0f;
    [Export] public float FinalRadius = 12.0f;
    [Export] public int ZoneDamagePerSecond = 6;
    [Export] public Vector3 ZoneCenter = Vector3.Zero;

    public float CurrentRadius { get; private set; } = 116.0f;
    public float TargetRadius { get; private set; } = 116.0f;
    public bool IsActive { get; private set; } = false;
    public bool IsShrinking { get; private set; } = false;
    public string ZoneStatusMessage { get; private set; } = "SAFE";

    private MeshInstance3D _barrierMesh;
    private StandardMaterial3D _barrierMaterial;
    private AudioStreamPlayer _hazardAudioPlayer;
    private float _damageTimer = 0f;
    private float _syncTimer = 0f;
    private float _hazardSoundTimer = 0f;
    private bool _wasShrinking = false;
    private float _syncedTargetRadius = 116.0f;
    private const float SyncInterval = 0.35f;

    public override void _Ready()
    {
        Instance = this;
        CurrentRadius = InitialRadius;
        TargetRadius = InitialRadius;
        _syncedTargetRadius = InitialRadius;

        CreateBarrierVisual();
        CreateHazardAudioPlayer();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged += OnGamePhaseChanged;
            if (GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale)
            {
                ActivateZone();
            }
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void CreateBarrierVisual()
    {
        _barrierMesh = new MeshInstance3D();
        _barrierMesh.Name = "StormBarrierMesh";
        _barrierMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // Create a cylinder mesh for the hazard wall
        CylinderMesh cylinder = new CylinderMesh();
        cylinder.TopRadius = 1.0f;
        cylinder.BottomRadius = 1.0f;
        cylinder.Height = 12.0f;
        cylinder.RadialSegments = 64;
        cylinder.CapTop = false;
        cylinder.CapBottom = false;

        _barrierMaterial = new StandardMaterial3D();
        _barrierMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _barrierMaterial.AlbedoColor = new Color(0.2f, 0.7f, 1.0f, 0.20f);
        _barrierMaterial.EmissionEnabled = true;
        _barrierMaterial.Emission = new Color(0.1f, 0.6f, 1.0f);
        _barrierMaterial.EmissionEnergyMultiplier = 2.0f;
        _barrierMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Visible from both sides

        _barrierMesh.Mesh = cylinder;
        _barrierMesh.MaterialOverride = _barrierMaterial;
        _barrierMesh.Visible = false;
        _barrierMesh.Position = new Vector3(ZoneCenter.X, 4.5f, ZoneCenter.Z);
        _barrierMesh.Scale = new Vector3(CurrentRadius, 1.0f, CurrentRadius);

        AddChild(_barrierMesh);
    }

    private void CreateHazardAudioPlayer()
    {
        _hazardAudioPlayer = new AudioStreamPlayer();
        _hazardAudioPlayer.Name = "HazardAudioPlayer";
        _hazardAudioPlayer.Bus = "SFX";
        _hazardAudioPlayer.VolumeDb = -6.0f;
        _hazardAudioPlayer.Stream = CreateHazardWarningTone();
        AddChild(_hazardAudioPlayer);
    }

    private AudioStreamWav CreateHazardWarningTone()
    {
        int sampleRate = 22050;
        float duration = 0.12f;
        int sampleCount = (int)(sampleRate * duration);
        byte[] data = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Clamp(1.0f - (t / duration), 0f, 1f);
            float sample = Mathf.Sin(t * Mathf.Tau * 650.0f) * env * 0.35f;
            short pcm = (short)(sample * short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav();
        wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
        wav.MixRate = sampleRate;
        wav.Data = data;
        return wav;
    }

    private void OnGamePhaseChanged()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale)
        {
            ActivateZone();
        }
        else
        {
            ResetZone();
        }
    }

    public void ActivateZone()
    {
        IsActive = true;
        CurrentRadius = InitialRadius;
        TargetRadius = InitialRadius;
        _syncedTargetRadius = InitialRadius;
        IsShrinking = false;
        _wasShrinking = false;
        ZoneStatusMessage = "SAFE";

        if (_barrierMesh != null)
        {
            _barrierMesh.Visible = true;
            _barrierMesh.Scale = new Vector3(CurrentRadius, 1.0f, CurrentRadius);
        }
    }

    public void ResetZone()
    {
        IsActive = false;
        CurrentRadius = InitialRadius;
        TargetRadius = InitialRadius;
        _syncedTargetRadius = InitialRadius;
        IsShrinking = false;
        _wasShrinking = false;
        ZoneStatusMessage = "SAFE";

        if (_barrierMesh != null)
        {
            _barrierMesh.Visible = false;
        }

        if (Multiplayer.IsServer() && Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncZone), CurrentRadius, TargetRadius, false, ZoneStatusMessage, false);
        }
    }

    public override void _Process(double delta)
    {
        if (!IsActive) return;

        if (Multiplayer.IsServer())
        {
            UpdateServerStage();

            // Notify all players when shrinking starts
            if (IsShrinking && !_wasShrinking)
            {
                OnShrinkPhaseStarted();
            }
            _wasShrinking = IsShrinking;

            // Periodic zone damage to players outside radius
            _damageTimer += (float)delta;
            if (_damageTimer >= 1.0f)
            {
                _damageTimer = 0f;
                ApplyZoneDamage();
            }

            // Sync authoritative state to clients
            _syncTimer += (float)delta;
            if (_syncTimer >= SyncInterval)
            {
                _syncTimer = 0f;
                if (Multiplayer.HasMultiplayerPeer())
                {
                    Rpc(nameof(RpcSyncZone), CurrentRadius, TargetRadius, IsShrinking, ZoneStatusMessage, IsActive);
                }
            }
        }
        else
        {
            // Client smoothly catches up to server target/radius
            if (IsShrinking)
            {
                CurrentRadius = Mathf.MoveToward(CurrentRadius, _syncedTargetRadius, (float)delta * 2.5f);
            }
            else
            {
                CurrentRadius = Mathf.MoveToward(CurrentRadius, TargetRadius, (float)delta * 5.0f);
            }
        }

        // Update barrier mesh scale and pulse animation
        if (_barrierMesh != null && _barrierMesh.Visible)
        {
            _barrierMesh.Scale = new Vector3(CurrentRadius, 1.0f, CurrentRadius);
            float pulse = 1.5f + Mathf.Sin((float)Time.GetTicksMsec() * 0.005f) * 0.5f;

            if (_barrierMaterial != null)
            {
                _barrierMaterial.EmissionEnergyMultiplier = pulse;
                if (IsShrinking)
                {
                    _barrierMaterial.AlbedoColor = new Color(1.0f, 0.2f, 0.1f, 0.35f);
                    _barrierMaterial.Emission = new Color(1.0f, 0.15f, 0.05f);
                }
                else
                {
                    _barrierMaterial.AlbedoColor = new Color(0.2f, 0.7f, 1.0f, 0.20f);
                    _barrierMaterial.Emission = new Color(0.1f, 0.6f, 1.0f);
                }
            }
        }

        // Local hazard warning sound if local player is outside safe zone
        UpdateLocalHazardAudio((float)delta);
    }

    private void OnShrinkPhaseStarted()
    {
        if (GameManager.Instance == null) return;
        AudioStream warningSound = GameManager.Instance.TransitionChime;
        if (warningSound != null)
        {
            GameManager.Instance.PlaySoundOnSpeakers(warningSound, 0.0f, true);
        }
    }

    private void UpdateLocalHazardAudio(float delta)
    {
        PlayerController localPlayer = PlayerController.Instance;
        if (localPlayer == null || !GodotObject.IsInstanceValid(localPlayer)) return;

        bool isSpectating = localPlayer.IsSpectating || (localPlayer.Health != null && localPlayer.Health.IsDead);
        if (isSpectating) return;

        if (IsPlayerOutside(localPlayer.GlobalPosition))
        {
            _hazardSoundTimer += delta;
            if (_hazardSoundTimer >= 1.0f)
            {
                _hazardSoundTimer = 0f;
                if (_hazardAudioPlayer != null && GodotObject.IsInstanceValid(_hazardAudioPlayer))
                {
                    _hazardAudioPlayer.Play();
                }
            }
        }
        else
        {
            _hazardSoundTimer = 0.8f; // Prime it to sound promptly if player steps outside
        }
    }

    private void UpdateServerStage()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentPhase != GamePhase.BattleRoyale) return;

        float elapsed = GameManager.Instance.BattleDuration - GameManager.Instance.TimeRemaining;

        // Stage timings based on elapsed battle royale time:
        // 0-45s: Full store open (116m)
        // 45-90s: Continuous shrink to 58m (aisle perimeter)
        // 90-130s: Stable at 58m
        // 130-170s: Continuous shrink to 28m (center departments)
        // 170-200s: Stable at 28m
        // 200-235s: Continuous final shrink to 12m (central aisles)
        // 235-240s: Final showdown at 12m
        if (elapsed < 45f)
        {
            CurrentRadius = InitialRadius;
            TargetRadius = InitialRadius;
            IsShrinking = false;
            ZoneStatusMessage = "SAFE (Lockdown in " + Mathf.CeilToInt(45f - elapsed) + "s)";
        }
        else if (elapsed < 90f)
        {
            float t = (elapsed - 45f) / 45f;
            TargetRadius = Stage1Radius;
            CurrentRadius = Mathf.Lerp(InitialRadius, Stage1Radius, t);
            IsShrinking = true;
            ZoneStatusMessage = "PERIMETER LOCKDOWN CLOSING IN! (" + Mathf.CeilToInt(90f - elapsed) + "s)";
        }
        else if (elapsed < 130f)
        {
            CurrentRadius = Stage1Radius;
            TargetRadius = Stage1Radius;
            IsShrinking = false;
            ZoneStatusMessage = "ZONE STABLE (Next shrink in " + Mathf.CeilToInt(130f - elapsed) + "s)";
        }
        else if (elapsed < 170f)
        {
            float t = (elapsed - 130f) / 40f;
            TargetRadius = Stage2Radius;
            CurrentRadius = Mathf.Lerp(Stage1Radius, Stage2Radius, t);
            IsShrinking = true;
            ZoneStatusMessage = "STORE CLOSING IN - MOVE TO CENTER! (" + Mathf.CeilToInt(170f - elapsed) + "s)";
        }
        else if (elapsed < 200f)
        {
            CurrentRadius = Stage2Radius;
            TargetRadius = Stage2Radius;
            IsShrinking = false;
            ZoneStatusMessage = "ZONE STABLE (Final shrink in " + Mathf.CeilToInt(200f - elapsed) + "s)";
        }
        else if (elapsed < 235f)
        {
            float t = (elapsed - 200f) / 35f;
            TargetRadius = FinalRadius;
            CurrentRadius = Mathf.Lerp(Stage2Radius, FinalRadius, t);
            IsShrinking = true;
            ZoneStatusMessage = "FINAL SHOWDOWN CLOSING IN! (" + Mathf.CeilToInt(235f - elapsed) + "s)";
        }
        else
        {
            CurrentRadius = FinalRadius;
            TargetRadius = FinalRadius;
            IsShrinking = false;
            ZoneStatusMessage = "FINAL SHOWDOWN IN PROGRESS!";
        }
    }

    private void ApplyZoneDamage()
    {
        if (GameManager.Instance == null) return;

        foreach (var player in GameManager.Instance.GetLivingPlayers())
        {
            if (player == null || !GodotObject.IsInstanceValid(player)) continue;

            float dist2D = new Vector2(player.GlobalPosition.X - ZoneCenter.X, player.GlobalPosition.Z - ZoneCenter.Z).Length();
            if (dist2D > CurrentRadius)
            {
                player.TakeDamage(ZoneDamagePerSecond, null);
                GD.Print($"[ArenaZone] Player '{player.PlayerName}' outside safe zone ({dist2D:0.0}m > {CurrentRadius:0.0}m), dealt {ZoneDamagePerSecond} dmg");
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcSyncZone(float currentRadius, float targetRadius, bool isShrinking, string statusMessage, bool isActive)
    {
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer()) return;

        IsActive = isActive;
        _syncedTargetRadius = targetRadius;
        TargetRadius = targetRadius;
        IsShrinking = isShrinking;
        ZoneStatusMessage = statusMessage;

        // If client is noticeably behind or ahead, synchronize radius directly
        if (Mathf.Abs(CurrentRadius - currentRadius) > 3.0f || !IsShrinking)
        {
            CurrentRadius = currentRadius;
        }

        if (_barrierMesh != null)
        {
            _barrierMesh.Visible = IsActive;
        }
    }

    public bool IsPlayerOutside(Vector3 playerPos)
    {
        if (!IsActive) return false;
        float dist2D = new Vector2(playerPos.X - ZoneCenter.X, playerPos.Z - ZoneCenter.Z).Length();
        return dist2D > CurrentRadius;
    }

    public float GetDistanceToSafeZone(Vector3 playerPos)
    {
        float dist2D = new Vector2(playerPos.X - ZoneCenter.X, playerPos.Z - ZoneCenter.Z).Length();
        return Mathf.Max(0f, dist2D - CurrentRadius);
    }
}
