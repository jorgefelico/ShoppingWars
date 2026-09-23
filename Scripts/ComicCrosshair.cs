using Godot;
using System;

public partial class ComicCrosshair : Control
{
    private float _currentSpread = 9.0f;
    private float _targetSpread = 9.0f;
    private Color _currentColor = UITheme.PaperWhite;
    private Color _targetColor = UITheme.PaperWhite;
    private bool _isTargetingInteractable = false;
    private float _pulseTimer = 0f;

    public const float BaseSpread = 9.0f;
    public const float WalkSpread = 14.0f;
    public const float SprintSpread = 20.0f;
    public const float AirSpread = 24.0f;

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
        float pipRadius = 2.4f;
        // High-contrast ink border
        DrawCircle(center, pipRadius + 1.2f, UITheme.InkBlack);
        // Colored core
        DrawCircle(center, pipRadius, _currentColor);

        // 2. Reticle Brackets (Top, Bottom, Left, Right)
        float spread = _currentSpread;
        float tickLen = 6.0f;
        float tickWidth = 2.2f;

        // When interacting, apply a tiny subtle pulse
        if (_isTargetingInteractable)
        {
            spread += Mathf.Sin(_pulseTimer) * 1.5f;
        }

        // Shadow / Outline Pass (Thick black underlay for 100% readability against any supermarket lighting)
        DrawReticleTicks(center, spread, tickLen, tickWidth + 2.0f, UITheme.InkBlack);

        // Foreground Crisp Color Pass
        DrawReticleTicks(center, spread, tickLen, tickWidth, _currentColor);

        // 3. Special Interaction Reticle Corners when hovering items
        if (_isTargetingInteractable)
        {
            float cornerDist = spread + tickLen + 3.0f;
            Color cueColor = new Color(_currentColor.R, _currentColor.G, _currentColor.B, 0.85f);
            
            // Subtle 4-corner targeting brackets
            float cornerLen = 4.0f;
            // Top-Left corner
            DrawLine(new Vector2(center.X - cornerDist, center.Y - cornerDist), new Vector2(center.X - cornerDist + cornerLen, center.Y - cornerDist), cueColor, 1.8f);
            DrawLine(new Vector2(center.X - cornerDist, center.Y - cornerDist), new Vector2(center.X - cornerDist, center.Y - cornerDist + cornerLen), cueColor, 1.8f);
            // Top-Right corner
            DrawLine(new Vector2(center.X + cornerDist, center.Y - cornerDist), new Vector2(center.X + cornerDist - cornerLen, center.Y - cornerDist), cueColor, 1.8f);
            DrawLine(new Vector2(center.X + cornerDist, center.Y - cornerDist), new Vector2(center.X + cornerDist, center.Y - cornerDist + cornerLen), cueColor, 1.8f);
            // Bottom-Left corner
            DrawLine(new Vector2(center.X - cornerDist, center.Y + cornerDist), new Vector2(center.X - cornerDist + cornerLen, center.Y + cornerDist), cueColor, 1.8f);
            DrawLine(new Vector2(center.X - cornerDist, center.Y + cornerDist), new Vector2(center.X - cornerDist, center.Y + cornerDist - cornerLen), cueColor, 1.8f);
            // Bottom-Right corner
            DrawLine(new Vector2(center.X + cornerDist, center.Y + cornerDist), new Vector2(center.X + cornerDist - cornerLen, center.Y + cornerDist), cueColor, 1.8f);
            DrawLine(new Vector2(center.X + cornerDist, center.Y + cornerDist), new Vector2(center.X + cornerDist, center.Y + cornerDist - cornerLen), cueColor, 1.8f);
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
