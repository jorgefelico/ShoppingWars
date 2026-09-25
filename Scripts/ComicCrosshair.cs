using Godot;
using System;

public partial class ComicCrosshair : Control
{
    private float _currentSpread = BaseSpread;
    private float _targetSpread = BaseSpread;
    private Color _currentColor = UITheme.PaperWhite;
    private Color _targetColor = UITheme.PaperWhite;
    private bool _isTargetingInteractable = false;
    private float _pulseTimer = 0f;
    private float _chargeProgress = 0f;

    public void SetChargeProgress(float progress)
    {
        _chargeProgress = Mathf.Clamp(progress, 0f, 1f);
        QueueRedraw();
    }

    public const float BaseSpread = 5.5f;
    public const float WalkSpread = 8.5f;
    public const float SprintSpread = 12.5f;
    public const float AirSpread = 15.0f;

    public override void _Ready()
    {
        AnchorLeft = 0.5f;
        AnchorTop = 0.5f;
        AnchorRight = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -32f;
        OffsetTop = -32f;
        OffsetRight = 32f;
        OffsetBottom = 32f;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void UpdateCrosshair(float horizontalSpeed, bool isSprinting, bool isAirborne, bool isTargetingInteractable, bool isTargetingHostile = false)
    {
        _isTargetingInteractable = isTargetingInteractable;

        // Calculate target spread based on locomotion
        if (isAirborne)
        {
            _targetSpread = AirSpread;
        }
        else if (isSprinting && horizontalSpeed > 3.0f)
        {
            _targetSpread = SprintSpread;
        }
        else if (horizontalSpeed > 0.5f)
        {
            _targetSpread = WalkSpread;
        }
        else
        {
            _targetSpread = BaseSpread;
        }

        // Color targeting
        if (isTargetingHostile)
        {
            _targetColor = UITheme.ActionRed;
        }
        else if (isTargetingInteractable)
        {
            _targetColor = UITheme.FlyerYellow;
        }
        else
        {
            _targetColor = UITheme.PaperWhite;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _pulseTimer += dt * 8.0f;

        // Smooth spread transition
        _currentSpread = Mathf.Lerp(_currentSpread, _targetSpread, dt * 15.0f);

        // Smooth color transition
        _currentColor = _currentColor.Lerp(_targetColor, dt * 12.0f);

        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 center = Size * 0.5f;

        // 1. Center Pip
        float pipRadius = 1.4f;
        // High-contrast ink border
        DrawCircle(center, pipRadius + 0.8f, UITheme.InkBlack);
        // Colored core
        DrawCircle(center, pipRadius, _currentColor);

        // 2. Reticle Brackets (Top, Bottom, Left, Right)
        float spread = _currentSpread;
        float tickLen = 4.0f;
        float tickWidth = 1.6f;

        // When interacting, apply a tiny subtle pulse
        if (_isTargetingInteractable)
        {
            spread += Mathf.Sin(_pulseTimer) * 1.0f;
        }

        // Fastball primed energetic tremor
        if (_chargeProgress >= 0.85f)
        {
            spread += (float)GD.RandRange(-0.5, 0.5);
        }

        // Shadow / Outline Pass (Thick black underlay for 100% readability against any supermarket lighting)
        DrawReticleTicks(center, spread, tickLen, tickWidth + 1.4f, UITheme.InkBlack);

        // Foreground Crisp Color Pass
        DrawReticleTicks(center, spread, tickLen, tickWidth, _currentColor);

        // 3. Throw Charge Arc Meter
        if (_chargeProgress > 0.02f)
        {
            float gaugeRadius = 8.5f;
            // Dark ink outline ring
            DrawArc(center, gaugeRadius, 0f, Mathf.Tau, 32, UITheme.InkBlack, 2.6f, true);
            // Gauge track underlay
            DrawArc(center, gaugeRadius, 0f, Mathf.Tau, 32, new Color(0.15f, 0.15f, 0.20f, 0.65f), 1.6f, true);

            // Progress fill arc
            float startAngle = -Mathf.Pi / 2.0f;
            float sweep = _chargeProgress * Mathf.Tau;
            Color chargeCol = (_chargeProgress < 0.85f)
                ? UITheme.FlyerYellow.Lerp(UITheme.ClearanceOrange, _chargeProgress / 0.85f)
                : ((Mathf.Sin(_pulseTimer * 3.5f) > 0f) ? UITheme.ActionRed : UITheme.FlyerYellow);

            DrawArc(center, gaugeRadius, startAngle, startAngle + sweep, 28, chargeCol, 1.8f, true);

            // Primed fastball corner flares
            if (_chargeProgress >= 0.85f)
            {
                float flareDist = 11.0f + Mathf.Sin(_pulseTimer * 4.0f) * 0.8f;
                float[] angles = { Mathf.Pi * 0.25f, Mathf.Pi * 0.75f, Mathf.Pi * 1.25f, Mathf.Pi * 1.75f };
                foreach (float angle in angles)
                {
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    DrawLine(center + dir * flareDist, center + dir * (flareDist + 2.8f), chargeCol, 1.6f, true);
                }
            }
        }

        // 4. Special Interaction Reticle Corners when hovering items
        if (_isTargetingInteractable)
        {
            float cornerDist = spread + tickLen + 2.0f;
            Color cueColor = new Color(_currentColor.R, _currentColor.G, _currentColor.B, 0.85f);
            
            // Subtle 4-corner targeting brackets
            float cornerLen = 3.0f;
            // Top-Left corner
            DrawLine(new Vector2(center.X - cornerDist, center.Y - cornerDist), new Vector2(center.X - cornerDist + cornerLen, center.Y - cornerDist), cueColor, 1.4f);
            DrawLine(new Vector2(center.X - cornerDist, center.Y - cornerDist), new Vector2(center.X - cornerDist, center.Y - cornerDist + cornerLen), cueColor, 1.4f);
            // Top-Right corner
            DrawLine(new Vector2(center.X + cornerDist, center.Y - cornerDist), new Vector2(center.X + cornerDist - cornerLen, center.Y - cornerDist), cueColor, 1.4f);
            DrawLine(new Vector2(center.X + cornerDist, center.Y - cornerDist), new Vector2(center.X + cornerDist, center.Y - cornerDist + cornerLen), cueColor, 1.4f);
            // Bottom-Left corner
            DrawLine(new Vector2(center.X - cornerDist, center.Y + cornerDist), new Vector2(center.X - cornerDist + cornerLen, center.Y + cornerDist), cueColor, 1.4f);
            DrawLine(new Vector2(center.X - cornerDist, center.Y + cornerDist), new Vector2(center.X - cornerDist, center.Y + cornerDist - cornerLen), cueColor, 1.4f);
            // Bottom-Right corner
            DrawLine(new Vector2(center.X + cornerDist, center.Y + cornerDist), new Vector2(center.X + cornerDist - cornerLen, center.Y + cornerDist), cueColor, 1.4f);
            DrawLine(new Vector2(center.X + cornerDist, center.Y + cornerDist), new Vector2(center.X + cornerDist, center.Y + cornerDist - cornerLen), cueColor, 1.4f);
        }
    }

    private void DrawReticleTicks(Vector2 center, float spread, float len, float width, Color color)
    {
        // Top
        DrawLine(center + new Vector2(0, -spread), center + new Vector2(0, -spread - len), color, width, true);
        // Bottom
        DrawLine(center + new Vector2(0, spread), center + new Vector2(0, spread + len), color, width, true);
        // Left
        DrawLine(center + new Vector2(-spread, 0), center + new Vector2(-spread - len, 0), color, width, true);
        // Right
        DrawLine(center + new Vector2(spread, 0), center + new Vector2(spread + len, 0), color, width, true);
    }
}
