using Godot;
using System;

public partial class EmergencyBeacon : Node3D
{
    [Export] public float BaseEnergy = 3.0f;
    [Export] public float PulseSpeed = 2.5f;
    [Export] public Color BeaconColor = new Color(1.0f, 0.08f, 0.05f);

    private OmniLight3D _light;
    private MeshInstance3D _domeMesh;
    private StandardMaterial3D _domeMaterial;
    private bool _isActive = false;
    private float _time = 0.0f;

    public override void _Ready()
    {
        AddToGroup("EmergencyBeacons");

        _light = GetNodeOrNull<OmniLight3D>("OmniLight3D");
        _domeMesh = GetNodeOrNull<MeshInstance3D>("Dome");

        if (_domeMesh != null)
        {
            var mat = _domeMesh.GetActiveMaterial(0);
            if (mat is StandardMaterial3D stdMat)
            {
                _domeMaterial = (StandardMaterial3D)stdMat.Duplicate();
                _domeMesh.SetSurfaceOverrideMaterial(0, _domeMaterial);
            }
        }

        SetBeaconState(false);
    }

    public void SetBeaconState(bool active)
    {
        _isActive = active;

        if (_light != null)
        {
            _light.Visible = active;
        }

        if (_domeMaterial != null)
        {
            _domeMaterial.EmissionEnabled = active;
            _domeMaterial.Emission = BeaconColor;
            _domeMaterial.EmissionEnergyMultiplier = active ? BaseEnergy : 0.0f;
        }
    }

    public override void _Process(double delta)
    {
        if (!_isActive) return;

        _time += (float)delta * PulseSpeed;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time); // 0.0 to 1.0 smooth wave
        float energy = Mathf.Lerp(0.8f, BaseEnergy, pulse);

        if (_light != null)
        {
            _light.LightEnergy = energy;
        }

        if (_domeMaterial != null)
        {
            _domeMaterial.EmissionEnergyMultiplier = energy * 1.5f;
        }
    }
}
