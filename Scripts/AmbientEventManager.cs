using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class AmbientEventManager : Node
{
    public static AmbientEventManager Instance { get; private set; }

    [Export] public bool EnableRandomEvents = true;
    [Export] public float MinEventInterval = 15.0f;
    [Export] public float MaxEventInterval = 25.0f;
    [Export] public float MinFirstEventDelay = 8.0f;
    [Export] public float MaxFirstEventDelay = 15.0f;
    [Export] public bool TriggerInShoppingPhase = false;
    [Export] public bool TriggerInBattleRoyalePhase = true;

    [Export] private LightmapGI _lightmap;
    [Export] private WorldEnvironment _worldEnvironment;
    [Export] public AudioStream BlackoutSound;

    public IAmbientEvent ActiveEvent { get; private set; }
    public string ActiveEventId { get; private set; } = "";
    public float ActiveEventTimeRemaining { get; private set; } = 0f;
    public float NextEventTimer { get; private set; } = 15.0f;

    private readonly Dictionary<string, IAmbientEvent> _events = new();
    private string _lastEventId = "";
    private AudioStreamPlayer _audioPlayer;
    private bool _subscribedToGameManager = false;

    [Signal]
    public delegate void AmbientEventStartedEventHandler(string eventId, string displayName, string description, float duration);

    [Signal]
    public delegate void AmbientEventEndedEventHandler(string eventId);

    public override void _Ready()
    {
        Instance = this;

        _audioPlayer = new AudioStreamPlayer();
        _audioPlayer.Bus = "SFX";
        AddChild(_audioPlayer);

        if (BlackoutSound == null && ResourceLoader.Exists("res://Sounds/lights_out.wav"))
        {
            BlackoutSound = GD.Load<AudioStream>("res://Sounds/lights_out.wav");
        }

        FindSceneReferences();

        // Register all standard ambient events
        RegisterEvent(new LightsOutAmbientEvent());
        RegisterEvent(new PowerSurgeAmbientEvent());
        RegisterEvent(new LowGravityAmbientEvent());
        RegisterEvent(new SpeedFrenzyAmbientEvent());
        RegisterEvent(new DenseFogAmbientEvent());
        RegisterEvent(new GroombaRageAmbientEvent());
        RegisterEvent(new SprinklerAmbientEvent());
        RegisterEvent(new ClearanceSaleAmbientEvent());

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged += OnGamePhaseChanged;
            _subscribedToGameManager = true;
        }

        ScheduleFirstEventDelay();
    }

    public override void _ExitTree()
    {
        if (_subscribedToGameManager && GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged -= OnGamePhaseChanged;
            _subscribedToGameManager = false;
        }
    }

    public void ScheduleFirstEventDelay()
    {
        float min = Mathf.Min(MinFirstEventDelay, MaxFirstEventDelay);
        float max = Mathf.Max(MinFirstEventDelay, MaxFirstEventDelay);
        NextEventTimer = (float)GD.RandRange(min, max);
    }

    private void OnGamePhaseChanged()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale)
        {
            if (Multiplayer.IsServer())
            {
                ScheduleFirstEventDelay();
                GD.Print($"[AmbientEventManager] Battle Royale phase started! First ambient event scheduled in {NextEventTimer:0.0}s");
            }
        }
        else
        {
            if (ActiveEvent != null)
            {
                EndActiveEvent();
            }
        }
    }

    private void FindSceneReferences()
    {
        if (_lightmap == null)
        {
            _lightmap = GetTree().CurrentScene?.GetNodeOrNull<LightmapGI>("LightmapGI")
                     ?? GetTree().CurrentScene?.FindChild("LightmapGI", true, false) as LightmapGI;
        }

        if (_worldEnvironment == null)
        {
            _worldEnvironment = GetTree().CurrentScene?.GetNodeOrNull<WorldEnvironment>("WorldStuff/WorldEnvironment")
                             ?? GetTree().CurrentScene?.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
        }
    }

    public void RegisterEvent(IAmbientEvent ambientEvent)
    {
        if (ambientEvent == null) return;
        _events[ambientEvent.EventId] = ambientEvent;
        GD.Print($"[AmbientEventManager] Registered ambient event: {ambientEvent.DisplayName} ({ambientEvent.EventId})");
    }

    public IAmbientEvent GetEvent(string eventId)
    {
        return _events.TryGetValue(eventId, out var ev) ? ev : null;
    }

    public override void _Process(double delta)
    {
        if (!_subscribedToGameManager && GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged += OnGamePhaseChanged;
            _subscribedToGameManager = true;
        }

        // 1. Process active ambient event
        if (ActiveEvent != null)
        {
            ActiveEventTimeRemaining = Mathf.Max(0f, ActiveEventTimeRemaining - (float)delta);
            ActiveEvent.OnProcess(this, (float)delta);

            if (Multiplayer.IsServer() && ActiveEventTimeRemaining <= 0f)
            {
                EndActiveEvent();
            }
            return;
        }

        // 2. Server-authoritative random event scheduler
        if (!Multiplayer.IsServer() || !EnableRandomEvents) return;

        // Only trigger during active game phases
        if (GameManager.Instance == null) return;
        GamePhase phase = GameManager.Instance.CurrentPhase;
        if (phase == GamePhase.Lobby) return;
        if (phase == GamePhase.Shopping && !TriggerInShoppingPhase) return;
        if (phase == GamePhase.BattleRoyale && !TriggerInBattleRoyalePhase) return;

        NextEventTimer -= (float)delta;
        if (NextEventTimer <= 0f)
        {
            TriggerRandomEvent();
        }
    }

    public void TriggerRandomEvent()
    {
        if (!Multiplayer.IsServer()) return;
        if (_events.Count == 0) return;

        // Select candidate events (prefer different from last event if possible)
        var candidates = _events.Values.Where(e => _events.Count == 1 || e.EventId != _lastEventId).ToList();
        if (candidates.Count == 0) candidates = _events.Values.ToList();

        float totalWeight = candidates.Sum(e => e.Weight);
        float roll = (float)GD.RandRange(0f, totalWeight);
        float cumulative = 0f;

        IAmbientEvent selected = candidates[0];
        foreach (var ev in candidates)
        {
            cumulative += ev.Weight;
            if (roll <= cumulative)
            {
                selected = ev;
                break;
            }
        }

        StartEvent(selected.EventId, selected.DefaultDuration);
    }

    public void StartEvent(string eventId, float duration = -1f)
    {
        if (!_events.TryGetValue(eventId, out var ev))
        {
            GD.PrintErr($"[AmbientEventManager] Unknown event ID: {eventId}");
            return;
        }

        float actualDuration = duration > 0 ? duration : ev.DefaultDuration;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcStartAmbientEvent), eventId, actualDuration);
        }
        else
        {
            RpcStartAmbientEvent(eventId, actualDuration);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcStartAmbientEvent(string eventId, float duration)
    {
        // If an event is currently active, end it first cleanly
        if (ActiveEvent != null)
        {
            ActiveEvent.OnEnd(this);
            EmitSignal(SignalName.AmbientEventEnded, ActiveEventId);
        }

        if (!_events.TryGetValue(eventId, out var ev))
        {
            GD.PrintErr($"[AmbientEventManager] Cannot start unknown event: {eventId}");
            return;
        }

        ActiveEvent = ev;
        ActiveEventId = eventId;
        _lastEventId = eventId;
        ActiveEventTimeRemaining = duration;

        ActiveEvent.OnStart(this, duration);
        EmitSignal(SignalName.AmbientEventStarted, ev.EventId, ev.DisplayName, ev.Description, duration);
        GD.Print($"[AmbientEventManager] Event START: {ev.DisplayName} for {duration:0.0}s");
    }

    public void EndActiveEvent()
    {
        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcEndAmbientEvent));
        }
        else
        {
            RpcEndAmbientEvent();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcEndAmbientEvent()
    {
        if (ActiveEvent != null)
        {
            string endedId = ActiveEventId;
            ActiveEvent.OnEnd(this);
            ActiveEvent = null;
            ActiveEventId = "";
            ActiveEventTimeRemaining = 0f;

            EmitSignal(SignalName.AmbientEventEnded, endedId);
            GD.Print($"[AmbientEventManager] Event END: {endedId}");
        }

        if (Multiplayer.IsServer())
        {
            NextEventTimer = (float)GD.RandRange(MinEventInterval, MaxEventInterval);
        }
    }

    public void ResetEvents()
    {
        if (ActiveEvent != null)
        {
            ActiveEvent.OnEnd(this);
            ActiveEvent = null;
            ActiveEventId = "";
            ActiveEventTimeRemaining = 0f;
        }

        // Restore lighting and world environment to pristine baseline
        GetTree().CallGroup("StoreLights", "SetPower", true);
        GetTree().CallGroup("EmergencyBeacons", "SetBeaconState", false);
        SetLightmapVisible(true);
        SetTonemapExposure(1.5f);
        SetVolumetricFog(false, 0f, Colors.White);
        PlayerController.SetGlobalSpeedModifier(1.0f);
        PlayerController.SetGlobalGravityModifier(1.0f, 1.0f);
        PlayerController.SetGlobalFrictionModifier(1.0f);
        PlayerController.GlobalDamageMultiplier = 1.0f;
        GetTree().CallGroup("PatrolEnemies", "SetSpeedMultiplier", 1.0f);

        ScheduleFirstEventDelay();
    }

    public void SetLightmapVisible(bool visible)
    {
        FindSceneReferences();
        if (_lightmap != null)
        {
            _lightmap.Visible = visible;
        }
    }

    public void SetTonemapExposure(float exposure)
    {
        FindSceneReferences();
        if (_worldEnvironment?.Environment != null)
        {
            _worldEnvironment.Environment.TonemapExposure = exposure;
        }
    }

    public void SetVolumetricFog(bool enabled, float density, Color albedo)
    {
        FindSceneReferences();
        if (_worldEnvironment?.Environment != null)
        {
            _worldEnvironment.Environment.VolumetricFogEnabled = enabled;
            if (enabled)
            {
                _worldEnvironment.Environment.VolumetricFogDensity = density;
                _worldEnvironment.Environment.VolumetricFogAlbedo = albedo;
            }
        }
    }

    public void PlayBlackoutSound()
    {
        if (BlackoutSound != null && _audioPlayer != null)
        {
            _audioPlayer.Stream = BlackoutSound;
            _audioPlayer.Play();
        }
    }

    public void SyncStateToClient(long peerId)
    {
        if (!Multiplayer.IsServer()) return;

        if (ActiveEvent != null && ActiveEventTimeRemaining > 0f)
        {
            RpcId(peerId, nameof(RpcStartAmbientEvent), ActiveEventId, ActiveEventTimeRemaining);
        }
    }
}
