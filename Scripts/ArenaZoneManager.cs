using Godot;
using System.Collections.Generic;

public partial class ArenaZoneManager : Node3D
{
    private static ArenaZoneManager _instance;
    public static ArenaZoneManager Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [Export] public float InitialRadius = 116.0f;
    [Export] public float Stage1Radius = 58.0f;
    [Export] public float Stage2Radius = 28.0f;
    [Export] public float FinalRadius = 12.0f;
    [Export] public int ZoneDamagePerSecond = 6;

    public Vector3 CurrentCenter { get; private set; } = Vector3.Zero;
    public Vector3 TargetCenter { get; private set; } = Vector3.Zero;
    public Vector3 ZoneCenter => CurrentCenter; // Backwards compatibility for external callers

    public float CurrentRadius { get; private set; } = 116.0f;
    public float TargetRadius { get; private set; } = 116.0f;
    public bool IsActive { get; private set; } = false;
    public bool IsShrinking { get; private set; } = false;
    public string ZoneStatusMessage { get; private set; } = "SAFE";

    public string TargetDepartmentName => GetDepartmentName(TargetCenter);
    public string CurrentDepartmentName => GetDepartmentName(CurrentCenter);

    private Vector3 _stage0Center = Vector3.Zero;
    private Vector3 _stage1Center = Vector3.Zero;
    private Vector3 _stage2Center = Vector3.Zero;
    private Vector3 _stage3Center = Vector3.Zero;
    private bool _hasGeneratedCenters = false;

    private MeshInstance3D _barrierMesh;
    private StandardMaterial3D _barrierMaterial;
    private AudioStreamPlayer _hazardAudioPlayer;
    private float _damageTimer = 0f;
    private float _syncTimer = 0f;
    private float _hazardSoundTimer = 0f;
    private bool _wasShrinking = false;
    private float _syncedTargetRadius = 116.0f;
    private Vector3 _syncedTargetCenter = Vector3.Zero;
    private const float SyncInterval = 0.35f;

    public override void _Ready()
    {
        Instance = this;
        CurrentRadius = InitialRadius;
        TargetRadius = InitialRadius;
        _syncedTargetRadius = InitialRadius;
        CurrentCenter = Vector3.Zero;
        TargetCenter = Vector3.Zero;
        _syncedTargetCenter = Vector3.Zero;
        _hasGeneratedCenters = false;

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
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void CreateBarrierVisual()
    {
        _barrierMesh = new MeshInstance3D();
        _barrierMesh.Name = "StormBarrierMesh";
        _barrierMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // Create a cylinder mesh for the hazard wall spanning floor to ceiling
        CylinderMesh cylinder = new CylinderMesh();
        cylinder.TopRadius = 1.0f;
        cylinder.BottomRadius = 1.0f;
        cylinder.Height = 16.0f;
        cylinder.RadialSegments = 64;
        cylinder.CapTop = false;
        cylinder.CapBottom = false;

        _barrierMaterial = new StandardMaterial3D();
        _barrierMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _barrierMaterial.AlbedoColor = new Color(0.2f, 0.7f, 1.0f, 0.22f);
        _barrierMaterial.EmissionEnabled = true;
        _barrierMaterial.Emission = new Color(0.15f, 0.65f, 1.0f);
        _barrierMaterial.EmissionEnergyMultiplier = 2.5f;
        _barrierMaterial.RimEnabled = true;
        _barrierMaterial.Rim = 0.8f;
        _barrierMaterial.RimTint = 0.7f;
        _barrierMaterial.Roughness = 0.1f;
        _barrierMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Visible from both sides

        _barrierMesh.Mesh = cylinder;
        _barrierMesh.MaterialOverride = _barrierMaterial;
        _barrierMesh.Visible = false;
        _barrierMesh.Position = new Vector3(CurrentCenter.X, 7.0f, CurrentCenter.Z);
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

    /// <summary>
    /// Generates randomized, nested battle royale circle centers for unpredictable match outcomes.
    /// Stage 3 (Final showdown) is chosen randomly in the playable store floor.
    /// Stage 2 is chosen to contain Stage 3.
    /// Stage 1 is chosen to contain Stage 2.
    /// Stage 0 starts at supermarket center (0,0) encompassing the whole building.
    /// </summary>
    private void GenerateZoneCenters()
    {
        _stage0Center = Vector3.Zero;

        // 1. Pick random final showdown center anywhere in playable supermarket interior
        // Floor size is 180m x 140m (X: [-90, 90], Z: [-70, 70]).
        // With FinalRadius = 12m, picking in [-64, 64] x [-46, 46] ensures 100% of final zone is on playable floor.
        float finalX = (float)GD.RandRange(-64.0f, 64.0f);
        float finalZ = (float)GD.RandRange(-46.0f, 46.0f);
        _stage3Center = new Vector3(finalX, 0f, finalZ);

        // 2. Pick Stage 2 center (28m radius) ensuring Stage 3 (12m radius) is contained inside it.
        // Maximum center distance allowed = R2 - R3 = 28 - 12 = 16m. We use up to 14m offset.
        float angle2 = (float)GD.RandRange(0f, Mathf.Tau);
        float dist2 = (float)GD.RandRange(0f, 14.0f);
        float s2X = Mathf.Clamp(_stage3Center.X + Mathf.Cos(angle2) * dist2, -60.0f, 60.0f);
        float s2Z = Mathf.Clamp(_stage3Center.Z + Mathf.Sin(angle2) * dist2, -42.0f, 42.0f);
        _stage2Center = new Vector3(s2X, 0f, s2Z);

        // 3. Pick Stage 1 center (58m radius) ensuring Stage 2 (28m radius) is contained inside it.
        // Maximum center distance allowed = R1 - R2 = 58 - 28 = 30m. We use up to 24m offset.
        float angle1 = (float)GD.RandRange(0f, Mathf.Tau);
        float dist1 = (float)GD.RandRange(0f, 24.0f);
        float s1X = Mathf.Clamp(_stage2Center.X + Mathf.Cos(angle1) * dist1, -38.0f, 38.0f);
        float s1Z = Mathf.Clamp(_stage2Center.Z + Mathf.Sin(angle1) * dist1, -26.0f, 26.0f);
        _stage1Center = new Vector3(s1X, 0f, s1Z);

        _hasGeneratedCenters = true;

        string deptFinal = GetDepartmentName(_stage3Center);
        string deptS2 = GetDepartmentName(_stage2Center);
        string deptS1 = GetDepartmentName(_stage1Center);
        GD.Print($"[ArenaZoneManager] Generated Unpredictable Zone Centers:");
        GD.Print($"  Stage 0 (116m): {_stage0Center} (Whole Store)");
        GD.Print($"  Stage 1 (58m):  {_stage1Center} ({deptS1})");
        GD.Print($"  Stage 2 (28m):  {_stage2Center} ({deptS2})");
        GD.Print($"  Stage 3 (12m):  {_stage3Center} ({deptFinal})");
    }

    public static string GetDepartmentName(Vector3 pos)
    {
        (string Name, Vector2 Pos)[] landmarks = new[]
        {
            ("Produce", new Vector2(-60f, -46f)),
            ("Bakery & Deli", new Vector2(-60f, 50f)),
            ("Pantry & Snacks", new Vector2(-60f, 0f)),
            ("Express Checkout", new Vector2(0f, -55f)),
            ("Center Aisles", new Vector2(0f, 0f)),
            ("Back Storage", new Vector2(0f, 55f)),
            ("Tech & Electronics", new Vector2(60f, -25f)),
            ("Apparel & Gear", new Vector2(60f, 15f)),
            ("Health & Pharmacy", new Vector2(65f, 50f)),
            ("West Aisles", new Vector2(-30f, -15f)),
            ("East Aisles", new Vector2(30f, -15f))
        };

        string closest = "Store Interior";
        float minDstSq = float.MaxValue;
        Vector2 p2D = new Vector2(pos.X, pos.Z);

        foreach (var landmark in landmarks)
        {
            float dstSq = p2D.DistanceSquaredTo(landmark.Pos);
            if (dstSq < minDstSq)
            {
                minDstSq = dstSq;
                closest = landmark.Name;
            }
        }

        return closest;
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

        if (Multiplayer.IsServer())
        {
            GenerateZoneCenters();
            CurrentCenter = _stage0Center;
            TargetCenter = _stage1Center;
            _syncedTargetCenter = _stage0Center;
            if (Multiplayer.HasMultiplayerPeer())
            {
                Rpc(nameof(RpcInitZoneCenters), _stage0Center, _stage1Center, _stage2Center, _stage3Center);
            }
        }
        else
        {
            CurrentCenter = Vector3.Zero;
            TargetCenter = Vector3.Zero;
            _syncedTargetCenter = Vector3.Zero;
        }

        if (_barrierMesh != null)
        {
            _barrierMesh.Visible = true;
            _barrierMesh.Position = new Vector3(CurrentCenter.X, 7.0f, CurrentCenter.Z);
            _barrierMesh.Scale = new Vector3(CurrentRadius, 1.0f, CurrentRadius);
        }
    }

    public void ResetZone()
    {
        IsActive = false;
        CurrentRadius = InitialRadius;
        TargetRadius = InitialRadius;
        _syncedTargetRadius = InitialRadius;
        CurrentCenter = Vector3.Zero;
        TargetCenter = Vector3.Zero;
        _syncedTargetCenter = Vector3.Zero;
        _hasGeneratedCenters = false;
        IsShrinking = false;
        _wasShrinking = false;
        ZoneStatusMessage = "SAFE";

        if (_barrierMesh != null)
        {
            _barrierMesh.Visible = false;
        }

        if (Multiplayer.IsServer() && Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncZone), CurrentRadius, TargetRadius, CurrentCenter, TargetCenter, false, ZoneStatusMessage, false);
        }
    }

    public void SyncStateToClient(long peerId)
    {
        if (!Multiplayer.IsServer()) return;

        if (_hasGeneratedCenters)
        {
            RpcId(peerId, nameof(RpcInitZoneCenters), _stage0Center, _stage1Center, _stage2Center, _stage3Center);
        }

        RpcId(peerId, nameof(RpcSyncZone), CurrentRadius, TargetRadius, CurrentCenter, TargetCenter, IsShrinking, ZoneStatusMessage, IsActive);
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
                    Rpc(nameof(RpcSyncZone), CurrentRadius, TargetRadius, CurrentCenter, TargetCenter, IsShrinking, ZoneStatusMessage, IsActive);
                }
            }
        }
        else
        {
            // Client side update
            if (_hasGeneratedCenters && GameManager.Instance != null && GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale)
            {
                UpdateClientStage();
            }
            else
            {
                float moveRate = IsShrinking ? 2.5f : 5.0f;
                CurrentRadius = Mathf.MoveToward(CurrentRadius, _syncedTargetRadius, (float)delta * moveRate);
                CurrentCenter = CurrentCenter.MoveToward(_syncedTargetCenter, (float)delta * moveRate * 1.5f);
            }
        }

        // Update barrier mesh position, scale, and pulse animation
        if (_barrierMesh != null && _barrierMesh.Visible)
        {
            _barrierMesh.Position = new Vector3(CurrentCenter.X, 7.0f, CurrentCenter.Z);
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
        ManagerAnnouncer.Instance?.AnnounceZoneShrink();

        if (GameManager.Instance == null) return;
        AudioStream warningSound = GameManager.Instance.TransitionChime;
        if (warningSound != null)
        {
            GameManager.Instance.PlaySoundOnSpeakers(warningSound, duckMusic: true);
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
        EvaluateStage(elapsed);
    }

    private void UpdateClientStage()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentPhase != GamePhase.BattleRoyale) return;

        float elapsed = GameManager.Instance.BattleDuration - GameManager.Instance.TimeRemaining;
        EvaluateStage(elapsed);
    }

    private void EvaluateStage(float elapsed)
    {
        string dept1 = GetDepartmentName(_stage1Center);
        string dept2 = GetDepartmentName(_stage2Center);
        string deptFinal = GetDepartmentName(_stage3Center);

        // Stage timings based on elapsed battle royale time:
        // 0-45s: Full store open (116m)
        // 45-90s: Continuous shrink to 58m (aisle perimeter)
        // 90-130s: Stable at 58m
        // 130-170s: Continuous shrink to 28m (department area)
        // 170-200s: Stable at 28m
        // 200-235s: Continuous final shrink to 12m (final showdown)
        // 235-240s: Final showdown at 12m
        if (elapsed < 45f)
        {
            CurrentRadius = InitialRadius;
            TargetRadius = InitialRadius;
            CurrentCenter = _stage0Center;
            TargetCenter = _stage1Center;
            IsShrinking = false;
            ZoneStatusMessage = $"SAFE (Lockdown in {Mathf.CeilToInt(45f - elapsed)}s)";
        }
        else if (elapsed < 90f)
        {
            float t = (elapsed - 45f) / 45f;
            TargetRadius = Stage1Radius;
            TargetCenter = _stage1Center;
            CurrentRadius = Mathf.Lerp(InitialRadius, Stage1Radius, t);
            CurrentCenter = _stage0Center.Lerp(_stage1Center, t);
            IsShrinking = true;
            ZoneStatusMessage = $"PERIMETER LOCKDOWN CLOSING TOWARDS {dept1.ToUpper()}! ({Mathf.CeilToInt(90f - elapsed)}s)";
        }
        else if (elapsed < 130f)
        {
            CurrentRadius = Stage1Radius;
            TargetRadius = Stage1Radius;
            CurrentCenter = _stage1Center;
            TargetCenter = _stage2Center;
            IsShrinking = false;
            ZoneStatusMessage = $"ZONE STABLE AT {dept1.ToUpper()} (Next shrink in {Mathf.CeilToInt(130f - elapsed)}s)";
        }
        else if (elapsed < 170f)
        {
            float t = (elapsed - 130f) / 40f;
            TargetRadius = Stage2Radius;
            TargetCenter = _stage2Center;
            CurrentRadius = Mathf.Lerp(Stage1Radius, Stage2Radius, t);
            CurrentCenter = _stage1Center.Lerp(_stage2Center, t);
            IsShrinking = true;
            ZoneStatusMessage = $"STORE CLOSING TOWARDS {dept2.ToUpper()}! ({Mathf.CeilToInt(170f - elapsed)}s)";
        }
        else if (elapsed < 200f)
        {
            CurrentRadius = Stage2Radius;
            TargetRadius = Stage2Radius;
            CurrentCenter = _stage2Center;
            TargetCenter = _stage3Center;
            IsShrinking = false;
            ZoneStatusMessage = $"ZONE STABLE AT {dept2.ToUpper()} (Final shrink in {Mathf.CeilToInt(200f - elapsed)}s)";
        }
        else if (elapsed < 235f)
        {
            float t = (elapsed - 200f) / 35f;
            TargetRadius = FinalRadius;
            TargetCenter = _stage3Center;
            CurrentRadius = Mathf.Lerp(Stage2Radius, FinalRadius, t);
            CurrentCenter = _stage2Center.Lerp(_stage3Center, t);
            IsShrinking = true;
            ZoneStatusMessage = $"FINAL SHOWDOWN CLOSING IN AT {deptFinal.ToUpper()}! ({Mathf.CeilToInt(235f - elapsed)}s)";
        }
        else
        {
            CurrentRadius = FinalRadius;
            TargetRadius = FinalRadius;
            CurrentCenter = _stage3Center;
            TargetCenter = _stage3Center;
            IsShrinking = false;
            ZoneStatusMessage = $"FINAL SHOWDOWN AT {deptFinal.ToUpper()}!";
        }
    }

    private void ApplyZoneDamage()
    {
        if (GameManager.Instance == null) return;

        foreach (var player in GameManager.Instance.GetLivingPlayers())
        {
            if (player == null || !GodotObject.IsInstanceValid(player)) continue;

            float dist2D = new Vector2(player.GlobalPosition.X - CurrentCenter.X, player.GlobalPosition.Z - CurrentCenter.Z).Length();
            if (dist2D > CurrentRadius)
            {
                player.TakeDamage(ZoneDamagePerSecond, null);
                GD.Print($"[ArenaZone] Player '{player.PlayerName}' outside safe zone ({dist2D:0.0}m > {CurrentRadius:0.0}m), dealt {ZoneDamagePerSecond} dmg");
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcInitZoneCenters(Vector3 stage0, Vector3 stage1, Vector3 stage2, Vector3 stage3)
    {
        _stage0Center = stage0;
        _stage1Center = stage1;
        _stage2Center = stage2;
        _stage3Center = stage3;
        _hasGeneratedCenters = true;
        GD.Print($"[ArenaZoneManager] Received Authoritative Zone Centers: Stage 1={stage1}, Stage 2={stage2}, Stage 3={stage3}");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcSyncZone(float currentRadius, float targetRadius, Vector3 currentCenter, Vector3 targetCenter, bool isShrinking, string statusMessage, bool isActive)
    {
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer()) return;

        IsActive = isActive;
        _syncedTargetRadius = targetRadius;
        TargetRadius = targetRadius;
        _syncedTargetCenter = targetCenter;
        TargetCenter = targetCenter;
        IsShrinking = isShrinking;
        ZoneStatusMessage = statusMessage;

        // If client is noticeably behind or ahead, synchronize directly
        if (Mathf.Abs(CurrentRadius - currentRadius) > 3.0f || !IsShrinking)
        {
            CurrentRadius = currentRadius;
        }

        if (CurrentCenter.DistanceTo(currentCenter) > 4.0f || !IsShrinking)
        {
            CurrentCenter = currentCenter;
        }

        if (_barrierMesh != null)
        {
            _barrierMesh.Visible = IsActive;
            _barrierMesh.Position = new Vector3(CurrentCenter.X, 7.0f, CurrentCenter.Z);
            _barrierMesh.Scale = new Vector3(CurrentRadius, 1.0f, CurrentRadius);
        }
    }

    public bool IsPlayerOutside(Vector3 playerPos)
    {
        if (!IsActive) return false;
        float dist2D = new Vector2(playerPos.X - CurrentCenter.X, playerPos.Z - CurrentCenter.Z).Length();
        return dist2D > CurrentRadius;
    }

    public float GetDistanceToSafeZone(Vector3 playerPos)
    {
        float dist2D = new Vector2(playerPos.X - CurrentCenter.X, playerPos.Z - CurrentCenter.Z).Length();
        return Mathf.Max(0f, dist2D - CurrentRadius);
    }
}
