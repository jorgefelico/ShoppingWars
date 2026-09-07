using Godot;

public partial class Explosion : Node3D
{
    [Export] public float Lifetime = 2.0f;
    [Export] public OmniLight3D FlashLight;
    [Export] public AudioStreamPlayer3D AudioPlayer;
    [Export] public Godot.Collections.Array<GpuParticles3D> ParticleEmitters = new();

    public override void _Ready()
    {
        // Emit all particle systems
        if (ParticleEmitters != null)
        {
            foreach (GpuParticles3D emitter in ParticleEmitters)
            {
                if (emitter != null)
                {
                    emitter.Emitting = true;
                }
            }
        }

        // Play explosion sound
        if (AudioPlayer != null && !AudioPlayer.Playing)
        {
            AudioPlayer.Play();
        }

        // Fade out the initial flash light
        if (FlashLight != null)
        {
            Tween tween = CreateTween();
            tween.TweenProperty(FlashLight, "light_energy", 0f, 0.35f);
        }

        // Auto clean up after lifetime expires
        GetTree().CreateTimer(Lifetime).Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(QueueFree));
    }
}
