using Godot;

public class SpeedFrenzyAmbientEvent : AmbientEventBase
{
    public override string EventId => "SpeedFrenzy";
    public override string DisplayName => "SUGAR RUSH!";
    public override string Description => "Energy drink spill in aisle 4! Sprint and walk speed increased by 50%.";
    public override Color BannerColor => new Color(0.2f, 1.0f, 0.4f);
    public override float DefaultDuration => 14.0f;
    public override float Weight { get; set; } = 1.0f;

    public override void OnStart(AmbientEventManager manager, float duration)
    {
        PlayerController.SetGlobalSpeedModifier(1.5f);
        GD.Print("[AmbientEvent] Speed Frenzy / Sugar Rush started!");
    }

    public override void OnEnd(AmbientEventManager manager)
    {
        PlayerController.SetGlobalSpeedModifier(1.0f);
        GD.Print("[AmbientEvent] Speed Frenzy ended. Movement speeds back to normal!");
    }
}
