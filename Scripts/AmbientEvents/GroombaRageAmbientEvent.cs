using Godot;

public class GroombaRageAmbientEvent : AmbientEventBase
{
    public override string EventId => "GroombaRage";
    public override string DisplayName => "GROOMBA RAGE!";
    public override string Description => "Autonomous cleaning units overcharged! Groombas moving in turbo overdrive.";
    public override Color BannerColor => new Color(1.0f, 0.45f, 0.1f);
    public override float DefaultDuration => 15.0f;
    public override float Weight { get; set; } = 1.0f;

    public override void OnStart(AmbientEventManager manager, float duration)
    {
        manager.GetTree().CallGroup("PatrolEnemies", "SetSpeedMultiplier", 1.6f);
        GD.Print("[AmbientEvent] Groomba Rage started!");
    }

    public override void OnEnd(AmbientEventManager manager)
    {
        manager.GetTree().CallGroup("PatrolEnemies", "SetSpeedMultiplier", 1.0f);
        GD.Print("[AmbientEvent] Groomba Rage ended. Units returned to standard patrol speed.");
    }
}
