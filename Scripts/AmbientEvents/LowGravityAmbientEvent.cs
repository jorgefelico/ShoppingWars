using Godot;

public class LowGravityAmbientEvent : AmbientEventBase
{
    public override string EventId => "LowGravity";
    public override string DisplayName => "LOW GRAVITY!";
    public override string Description => "Anti-grav anomaly! High jumps and floaty physics enabled.";
    public override Color BannerColor => new Color(0.3f, 0.85f, 1.0f);
    public override float DefaultDuration => 15.0f;
    public override float Weight { get; set; } = 1.0f;

    public override void OnStart(AmbientEventManager manager, float duration)
    {
        PlayerController.SetGlobalGravityModifier(0.35f, 1.35f);
        GD.Print("[AmbientEvent] Low Gravity started!");
    }

    public override void OnEnd(AmbientEventManager manager)
    {
        PlayerController.SetGlobalGravityModifier(1.0f, 1.0f);
        GD.Print("[AmbientEvent] Low Gravity ended. Standard gravity restored!");
    }
}
