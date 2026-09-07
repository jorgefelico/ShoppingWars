using System;
using System.Collections.Generic;
using Godot;

public partial class LaserCamera : Area3D
{
    private List<PlayerController> _trackedPlayers = new();
    private PlayerController _currentTarget = null;
    private Transform3D _initialTransform;

    [Export] private Node3D _lens;
    [Export] public float MaxRange = 22.5f;
    [Export] public float ConeHalfAngle = 11f;
    [Export] public int DamageAmount = 2;
    [Export] public float TargetHeightOffset = 0.35f; // Height offset on player (0.35 = chest/torso, 0.7 = head)
    [Export] private MeshInstance3D _laser;
    [Export] public float TrackingSpeed = 2f; // 0 = instant snap, > 0 = smooth rotation
    [Export] public bool ReturnToRest = true;
    [Export] public float RestReturnSpeed = 2.0f;

    [ExportGroup("Scanning")]
    [Export] public bool EnableScanning = true;
    [Export] public float ScanYawRange = 40.0f;     // Max degrees left/right from initial forward
    [Export] public float ScanPitchRange = 10.0f;   // Max degrees up/down
    [Export] public float ScanSpeed = 1.2f;         // How fast it pans between points
    [Export] public float ScanPauseMin = 1.5f;      // Min pause time at each vantage point (seconds)
    [Export] public float ScanPauseMax = 3.5f;      // Max pause time at each vantage point (seconds)

    private Basis _targetScanBasis;
    private float _scanPauseTimer = 0f;
    private bool _isScanningWaiting = false;

    [ExportGroup("Multiplayer & Damage")]
    [Export] public float DamageInterval = 0.5f; // Seconds between damage ticks
    private float _damageTimer = 0f;
    private Quaternion _syncRotation = Quaternion.Identity;
    private bool _syncLaserActive = false;
    private float _syncBeamLength = 0f;
    private float _currentBeamLength = 0f;

    [ExportGroup("Phase Gating")]
    [Export] public bool OnlyActiveInBattlePhase = true;
    [Export] private SpotLight3D _spotLight;
    [Export] private MeshInstance3D _visionCone;

    public bool IsActivePhase()
    {
        if (!OnlyActiveInBattlePhase || GameManager.Instance == null) return true;
        return GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale;
    }

    private void UpdatePhaseVisibility(bool active)
    {
        if (_spotLight != null) _spotLight.Visible = active;
        if (_visionCone != null) _visionCone.Visible = active;
        if (!active && _laser != null) _laser.Visible = false;
    }

    private bool IsServer() => !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        _spotLight ??= GetNodeOrNull<SpotLight3D>("SpotLight3D");
        _visionCone ??= GetNodeOrNull<MeshInstance3D>("VisionCone");

        _initialTransform = GlobalTransform;
        _targetScanBasis = _initialTransform.Basis;
        _syncRotation = _initialTransform.Basis.GetRotationQuaternion();

        UpdatePhaseVisibility(IsActivePhase());

        if (IsServer() && EnableScanning)
        {
            PickNewScanTarget();
        }

        if (_laser?.Mesh != null)
        {
            _laser.Mesh = (Mesh)_laser.Mesh.Duplicate();
        }

        if (_laser != null)
        {
            _laser.Visible = false;
        }
    }

    public override void _Process(double delta)
    {
        bool active = IsActivePhase();
        UpdatePhaseVisibility(active);

        if (IsServer()) return;

        // Smooth rotation interpolation on remote clients
        float rotLerpWeight = (float)Mathf.Clamp(delta * 20.0, 0.0, 1.0);
        Basis currentBasis = GlobalBasis;
        Basis targetBasis = new Basis(_syncRotation);
        GlobalBasis = currentBasis.Slerp(targetBasis, rotLerpWeight).Orthonormalized();

        // Update laser visuals on remote clients
        if (_laser != null)
        {
            _laser.Visible = active && _syncLaserActive;
            if (active && _syncLaserActive && _syncBeamLength > 0f)
            {
                if (_laser.Mesh is CylinderMesh cyl)
                {
                    cyl.Height = _syncBeamLength;
                }
                _laser.Position = new Vector3(0, -_syncBeamLength * 0.5f, 0);
                _laser.Rotation = Vector3.Zero;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsServer()) return;

        bool active = IsActivePhase();
        UpdatePhaseVisibility(active);

        if (!active)
        {
            _currentTarget = null;
            _currentBeamLength = 0f;
            if (ReturnToRest)
            {
                GlobalBasis = GlobalBasis.Slerp(_initialTransform.Basis, (float)(delta * RestReturnSpeed));
            }
            SyncToClients();
            return;
        }

        // Clean up dead or invalid players
        _trackedPlayers.RemoveAll(p => !GodotObject.IsInstanceValid(p) || p.Health == null || p.Health.IsDead);

        // If currently locked onto a target, verify they are still valid
        if (_currentTarget != null)
        {
            Vector3 targetPos = GetPlayerTargetPosition(_currentTarget);
            float dist = (targetPos - _lens.GlobalPosition).Length();

            if (!_trackedPlayers.Contains(_currentTarget) || 
                _currentTarget.Health == null || 
                _currentTarget.Health.IsDead || 
                dist > MaxRange || 
                !HasLineOfSight(_currentTarget, out targetPos))
            {
                _currentTarget = null;
                _currentBeamLength = 0f;
                if (EnableScanning)
                {
                    PickNewScanTarget();
                }
            }
            else
            {
                TrackPlayer(_currentTarget, targetPos, delta);
                SyncToClients();
                return;
            }
        }

        // Search for a new target inside the detection cone
        PlayerController bestTarget = null;
        Vector3 bestTargetPos = Vector3.Zero;
        float closestDist = float.MaxValue;
        Vector3 cameraForward = -GlobalTransform.Basis.Z;

        foreach (PlayerController player in _trackedPlayers)
        {
            Vector3 targetPos = GetPlayerTargetPosition(player);
            Vector3 toPlayer = targetPos - _lens.GlobalPosition;
            float dist = toPlayer.Length();

            if (dist <= MaxRange && dist < closestDist)
            {
                Vector3 dir = toPlayer.Normalized();
                float angleDegrees = Mathf.RadToDeg(cameraForward.AngleTo(dir));
                if (angleDegrees <= ConeHalfAngle)
                {
                    if (HasLineOfSight(player, out _))
                    {
                        closestDist = dist;
                        bestTarget = player;
                        bestTargetPos = targetPos;
                    }
                }
            }
        }

        if (bestTarget != null)
        {
            _currentTarget = bestTarget;
            TrackPlayer(_currentTarget, bestTargetPos, delta);
        }
        else
        {
            _currentBeamLength = 0f;
            if (_laser != null)
            {
                _laser.Visible = false;
            }

            if (EnableScanning)
            {
                UpdateScan(delta);
            }
            else if (ReturnToRest)
            {
                GlobalBasis = GlobalBasis.Slerp(_initialTransform.Basis, (float)(delta * RestReturnSpeed));
            }
        }

        SyncToClients();
    }

    private void UpdateScan(double delta)
    {
        if (_isScanningWaiting)
        {
            _scanPauseTimer -= (float)delta;
            if (_scanPauseTimer <= 0f)
            {
                PickNewScanTarget();
            }
        }
        else
        {
            GlobalBasis = GlobalBasis.Slerp(_targetScanBasis, (float)(delta * ScanSpeed)).Orthonormalized();

            Vector3 currentForward = -GlobalTransform.Basis.Z;
            Vector3 targetForward = -_targetScanBasis.Z;
            float angleDiff = Mathf.RadToDeg(currentForward.AngleTo(targetForward));

            if (angleDiff < 2.0f)
            {
                _isScanningWaiting = true;
                _scanPauseTimer = (float)GD.RandRange(ScanPauseMin, ScanPauseMax);
            }
        }
    }

    private void PickNewScanTarget()
    {
        float randomYaw = (float)GD.RandRange(-ScanYawRange, ScanYawRange);
        float randomPitch = (float)GD.RandRange(-ScanPitchRange, ScanPitchRange);

        // Pan horizontally around world Up to stay level
        Basis panned = _initialTransform.Basis.Rotated(Vector3.Up, Mathf.DegToRad(randomYaw));
        // Tilt vertically around camera's local X axis
        _targetScanBasis = panned.Rotated(panned.X.Normalized(), Mathf.DegToRad(randomPitch)).Orthonormalized();

        _scanPauseTimer = (float)GD.RandRange(ScanPauseMin, ScanPauseMax);
        _isScanningWaiting = false;
    }

    private Vector3 GetPlayerTargetPosition(PlayerController player)
    {
        return player.GlobalPosition + Vector3.Up * TargetHeightOffset;
    }

    private bool HasLineOfSight(PlayerController player, out Vector3 targetPos)
    {
        targetPos = GetPlayerTargetPosition(player);
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(_lens.GlobalPosition, targetPos);
        query.CollisionMask = 1 | 2;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = result["collider"].As<Node>();
            return collider == player;
        }

        return false;
    }

    private void TrackPlayer(PlayerController player, Vector3 targetPos, double delta)
    {
        // 1. Rotate the camera to face the player's chest
        Vector3 toTarget = (targetPos - GlobalPosition).Normalized();
        Vector3 up = Mathf.Abs(toTarget.Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;

        if (TrackingSpeed > 0f)
        {
            Transform3D targetTransform = GlobalTransform.LookingAt(targetPos, up);
            GlobalBasis = GlobalBasis.Slerp(targetTransform.Basis, (float)(delta * TrackingSpeed));
        }
        else
        {
            LookAt(targetPos, up);
        }

        // 2. Size and position the laser beam
        Vector3 startPos = _lens.GlobalPosition;
        float beamLength = (targetPos - startPos).Length();

        if (_laser != null)
        {
            if (_laser.Mesh is CylinderMesh cyl)
            {
                cyl.Height = beamLength;
            }

            // In CameraLens, local -Y points forward. Center the cylinder at -beamLength/2 so its top cap is at Y=0 (the lens)
            _laser.Position = new Vector3(0, -beamLength * 0.5f, 0);
            _laser.Rotation = Vector3.Zero;
            _laser.Visible = true;
        }

        _currentBeamLength = beamLength;

        // Apply damage server-side on an interval
        if (DamageAmount > 0)
        {
            _damageTimer -= (float)delta;
            if (_damageTimer <= 0f)
            {
                player.TakeDamage(DamageAmount, this);
                _damageTimer = DamageInterval;
            }
        }
    }

    private void SyncToClients()
    {
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            bool laserActive = _laser != null && _laser.Visible;
            Rpc(nameof(RpcSyncCameraState), GlobalTransform.Basis.GetRotationQuaternion(), laserActive, _currentBeamLength);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void RpcSyncCameraState(Quaternion rotation, bool laserActive, float beamLength)
    {
        _syncRotation = rotation;
        _syncLaserActive = laserActive;
        _syncBeamLength = beamLength;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!IsServer()) return;
        if (body is PlayerController player && player.Health != null && !player.Health.IsDead)
        {
            if (!_trackedPlayers.Contains(player))
            {
                _trackedPlayers.Add(player);
            }
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (!IsServer()) return;
        if (body is PlayerController player)
        {
            _trackedPlayers.Remove(player);
            if (_currentTarget == player)
            {
                _currentTarget = null;
                _currentBeamLength = 0f;
                if (EnableScanning)
                {
                    PickNewScanTarget();
                }
            }
        }
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
    }
}