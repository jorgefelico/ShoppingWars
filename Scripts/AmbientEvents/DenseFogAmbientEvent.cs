using Godot;

public class DenseFogAmbientEvent : AmbientEventBase
{
    public override string EventId => "DenseFog";
    public override string DisplayName => "FREEZER LEAK!";
    public override string Description => "Coolant leak from frozen food! Thick icy mist obscuring store aisles.";
    public override Color BannerColor => new Color(0.55f, 0.75f, 0.95f);
    public override float DefaultDuration => 16.0f;
    public override float Weight { get; set; } = 1.0f;

    public override void OnStart(AmbientEventManager manager, float duration)
    {
        manager.SetVolumetricFog(true, 0.06f, new Color(0.7f, 0.85f, 1.0f));
        GD.Print("[AmbientEvent] Freezer Leak / Dense Fog started!");
    }

    public override void OnEnd(AmbientEventManager manager)
    {
        manager.SetVolumetricFog(false, 0.0f, Colors.White);
        GD.Print("[AmbientEvent] Freezer Leak ended. Air cleared!");
    }
}
