using Godot;

public partial class FruitSplatter : Node3D
{
    [Export] public float Lifetime = 1.8f;
    [Export] public OmniLight3D SplashLight;
    [Export] public AudioStreamPlayer3D AudioPlayer;
    [Export] public Godot.Collections.Array<GpuParticles3D> ParticleEmitters = new();
    [Export] public Color SplatColor = new Color(0.98f, 0.45f, 0.15f, 1f); // Juicy citrus orange default

    private bool _colorApplied = false;

    public override void _Ready()
    {
        if (ParticleEmitters.Count == 0)
        {
            foreach (Node child in GetChildren())
            {
                if (child is GpuParticles3D emitter)
                {
                    ParticleEmitters.Add(emitter);
                }
            }
        }

        if (!_colorApplied)
        {
            ApplyColor(SplatColor);
        }

        foreach (GpuParticles3D emitter in ParticleEmitters)
        {
            if (emitter != null)
            {
                emitter.Restart();
                emitter.Emitting = true;
            }
        }

        if (AudioPlayer != null && !AudioPlayer.Playing && AudioPlayer.Stream != null)
        {
            AudioPlayer.Play();
        }

        if (SplashLight != null)
        {
            Tween tween = CreateTween();
            tween.TweenProperty(SplashLight, "light_energy", 0f, 0.25f);
        }

        GetTree().CreateTimer(Lifetime).Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(QueueFree));
    }

    public void SetSplatColor(Color color)
    {
        SplatColor = color;
        ApplyColor(color);
    }

    private void ApplyColor(Color color)
    {
        _colorApplied = true;

        if (SplashLight != null)
        {
            SplashLight.LightColor = color;
        }

        foreach (var emitter in ParticleEmitters)
        {
            if (emitter == null) continue;

            if (emitter.MaterialOverride is StandardMaterial3D mat)
            {
                var dupMat = (StandardMaterial3D)mat.Duplicate();
                dupMat.AlbedoColor = color;
                emitter.MaterialOverride = dupMat;
            }

            if (emitter.ProcessMaterial is ParticleProcessMaterial ppm)
            {
                var dupPpm = (ParticleProcessMaterial)ppm.Duplicate();
                dupPpm.Color = color;
                emitter.ProcessMaterial = dupPpm;
            }
        }
    }
}
