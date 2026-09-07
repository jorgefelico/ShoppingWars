using Godot;

public partial class WatermelonSplatter : Node3D
{
    [Export] public float Lifetime = 2.0f;
    [Export] public OmniLight3D SplashLight;
    [Export] public AudioStreamPlayer3D AudioPlayer;
    [Export] public Godot.Collections.Array<GpuParticles3D> ParticleEmitters = new();

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
}
