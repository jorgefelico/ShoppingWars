using Godot;
using System;

public partial class CombatHitEffect : Node3D
{
    [Export] public float Lifetime = 0.6f;
    private CpuParticles3D _sparks;
    private OmniLight3D _flashLight;
    private AudioStreamPlayer3D _audioPlayer;

    public override void _Ready()
    {
        // Impact flash light
        _flashLight = new OmniLight3D
        {
            LightColor = new Color(1.0f, 0.85f, 0.5f),
            LightEnergy = 3.0f,
            OmniRange = 4.0f,
            OmniAttenuation = 1.8f
        };
        AddChild(_flashLight);

        // CPU particles for sparks / impact debris
        _sparks = new CpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 0.95f,
            Amount = 14,
            Lifetime = 0.45f,
            Direction = Vector3.Up,
            Spread = 80.0f,
            InitialVelocityMin = 3.5f,
            InitialVelocityMax = 6.0f,
            Gravity = new Vector3(0, -9.8f, 0),
            Color = new Color(1.0f, 0.75f, 0.35f, 1.0f)
        };

        // Standard material for sparks
        StandardMaterial3D sparkMat = new()
        {
            AlbedoColor = new Color(1.0f, 0.8f, 0.4f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.75f, 0.3f),
            EmissionEnergyMultiplier = 4.0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        BoxMesh sparkMesh = new()
        {
            Size = new Vector3(0.04f, 0.04f, 0.04f),
            Material = sparkMat
        };
        _sparks.Mesh = sparkMesh;
        AddChild(_sparks);
        _sparks.Emitting = true;

        // Procedural crisp impact thud
        _audioPlayer = new AudioStreamPlayer3D
        {
            UnitSize = 10.0f,
            MaxDistance = 35.0f,
            VolumeDb = 2.0f,
            Bus = "Master"
        };
        _audioPlayer.Stream = CreateImpactSound();
        AddChild(_audioPlayer);
        _audioPlayer.Play();

        // Flash tween
        Tween tween = CreateTween();
        tween.TweenProperty(_flashLight, "light_energy", 0f, 0.18f);

        GetTree().CreateTimer(Lifetime).Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(QueueFree));
    }

    private AudioStreamWav CreateImpactSound()
    {
        int sampleRate = 22050;
        float duration = 0.12f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            // Quick thump envelope
            float env = Mathf.Clamp(1.0f - progress, 0f, 1f);
            env = env * env * env;

            // Pitch drop for punchy impact "thwack"
            float freq = Mathf.Lerp(320f, 90f, progress);
            float sample = Mathf.Sin(t * Mathf.Tau * freq) * env;
            // Add a little noise for crunch
            sample += (float)GD.RandRange(-0.15, 0.15) * env;

            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.75f);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav();
        wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
        wav.MixRate = sampleRate;
        wav.Data = data;
        return wav;
    }

    public static void Spawn(Node context, Vector3 position)
    {
        if (context == null || !GodotObject.IsInstanceValid(context)) return;
        Node parent = context.GetTree()?.CurrentScene ?? context;
        if (parent == null) return;

        CombatHitEffect effect = new();
        parent.AddChild(effect);
        effect.GlobalPosition = position;
    }
}
