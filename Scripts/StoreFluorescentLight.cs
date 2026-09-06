using Godot;
using System;

public partial class StoreFluorescentLight : Node3D
{
    [Export] public float OnLightEnergy = 3.5f;
    [Export] public float OnEmissionEnergy = 3.0f;

    private OmniLight3D _light;
    private MeshInstance3D _tubeMesh;
    private StandardMaterial3D _tubeMaterial;
    private bool _isPowered = true;

    public override void _Ready()
    {
        AddToGroup("StoreLights");

        _light = GetNodeOrNull<OmniLight3D>("OmniLight3D");
        _tubeMesh = GetNodeOrNull<MeshInstance3D>("Tube");

        if (_tubeMesh != null)
        {
            var mat = _tubeMesh.GetActiveMaterial(0);
            if (mat is StandardMaterial3D stdMat)
            {
                _tubeMaterial = (StandardMaterial3D)stdMat.Duplicate();
                _tubeMesh.SetSurfaceOverrideMaterial(0, _tubeMaterial);
            }
        }

        // Apply initial power state
        SetPower(_isPowered);
    }

    public void SetPower(bool isPowered)
    {
        _isPowered = isPowered;

        if (_light != null)
        {
            _light.Visible = isPowered;
            _light.LightEnergy = isPowered ? OnLightEnergy : 0.0f;
        }

        if (_tubeMaterial != null)
        {
            _tubeMaterial.EmissionEnergyMultiplier = isPowered ? OnEmissionEnergy : 0.0f;
        }
    }
}
