using Godot;
using System;

public partial class FloatingDamageNumber : Node3D
{
    private Label3D _label;
    private float _timer = 0f;
    private const float Lifetime = 0.85f;
    private Vector3 _velocity;
    private Color _baseColor;

    public override void _Ready()
    {
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            PixelSize = 0.005f
        };
        AddChild(_label);

        _velocity = new Vector3(
            (float)GD.RandRange(-0.35, 0.35),
            (float)GD.RandRange(1.4, 1.9),
            (float)GD.RandRange(-0.35, 0.35)
        );
    }

    public void Setup(int amount)
    {
        if (_label == null)
        {
            _label = GetNodeOrNull<Label3D>("Label3D");
            if (_label == null)
            {
                _label = new Label3D
                {
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    NoDepthTest = true,
                    OutlineSize = 10,
                    OutlineModulate = Colors.Black,
                    PixelSize = 0.005f
                };
                AddChild(_label);
            }
        }

        _label.Text = $"-{amount}";

        if (amount < 25)
        {
            _baseColor = new Color(1.0f, 0.95f, 0.40f); // Bright yellow
            _label.FontSize = 32;
        }
        else if (amount < 45)
        {
            _baseColor = new Color(1.0f, 0.52f, 0.10f); // Fiery orange
            _label.FontSize = 38;
        }
        else
        {
            _baseColor = new Color(1.0f, 0.12f, 0.20f); // Crimson red (heavy damage)
            _label.FontSize = 46;
        }

        _label.Modulate = _baseColor;
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        float progress = _timer / Lifetime;

        if (progress >= 1.0f)
        {
            QueueFree();
            return;
        }

        // Float upwards and drift horizontally
        GlobalPosition += _velocity * (float)delta;
        _velocity.Y = Mathf.Max(0.4f, _velocity.Y - (float)delta * 1.2f); // Gentle gravity decel

        // Pop in scale then fade alpha in second half
        float scale = progress < 0.15f
            ? Mathf.Lerp(0.6f, 1.2f, progress / 0.15f)
            : Mathf.Lerp(1.2f, 1.0f, (progress - 0.15f) / 0.85f);
        Scale = new Vector3(scale, scale, scale);

        float alpha = progress > 0.4f
            ? Mathf.Clamp(1.0f - (progress - 0.4f) / 0.6f, 0f, 1f)
            : 1.0f;

        if (_label != null)
        {
            _label.Modulate = new Color(_baseColor.R, _baseColor.G, _baseColor.B, alpha);
        }
    }

    public static void Spawn(Node context, Vector3 position, int amount)
    {
        if (context == null || !GodotObject.IsInstanceValid(context) || amount <= 0) return;

        Node targetParent = context.GetTree()?.CurrentScene ?? context;
        if (targetParent == null) return;

        FloatingDamageNumber popup = new();
        targetParent.AddChild(popup);

        Vector3 spawnOffset = new Vector3(
            (float)GD.RandRange(-0.25, 0.25),
            (float)GD.RandRange(1.3, 1.7),
            (float)GD.RandRange(-0.25, 0.25)
        );
        popup.GlobalPosition = position + spawnOffset;
        popup.Setup(amount);
    }
}
