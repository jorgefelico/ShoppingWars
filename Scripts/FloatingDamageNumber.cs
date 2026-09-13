using Godot;
using System;

public partial class FloatingDamageNumber : Node3D
{
    private Label3D _label;
    private float _timer = 0f;
    private const float Lifetime = 0.95f;
    private Vector3 _velocity;
    private Color _baseColor;
    private float _initialTilt = 0f;

    public override void _Ready()
    {
        EnsureLabel();
        _initialTilt = (float)GD.RandRange(-14.0, 14.0);
        RotationDegrees = new Vector3(0, 0, _initialTilt);

        _velocity = new Vector3(
            (float)GD.RandRange(-0.45, 0.45),
            (float)GD.RandRange(1.8, 2.4),
            (float)GD.RandRange(-0.45, 0.45)
        );
    }

    private void EnsureLabel()
    {
        if (_label != null) return;
        _label = GetNodeOrNull<Label3D>("Label3D");
        if (_label == null)
        {
            _label = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                OutlineSize = 12,
                OutlineModulate = Colors.Black,
                PixelSize = 0.0055f,
                RenderPriority = 10
            };
            AddChild(_label);
        }
    }

    public void Setup(int amount, string comicSuffix = null)
    {
        EnsureLabel();

        if (amount < 20)
        {
            _baseColor = new Color(1.0f, 0.95f, 0.35f); // Bright Lemon Yellow
            _label.FontSize = 34;
            _label.Text = string.IsNullOrEmpty(comicSuffix) ? $"-{amount}" : $"-{amount} {comicSuffix}";
        }
        else if (amount < 40)
        {
            _baseColor = new Color(1.0f, 0.55f, 0.12f); // Fiery Neon Orange
            _label.FontSize = 44;
            _label.Text = string.IsNullOrEmpty(comicSuffix) ? $"-{amount} POW!" : $"-{amount} {comicSuffix}";
        }
        else
        {
            _baseColor = new Color(1.0f, 0.15f, 0.25f); // Super Crimson CRIT
            _label.FontSize = 54;
            _label.Text = string.IsNullOrEmpty(comicSuffix) ? $"-{amount} CRIT!" : $"-{amount} {comicSuffix}";
        }

        _label.Modulate = _baseColor;
    }

    public void SetupCustom(string text, Color color, int fontSize = 48)
    {
        EnsureLabel();
        _baseColor = color;
        _label.FontSize = fontSize;
        _label.Text = text;
        _label.Modulate = color;
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

        // Rise with smooth comic deceleration
        GlobalPosition += _velocity * (float)delta;
        _velocity.Y = Mathf.Max(0.3f, _velocity.Y - (float)delta * 2.2f);

        // Explosive comic punch scale (starts small, punches out to 1.35x, settles to 1.0x)
        float scale;
        if (progress < 0.12f)
        {
            scale = Mathf.Lerp(0.4f, 1.38f, progress / 0.12f);
        }
        else if (progress < 0.28f)
        {
            scale = Mathf.Lerp(1.38f, 1.0f, (progress - 0.12f) / 0.16f);
        }
        else
        {
            scale = 1.0f;
        }
        Scale = new Vector3(scale, scale, scale);

        // Alpha fade in final stretch
        float alpha = progress > 0.5f
            ? Mathf.Clamp(1.0f - (progress - 0.5f) / 0.5f, 0f, 1f)
            : 1.0f;

        if (_label != null)
        {
            _label.Modulate = new Color(_baseColor.R, _baseColor.G, _baseColor.B, alpha);
        }
    }

    public static void Spawn(Node context, Vector3 position, int amount, string suffix = null)
    {
        if (context == null || !GodotObject.IsInstanceValid(context) || amount <= 0) return;

        Node targetParent = context.GetTree()?.CurrentScene ?? context;
        if (targetParent == null) return;

        FloatingDamageNumber popup = new();
        targetParent.AddChild(popup);

        Vector3 spawnOffset = new Vector3(
            (float)GD.RandRange(-0.3, 0.3),
            (float)GD.RandRange(1.2, 1.6),
            (float)GD.RandRange(-0.3, 0.3)
        );
        popup.GlobalPosition = position + spawnOffset;
        popup.Setup(amount, suffix);
    }

    public static void SpawnText(Node context, Vector3 position, string text, Color color, int fontSize = 48)
    {
        if (context == null || !GodotObject.IsInstanceValid(context) || string.IsNullOrEmpty(text)) return;

        Node targetParent = context.GetTree()?.CurrentScene ?? context;
        if (targetParent == null) return;

        FloatingDamageNumber popup = new();
        targetParent.AddChild(popup);

        Vector3 spawnOffset = new Vector3(
            (float)GD.RandRange(-0.2, 0.2),
            (float)GD.RandRange(1.3, 1.7),
            (float)GD.RandRange(-0.2, 0.2)
        );
        popup.GlobalPosition = position + spawnOffset;
        popup.SetupCustom(text, color, fontSize);
    }
}
