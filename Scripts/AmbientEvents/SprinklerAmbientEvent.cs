using Godot;

public class SprinklerAmbientEvent : AmbientEventBase
{
    public override string EventId => "SprinklerMalfunction";
    public override string DisplayName => "SPRINKLER MALFUNCTION!";
    public override string Description => "Fire sprinklers triggered! Floors are soaking wet and slippery!";
    public override Color BannerColor => new Color(0.3f, 0.7f, 1.0f);
    public override float DefaultDuration => 16.0f;
    public override float Weight { get; set; } = 1.0f;

    public override void OnStart(AmbientEventManager manager, float duration)
    {
        // Reduce floor friction to 25% for slippery sliding physics!
        PlayerController.SetGlobalFrictionModifier(0.25f);
        manager.SetVolumetricFog(true, 0.04f, new Color(0.8f, 0.9f, 1.0f));
        GD.Print("[AmbientEvent] Sprinkler Malfunction started - slippery floors!");
    }

    public override void OnEnd(AmbientEventManager manager)
    {
        PlayerController.SetGlobalFrictionModifier(1.0f);
        manager.SetVolumetricFog(false, 0f, Colors.White);
        GD.Print("[AmbientEvent] Sprinklers shut off. Floors dried up!");
    }
}
