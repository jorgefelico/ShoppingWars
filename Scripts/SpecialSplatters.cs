using Godot;
using System;

public static class SpecialSplatters
{
    public static void SpawnSoap(Node context, Vector3 position)
    {
        if (context == null || !GodotObject.IsInstanceValid(context)) return;
        Node parent = context.GetTree()?.CurrentScene ?? context;
        if (parent == null) return;

        var root = new Node3D { Name = "SoapSplatter" };
        parent.AddChild(root);
        root.GlobalPosition = position;

        // Flash of bubbly cyan/white light
        var light = new OmniLight3D
        {
            LightColor = new Color(0.6f, 0.95f, 1.0f),
            LightEnergy = 2.5f,
            OmniRange = 4.5f
        };
        root.AddChild(light);
        var tween = root.CreateTween();
        tween.TweenProperty(light, "light_energy", 0f, 0.35f);

        // Bubbly soapy foam particles
        var particles = new CpuParticles3D
        {
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.9f,
            Amount = 26,
            Lifetime = 0.85f,
            Direction = Vector3.Up,
            Spread = 85.0f,
            InitialVelocityMin = 2.5f,
            InitialVelocityMax = 5.2f,
            Gravity = new Vector3(0, -5.5f, 0)
        };

        var bubbleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.98f, 1.0f, 0.85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.1f,
            Metallic = 0.1f,
            RimEnabled = true,
            Rim = 0.6f,
            RimTint = 0.4f
        };
        var sphereMesh = new SphereMesh
        {
            Radius = 0.08f,
            Height = 0.16f,
            Material = bubbleMat
        };
        particles.Mesh = sphereMesh;
        root.AddChild(particles);

        // Procedural soapy squish audio
        var player = new AudioStreamPlayer3D
        {
            UnitSize = 10.0f,
            MaxDistance = 35.0f,
            VolumeDb = 1.0f,
            Bus = "Master",
            Stream = CreateSoapSound()
        };
        root.AddChild(player);
        player.Play();

        // Comic popup
        FloatingDamageNumber.SpawnText(context, position, "SUDS!", new Color(0.4f, 0.9f, 1.0f), 42);

        root.GetTree().CreateTimer(1.2f).Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(root.QueueFree));
    }

    public static void SpawnSoda(Node context, Vector3 position)
    {
        if (context == null || !GodotObject.IsInstanceValid(context)) return;
        Node parent = context.GetTree()?.CurrentScene ?? context;
        if (parent == null) return;

        var root = new Node3D { Name = "SodaSplatter" };
        parent.AddChild(root);
        root.GlobalPosition = position;

        var light = new OmniLight3D
        {
            LightColor = new Color(0.95f, 0.35f, 0.2f),
            LightEnergy = 2.0f,
            OmniRange = 3.5f
        };
        root.AddChild(light);
        var tween = root.CreateTween();
        tween.TweenProperty(light, "light_energy", 0f, 0.25f);

        // Pressurized fizzy droplets
        var particles = new CpuParticles3D
        {
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.95f,
            Amount = 35,
            Lifetime = 0.6f,
            Direction = Vector3.Up,
            Spread = 75.0f,
            InitialVelocityMin = 4.0f,
            InitialVelocityMax = 7.5f,
            Gravity = new Vector3(0, -11.0f, 0)
        };

        var sodaMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.25f, 0.15f),
            Roughness = 0.2f,
            Metallic = 0.3f
        };
        var dropletMesh = new SphereMesh
        {
            Radius = 0.045f,
            Height = 0.09f,
            Material = sodaMat
        };
        particles.Mesh = dropletMesh;
        root.AddChild(particles);

        var player = new AudioStreamPlayer3D
        {
            UnitSize = 10.0f,
            MaxDistance = 35.0f,
            VolumeDb = 2.0f,
            Bus = "Master",
            Stream = CreateSodaFizzSound()
        };
        root.AddChild(player);
        player.Play();

        FloatingDamageNumber.SpawnText(context, position, "FIZZ!", new Color(1.0f, 0.45f, 0.2f), 42);

        root.GetTree().CreateTimer(1.0f).Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(root.QueueFree));
    }

    public static void SpawnCrumbs(Node context, Vector3 position)
    {
        if (context == null || !GodotObject.IsInstanceValid(context)) return;
        Node parent = context.GetTree()?.CurrentScene ?? context;
        if (parent == null) return;

        var root = new Node3D { Name = "CrumbSplatter" };
        parent.AddChild(root);
        root.GlobalPosition = position;

        var particles = new CpuParticles3D
        {
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.92f,
            Amount = 24,
            Lifetime = 0.7f,
            Direction = Vector3.Up,
            Spread = 85.0f,
            InitialVelocityMin = 2.0f,
            InitialVelocityMax = 4.8f,
            Gravity = new Vector3(0, -8.0f, 0)
        };

        var crumbMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.75f, 0.45f),
            Roughness = 0.8f
        };
        var boxMesh = new BoxMesh
        {
            Size = new Vector3(0.06f, 0.06f, 0.06f),
            Material = crumbMat
        };
        particles.Mesh = boxMesh;
        root.AddChild(particles);

        var player = new AudioStreamPlayer3D
        {
            UnitSize = 10.0f,
            MaxDistance = 35.0f,
            VolumeDb = 1.0f,
            Bus = "Master",
            Stream = CreateCrunchSound()
        };
        root.AddChild(player);
        player.Play();

        FloatingDamageNumber.SpawnText(context, position, "CRUNCH!", new Color(1.0f, 0.82f, 0.35f), 40);

        root.GetTree().CreateTimer(1.0f).Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(root.QueueFree));
    }

    public static void SpawnZap(Node context, Vector3 position)
    {
        if (context == null || !GodotObject.IsInstanceValid(context)) return;
        Node parent = context.GetTree()?.CurrentScene ?? context;
        if (parent == null) return;

        var root = new Node3D { Name = "ZapSplatter" };
        parent.AddChild(root);
        root.GlobalPosition = position;

        var light = new OmniLight3D
        {
            LightColor = new Color(0.3f, 0.85f, 1.0f),
            LightEnergy = 4.0f,
            OmniRange = 5.0f
        };
        root.AddChild(light);
        var tween = root.CreateTween();
        tween.TweenProperty(light, "light_energy", 0f, 0.22f);

        // Electric zap spark particles
        var particles = new CpuParticles3D
        {
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.98f,
            Amount = 28,
            Lifetime = 0.4f,
            Direction = Vector3.Up,
            Spread = 90.0f,
            InitialVelocityMin = 3.5f,
            InitialVelocityMax = 7.0f,
            Gravity = new Vector3(0, -3.0f, 0)
        };

        var sparkMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.9f, 1.0f),
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.9f, 1.0f),
            EmissionEnergyMultiplier = 4.5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var sparkMesh = new BoxMesh
        {
            Size = new Vector3(0.04f, 0.04f, 0.12f),
            Material = sparkMat
        };
        particles.Mesh = sparkMesh;
        root.AddChild(particles);

        var player = new AudioStreamPlayer3D
        {
            UnitSize = 10.0f,
            MaxDistance = 35.0f,
            VolumeDb = 2.0f,
            Bus = "Master",
            Stream = CreateZapSound()
        };
        root.AddChild(player);
        player.Play();

        FloatingDamageNumber.SpawnText(context, position, "ZAP!", new Color(0.2f, 0.9f, 1.0f), 44);

        root.GetTree().CreateTimer(0.8f).Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(root.QueueFree));
    }

    private static AudioStreamWav CreateSoapSound()
    {
        int sampleRate = 22050;
        float duration = 0.15f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = Mathf.Sin(progress * Mathf.Pi);
            float freq = Mathf.Lerp(450f, 220f, progress);
            float sample = Mathf.Sin(t * Mathf.Tau * freq) * env;
            sample += (float)GD.RandRange(-0.1, 0.1) * env;

            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.7f);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Data = data
        };
    }

    private static AudioStreamWav CreateSodaFizzSound()
    {
        int sampleRate = 22050;
        float duration = 0.2f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = 1.0f - progress;
            // High frequency hiss noise
            float sample = (float)GD.RandRange(-0.8, 0.8) * env * env;
            // Pop thump at the very start
            if (progress < 0.2f)
            {
                sample += Mathf.Sin(t * Mathf.Tau * 180f) * (1.0f - progress / 0.2f);
            }

            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.75f);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Data = data
        };
    }

    private static AudioStreamWav CreateCrunchSound()
    {
        int sampleRate = 22050;
        float duration = 0.14f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = 1.0f - progress;
            float sample = Mathf.Sin(t * Mathf.Tau * 260f) * env;
            sample += (float)GD.RandRange(-0.4, 0.4) * env;

            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.7f);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Data = data
        };
    }

    private static AudioStreamWav CreateZapSound()
    {
        int sampleRate = 22050;
        float duration = 0.16f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = 1.0f - progress;
            // High buzzing sawtooth wave
            float buzz = ((t * 880f) % 1.0f) * 2.0f - 1.0f;
            float sample = buzz * env + (float)GD.RandRange(-0.25, 0.25) * env;

            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.8f);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Data = data
        };
    }
}
