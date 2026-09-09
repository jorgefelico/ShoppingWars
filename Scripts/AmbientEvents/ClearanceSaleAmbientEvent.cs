using Godot;

public class ClearanceSaleAmbientEvent : AmbientEventBase
{
    public override string EventId => "ClearanceSale";
    public override string DisplayName => "BLUE LIGHT SPECIAL!";
    public override string Description => "Super clearance madness! All attacks deal +50% extra damage!";
    public override Color BannerColor => new Color(0.2f, 0.5f, 1.0f);
    public override float DefaultDuration => 15.0f;
    public override float Weight { get; set; } = 1.0f;

    public override void OnStart(AmbientEventManager manager, float duration)
    {
        PlayerController.GlobalDamageMultiplier = 1.5f;
        GD.Print("[AmbientEvent] Blue Light Special started! Attacks deal 1.5x damage!");
    }

    public override void OnEnd(AmbientEventManager manager)
    {
        PlayerController.GlobalDamageMultiplier = 1.0f;
        GD.Print("[AmbientEvent] Blue Light Special ended. Damage returned to normal.");
    }
}
