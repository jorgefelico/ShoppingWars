using Godot;

public class PowerSurgeAmbientEvent : AmbientEventBase
{
    public override string EventId => "PowerSurge";
    public override string DisplayName => "POWER SURGE!";
    public override string Description => "Voltage instability! Lights flickering erratically across aisles.";
    public override Color BannerColor => new Color(1.0f, 0.85f, 0.2f);
    public override float DefaultDuration => 12.0f;
    public override float Weight { get; set; } = 1.0f;

    private float _flickerTimer = 0f;
    private float _nextFlickerInterval = 0.15f;
    private bool _flickerState = true;

    public override void OnStart(AmbientEventManager manager, float duration)
    {
        _flickerTimer = 0f;
        _nextFlickerInterval = 0.15f;
        _flickerState = true;
        GD.Print("[AmbientEvent] Power Surge started!");
    }

    public override void OnProcess(AmbientEventManager manager, float delta)
    {
        _flickerTimer += delta;
        if (_flickerTimer >= _nextFlickerInterval)
        {
            _flickerTimer = 0f;
            _nextFlickerInterval = (float)GD.RandRange(0.08f, 0.30f);
            _flickerState = !_flickerState;

            manager.GetTree().CallGroup("StoreLights", "SetPower", _flickerState);
            manager.SetTonemapExposure(_flickerState ? (float)GD.RandRange(1.3f, 1.8f) : 0.8f);
        }
    }

    public override void OnEnd(AmbientEventManager manager)
    {
        manager.GetTree().CallGroup("StoreLights", "SetPower", true);
        manager.SetLightmapVisible(true);
        manager.SetTonemapExposure(1.5f);
        GD.Print("[AmbientEvent] Power Surge ended. Grid stabilized!");
    }
}
