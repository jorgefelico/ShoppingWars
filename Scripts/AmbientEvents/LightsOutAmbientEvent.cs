using Godot;

public class LightsOutAmbientEvent : AmbientEventBase
{
    public override string EventId => "LightsOut";
    public override string DisplayName => "BLACKOUT!";
    public override string Description => "Store electrical grid failed! Emergency power active.";
    public override Color BannerColor => new Color(1.0f, 0.25f, 0.25f);
    public override float DefaultDuration => 18.0f;
    public override float Weight { get; set; } = 1.0f;

    public override void OnStart(AmbientEventManager manager, float duration)
    {
        // Turn off all fluorescent store lights
        manager.GetTree().CallGroup("StoreLights", "SetPower", false);

        // Turn off baked LightmapGI for darkness
        manager.SetLightmapVisible(false);

        // Lower camera tonemap exposure
        manager.SetTonemapExposure(0.9f);

        // Activate red emergency beacon rotators/pulses
        manager.GetTree().CallGroup("EmergencyBeacons", "SetBeaconState", true);

        // Automatically activate flashlights for players during blackout
        PlayerController.SetGlobalFlashlight(true);

        // Play blackout electrical shutoff sound
        manager.PlayBlackoutSound();

        GD.Print("[AmbientEvent] Blackout / Lights Out started!");
    }

    public override void OnEnd(AmbientEventManager manager)
    {
        // Restore all store lights
        manager.GetTree().CallGroup("StoreLights", "SetPower", true);

        // Restore LightmapGI
        manager.SetLightmapVisible(true);

        // Restore tonemap exposure
        manager.SetTonemapExposure(1.5f);

        // Deactivate emergency beacons
        manager.GetTree().CallGroup("EmergencyBeacons", "SetBeaconState", false);

        // Turn off flashlights when power is restored
        PlayerController.SetGlobalFlashlight(false);

        GD.Print("[AmbientEvent] Blackout / Lights Out ended. Power restored!");
    }
}
