using Godot;
using System;

public partial class FlickeringLight : Node3D
{
    [Export] public float BaseLightEnergy = 3.5f;
    [Export] public float BaseEmissionEnergy = 3.0f;

    [Export] public float MinFlickerInterval = 2.0f;
    [Export] public float MaxFlickerInterval = 6.0f;

    private OmniLight3D _light;
    private MeshInstance3D _tubeMesh;
    private StandardMaterial3D _tubeMaterial;

    private float _timer = 0.0f;
    private float _nextFlickerTime = 3.0f;
    private bool _isFlickering = false;
    private bool _isPowered = true;

    public override void _Ready()
    {
        AddToGroup("StoreLights");

        _light = GetNodeOrNull<OmniLight3D>("OmniLight3D");
        _tubeMesh = GetNodeOrNull<MeshInstance3D>("Tube");

        // Duplicate the tube material so only this light instance changes emission
        if (_tubeMesh != null)
        {
            var mat = _tubeMesh.GetActiveMaterial(0);
            if (mat is StandardMaterial3D stdMat)
            {
                _tubeMaterial = (StandardMaterial3D)stdMat.Duplicate();
                _tubeMesh.SetSurfaceOverrideMaterial(0, _tubeMaterial);
            }
        }

        ResetNextFlickerTime();
    }

    public void SetPower(bool isPowered)
    {
        _isPowered = isPowered;
        _isFlickering = false;
        
        if (!isPowered)
        {
            SetBrightness(0.0f);
            if (_light != null) _light.Visible = false;
        }
        else
        {
            if (_light != null) _light.Visible = true;
            SetBrightness(1.0f);
            ResetNextFlickerTime();
        }
    }

    public override void _Process(double delta)
    {
        if (!_isPowered || _isFlickering) return;

        _timer += (float)delta;
        if (_timer >= _nextFlickerTime)
        {
            _timer = 0.0f;
            TriggerFlickerBurst();
        }
    }

    private async void TriggerFlickerBurst()
    {
        _isFlickering = true;

        // Rapid micro-stutter (2 to 5 quick blinks)
        int blinks = (int)GD.RandRange(2, 5);
        for (int i = 0; i < blinks; i++)
        {
            SetBrightness(GD.Randf() > 0.5f ? 0.05f : 0.3f); // Dip/kill light
            await ToSignal(GetTree().CreateTimer(GD.RandRange(0.03f, 0.09f)), SceneTreeTimer.SignalName.Timeout);

            SetBrightness((float)GD.RandRange(0.8f, 1.2f)); // Flare
            await ToSignal(GetTree().CreateTimer(GD.RandRange(0.04f, 0.12f)), SceneTreeTimer.SignalName.Timeout);
        }

        // Return to normal baseline
        SetBrightness(1.0f);
        ResetNextFlickerTime();
        _isFlickering = false;
    }

    private void SetBrightness(float multiplier)
    {
        if (_light != null)
        {
            _light.LightEnergy = BaseLightEnergy * multiplier;
        }

        if (_tubeMaterial != null)
        {
            _tubeMaterial.EmissionEnergyMultiplier = BaseEmissionEnergy * multiplier;
        }
    }

    private void ResetNextFlickerTime()
    {
        _nextFlickerTime = (float)GD.RandRange(MinFlickerInterval, MaxFlickerInterval);
    }

}