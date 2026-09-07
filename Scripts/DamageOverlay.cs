using Godot;

public partial class DamageOverlay : CanvasLayer
{
    [Export] public ColorRect FlashRect;
    [Export] public float FlashDecay = 2.4f;
    [Export] public Color FlashColor = new Color(0.85f, 0.04f, 0.04f, 1.0f);

    private float _intensity = 0.0f;
    private ShaderMaterial _material;

    public override void _Ready()
    {
        if (FlashRect == null)
        {
            FlashRect = GetNodeOrNull<ColorRect>("ColorRect");
        }

        if (FlashRect != null)
        {
            if (FlashRect.Material is ShaderMaterial mat)
            {
                _material = mat;
            }
            FlashRect.Visible = false;
        }
    }

    public override void _Process(double delta)
    {
        if (_intensity > 0.0f)
        {
            _intensity = Mathf.Max(0.0f, _intensity - FlashDecay * (float)delta);
            UpdateVisuals();

            if (_intensity <= 0.0f && FlashRect != null)
            {
                FlashRect.Visible = false;
            }
        }
    }

    public void Flash(float damageAmount = 20.0f)
    {
        // Scale flash strength based on damage (10 dmg -> ~0.45, 30 dmg -> ~0.70, 50+ dmg -> ~0.95)
        float boost = Mathf.Clamp(0.35f + (damageAmount / 45.0f) * 0.5f, 0.4f, 0.95f);
        _intensity = Mathf.Clamp(_intensity + boost, 0.4f, 1.0f);

        if (FlashRect != null)
        {
            FlashRect.Visible = true;
        }
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (FlashRect == null) return;

        if (_material != null)
        {
            _material.SetShaderParameter("intensity", _intensity);
        }
        else
        {
            FlashRect.Color = new Color(FlashColor.R, FlashColor.G, FlashColor.B, _intensity * 0.45f);
        }
    }
}
