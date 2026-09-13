using Godot;
using System.Collections.Generic;

public partial class DamageOverlay : CanvasLayer
{
    [Export] public ColorRect FlashRect;
    [Export] public float FlashDecay = 2.4f;
    [Export] public Color FlashColor = new Color(0.85f, 0.04f, 0.04f, 1.0f);

    private float _intensity = 0.0f;
    private ShaderMaterial _material;
    private DirectionalIndicatorControl _indicatorControl;

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

        _indicatorControl = new DirectionalIndicatorControl();
        AddChild(_indicatorControl);
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

    public void AddDirectionalHit(Vector3 attackerWorldPos)
    {
        _indicatorControl?.AddHit(attackerWorldPos);
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

    private partial class DirectionalIndicatorControl : Control
    {
        private class HitIndicator
        {
            public Vector3 AttackerPos;
            public float Intensity = 1.0f;
        }

        private readonly List<HitIndicator> _hits = new();

        public override void _Ready()
        {
            AnchorLeft = 0f;
            AnchorTop = 0f;
            AnchorRight = 1f;
            AnchorBottom = 1f;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void AddHit(Vector3 pos)
        {
            _hits.Add(new HitIndicator { AttackerPos = pos, Intensity = 1.0f });
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            if (_hits.Count == 0) return;

            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                _hits[i].Intensity -= (float)delta * 1.25f; // Fades in ~0.8s
                if (_hits[i].Intensity <= 0f)
                {
                    _hits.RemoveAt(i);
                }
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_hits.Count == 0) return;

            PlayerController player = PlayerController.Instance;
            if (player == null || player.Camera == null) return;

            Vector3 camPos = player.Camera.GlobalPosition;
            Transform3D camTrans = player.Camera.GlobalTransform;

            Vector3 camForward = -camTrans.Basis.Z;
            camForward.Y = 0f;
            if (camForward.LengthSquared() < 0.001f) return;
            camForward = camForward.Normalized();

            Vector3 camRight = camTrans.Basis.X;
            camRight.Y = 0f;
            if (camRight.LengthSquared() < 0.001f) return;
            camRight = camRight.Normalized();

            Vector2 center = GetViewportRect().Size * 0.5f;
            float radius = Mathf.Min(center.X, center.Y) * 0.42f;

            foreach (var hit in _hits)
            {
                Vector3 toAttacker = hit.AttackerPos - camPos;
                toAttacker.Y = 0f;
                if (toAttacker.LengthSquared() < 0.01f) continue;
                toAttacker = toAttacker.Normalized();

                float fDot = toAttacker.Dot(camForward);
                float rDot = toAttacker.Dot(camRight);

                // Angle on screen: 0 is UP, PI/2 is RIGHT, -PI/2 is LEFT, PI is DOWN
                float angle = Mathf.Atan2(rDot, fDot);
                float drawAngle = angle - Mathf.Pi * 0.5f; // Screen 0 is +X (3 o'clock)

                float arcSpan = Mathf.DegToRad(38f);
                Color arcColor = new Color(0.95f, 0.08f, 0.08f, hit.Intensity * 0.85f);

                DrawArc(center, radius, drawAngle - arcSpan * 0.5f, drawAngle + arcSpan * 0.5f, 20, arcColor, 5.0f, true);

                // Directional chevron arrow tip
                Vector2 tip = center + new Vector2(Mathf.Cos(drawAngle), Mathf.Sin(drawAngle)) * (radius + 10f);
                Vector2 leftBase = center + new Vector2(Mathf.Cos(drawAngle - 0.10f), Mathf.Sin(drawAngle - 0.10f)) * (radius - 4f);
                Vector2 rightBase = center + new Vector2(Mathf.Cos(drawAngle + 0.10f), Mathf.Sin(drawAngle + 0.10f)) * (radius - 4f);
                DrawColoredPolygon(new Vector2[] { tip, leftBase, rightBase }, arcColor);
            }
        }
    }
}
