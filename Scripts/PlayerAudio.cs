using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerAudio : Node3D
{
    private AudioStreamPlayer3D _footstepPlayer;
    private AudioStreamPlayer3D _jumpLandPlayer;
    private AudioStreamPlayer3D _actionPlayer;

    private AudioStream[] _footstepsLeft;
    private AudioStream[] _footstepsRight;
    private AudioStream[] _jumpSounds;
    private AudioStream[] _landSoftSounds;
    private AudioStream[] _landHardSounds;
    private AudioStream[] _throwWhooshSounds;
    private AudioStream _pickupBeepSound;
    private AudioStream _itemSwitchSound;
    private AudioStream _fastballWhooshSound;
    private AudioStream _batCrackSound;
    private AudioStream _metalClangSound;
    private AudioStream _parryThudSound;

    private int _leftStepIndex = 0;
    private int _rightStepIndex = 0;

    public override void _Ready()
    {
        string busName = "SFX";
        if (AudioServer.GetBusIndex(busName) == -1)
        {
            busName = "Master";
        }

        _footstepPlayer = Create3DPlayer("FootstepPlayer", busName, 10.0f, 32.0f);
        _jumpLandPlayer = Create3DPlayer("JumpLandPlayer", busName, 12.0f, 36.0f);
        _actionPlayer = Create3DPlayer("ActionPlayer", busName, 10.0f, 32.0f);

        AddChild(_footstepPlayer);
        AddChild(_jumpLandPlayer);
        AddChild(_actionPlayer);

        LoadAllSounds();
    }

    private static AudioStreamPlayer3D Create3DPlayer(string name, string bus, float unitSize, float maxDist)
    {
        return new AudioStreamPlayer3D
        {
            Name = name,
            Bus = bus,
            UnitSize = unitSize,
            MaxDistance = maxDist,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled
        };
    }

    private void LoadAllSounds()
    {
        // 1. Concrete Footstep Sounds (Kenney CC0 Impact Pack)
        AudioStream step0 = LoadStream("res://Sounds/Footsteps/footstep_concrete_000.ogg");
        AudioStream step1 = LoadStream("res://Sounds/Footsteps/footstep_concrete_001.ogg");
        AudioStream step2 = LoadStream("res://Sounds/Footsteps/footstep_concrete_002.ogg");
        AudioStream step3 = LoadStream("res://Sounds/Footsteps/footstep_concrete_003.ogg");
        AudioStream step4 = LoadStream("res://Sounds/Footsteps/footstep_concrete_004.ogg");

        var leftList = new List<AudioStream>();
        var rightList = new List<AudioStream>();

        if (step0 != null) leftList.Add(step0);
        if (step2 != null) leftList.Add(step2);
        if (step4 != null) leftList.Add(step4);

        if (step1 != null) rightList.Add(step1);
        if (step3 != null) rightList.Add(step3);

        if (leftList.Count == 0 && rightList.Count > 0) leftList.AddRange(rightList);
        if (rightList.Count == 0 && leftList.Count > 0) rightList.AddRange(leftList);

        if (leftList.Count > 0)
        {
            _footstepsLeft = leftList.ToArray();
            _footstepsRight = rightList.ToArray();
        }
        else
        {
            // Fallback to procedural generator if files missing
            _footstepsLeft = new AudioStream[]
            {
                GenerateFootstep(165f, 2100f, 0.085f),
                GenerateFootstep(172f, 2250f, 0.082f)
            };
            _footstepsRight = new AudioStream[]
            {
                GenerateFootstep(185f, 2350f, 0.088f),
                GenerateFootstep(192f, 2450f, 0.084f)
            };
        }

        // 2. Jump Sounds (Kenney CC0 FPS Pack)
        var jumpList = new List<AudioStream>();
        foreach (string file in new[] { "jump_a.ogg", "jump_b.ogg", "jump_c.ogg" })
        {
            var s = LoadStream($"res://Sounds/Player/{file}");
            if (s != null) jumpList.Add(s);
        }
        if (jumpList.Count > 0)
        {
            _jumpSounds = jumpList.ToArray();
        }
        else
        {
            _jumpSounds = new AudioStream[] { GenerateJumpSound() };
        }

        // 3. Land Sounds (Kenney CC0 Impact Pack)
        var landSoftList = new List<AudioStream>();
        foreach (string file in new[] { "land_soft_000.ogg", "land_soft_001.ogg", "land_soft_002.ogg" })
        {
            var s = LoadStream($"res://Sounds/Player/{file}");
            if (s != null) landSoftList.Add(s);
        }
        if (landSoftList.Count > 0)
        {
            _landSoftSounds = landSoftList.ToArray();
        }
        else
        {
            _landSoftSounds = new AudioStream[] { GenerateLandSound(0.5f) };
        }

        var landHardList = new List<AudioStream>();
        foreach (string file in new[] { "land_heavy_000.ogg", "land_heavy_001.ogg", "land_heavy_002.ogg" })
        {
            var s = LoadStream($"res://Sounds/Player/{file}");
            if (s != null) landHardList.Add(s);
        }
        if (landHardList.Count > 0)
        {
            _landHardSounds = landHardList.ToArray();
        }
        else
        {
            _landHardSounds = new AudioStream[] { GenerateLandSound(1.0f) };
        }

        // 4. Throw Whoosh Sounds (Kenney CC0 RPG Cloth Pack)
        var whooshList = new List<AudioStream>();
        foreach (string file in new[] { "cloth_1.ogg", "cloth_2.ogg", "cloth_3.ogg" })
        {
            var s = LoadStream($"res://Sounds/Player/{file}");
            if (s != null) whooshList.Add(s);
        }
        if (whooshList.Count > 0)
        {
            _throwWhooshSounds = whooshList.ToArray();
        }
        else
        {
            _throwWhooshSounds = new AudioStream[] { GenerateThrowWhoosh() };
        }

        // 5. Item Switch and Pickup Sounds
        _itemSwitchSound = LoadStream("res://Sounds/Player/item_switch.ogg") ?? GenerateThrowWhoosh();
        _pickupBeepSound = LoadStream("res://Sounds/Player/pickup_beep.ogg") ?? GeneratePickupBeep();

        // 6. Fastball and Batting / Deflect Sounds
        _fastballWhooshSound = GenerateFastballWhoosh();
        _batCrackSound = GenerateBatCrack();
        _metalClangSound = GenerateMetalClang();
        _parryThudSound = GenerateParryThud();
    }

    private static AudioStream LoadStream(string resPath)
    {
        if (string.IsNullOrEmpty(resPath)) return null;

        // 1. Try standard Godot ResourceLoader (if already imported by editor cache)
        try
        {
            if (ResourceLoader.Exists(resPath))
            {
                var stream = GD.Load<AudioStream>(resPath);
                if (stream != null) return stream;
            }
        }
        catch { }

        // 2. Direct OGG loader for raw files before/without editor import
        if (resPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var ogg = AudioStreamOggVorbis.LoadFromFile(resPath);
                if (ogg != null) return ogg;
            }
            catch { }

            try
            {
                string absPath = ProjectSettings.GlobalizePath(resPath);
                if (System.IO.File.Exists(absPath))
                {
                    var ogg = AudioStreamOggVorbis.LoadFromFile(absPath);
                    if (ogg != null) return ogg;
                }
            }
            catch { }
        }

        return null;
    }

    public void PlayFootstep(bool isRunning, bool isLeftFoot)
    {
        if (_footstepPlayer == null) return;
        if (_footstepsLeft == null || _footstepsRight == null) return;

        AudioStream step;
        if (isLeftFoot)
        {
            step = _footstepsLeft[_leftStepIndex % _footstepsLeft.Length];
            _leftStepIndex++;
        }
        else
        {
            step = _footstepsRight[_rightStepIndex % _footstepsRight.Length];
            _rightStepIndex++;
        }

        _footstepPlayer.Stream = step;
        _footstepPlayer.VolumeDb = isRunning ? -8.0f : -13.0f;
        _footstepPlayer.PitchScale = (float)GD.RandRange(0.97, 1.03);
        _footstepPlayer.Play();
    }

    public void PlayJump()
    {
        if (_jumpLandPlayer == null || _jumpSounds == null || _jumpSounds.Length == 0) return;

        int idx = GD.RandRange(0, _jumpSounds.Length - 1);
        _jumpLandPlayer.Stream = _jumpSounds[idx];
        _jumpLandPlayer.VolumeDb = -4.0f;
        _jumpLandPlayer.PitchScale = (float)GD.RandRange(0.97, 1.03);
        _jumpLandPlayer.Play();
    }

    public void PlayLand(float fallSpeed)
    {
        if (_jumpLandPlayer == null) return;

        bool isHard = fallSpeed > 6.5f;
        AudioStream[] list = isHard ? _landHardSounds : _landSoftSounds;
        if (list == null || list.Length == 0) return;

        int idx = GD.RandRange(0, list.Length - 1);
        _jumpLandPlayer.Stream = list[idx];
        _jumpLandPlayer.VolumeDb = isHard
            ? Mathf.Clamp(Mathf.Lerp(-6.0f, 0.0f, (fallSpeed - 6.5f) / 8.0f), -6.0f, 0.0f)
            : -7.0f;
        _jumpLandPlayer.PitchScale = (float)GD.RandRange(0.96, 1.04);
        _jumpLandPlayer.Play();
    }

    public void PlayThrowWhoosh()
    {
        if (_actionPlayer == null || _throwWhooshSounds == null || _throwWhooshSounds.Length == 0) return;

        int idx = GD.RandRange(0, _throwWhooshSounds.Length - 1);
        _actionPlayer.Stream = _throwWhooshSounds[idx];
        _actionPlayer.VolumeDb = -4.0f;
        _actionPlayer.PitchScale = (float)GD.RandRange(0.95, 1.05);
        _actionPlayer.Play();
    }

    public void PlayPickupChime()
    {
        if (_actionPlayer == null || _pickupBeepSound == null) return;
        _actionPlayer.Stream = _pickupBeepSound;
        _actionPlayer.VolumeDb = -5.0f;
        _actionPlayer.PitchScale = 1.0f;
        _actionPlayer.Play();
    }

    public void PlayItemSwitch()
    {
        if (_actionPlayer == null || _itemSwitchSound == null) return;
        _actionPlayer.Stream = _itemSwitchSound;
        _actionPlayer.VolumeDb = -8.0f;
        _actionPlayer.PitchScale = (float)GD.RandRange(0.97, 1.03);
        _actionPlayer.Play();
    }

    public void PlayFastballWhoosh()
    {
        if (_actionPlayer == null || _fastballWhooshSound == null) return;
        _actionPlayer.Stream = _fastballWhooshSound;
        _actionPlayer.VolumeDb = 1.5f;
        _actionPlayer.PitchScale = (float)GD.RandRange(1.05, 1.18);
        _actionPlayer.Play();
    }

    public void PlayBatDeflect(bool isMetallic, bool isBaseballBat)
    {
        if (_actionPlayer == null) return;
        if (isBaseballBat && _batCrackSound != null)
        {
            _actionPlayer.Stream = _batCrackSound;
            _actionPlayer.VolumeDb = 3.5f;
            _actionPlayer.PitchScale = (float)GD.RandRange(0.96, 1.04);
        }
        else if (isMetallic && _metalClangSound != null)
        {
            _actionPlayer.Stream = _metalClangSound;
            _actionPlayer.VolumeDb = 3.0f;
            _actionPlayer.PitchScale = (float)GD.RandRange(0.95, 1.05);
        }
        else if (_parryThudSound != null)
        {
            _actionPlayer.Stream = _parryThudSound;
            _actionPlayer.VolumeDb = 2.0f;
            _actionPlayer.PitchScale = (float)GD.RandRange(0.97, 1.03);
        }
        _actionPlayer.Play();
    }

    // --- Procedural Fallback Generators ---

    private static AudioStreamWav GenerateFootstep(float baseFreq, float squeakFreq, float duration)
    {
        int sampleRate = 22050;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = Mathf.Pow(1.0f - progress, 2.5f);
            float lowThump = Mathf.Sin(t * Mathf.Tau * baseFreq * (1.0f - progress * 0.4f)) * env;
            float squeakEnv = progress < 0.35f ? (1.0f - progress / 0.35f) : 0f;
            float squeak = Mathf.Sin(t * Mathf.Tau * squeakFreq) * squeakEnv * 0.35f;
            float noise = (float)GD.RandRange(-0.12, 0.12) * env;

            float sample = (lowThump * 0.7f + squeak + noise) * 0.85f;
            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
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

    private static AudioStreamWav GenerateJumpSound()
    {
        int sampleRate = 22050;
        float duration = 0.14f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = Mathf.Sin(progress * Mathf.Pi);
            float freq = Mathf.Lerp(140f, 360f, progress);
            float sample = Mathf.Sin(t * Mathf.Tau * freq) * env;
            if (progress < 0.25f)
            {
                sample += Mathf.Sin(t * Mathf.Tau * 1200f) * (1.0f - progress / 0.25f) * 0.3f;
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

    private static AudioStreamWav GenerateLandSound(float intensity)
    {
        int sampleRate = 22050;
        float duration = 0.16f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = Mathf.Pow(1.0f - progress, 2.0f);
            float freq = Mathf.Lerp(95f, 45f, progress);
            float sample = Mathf.Sin(t * Mathf.Tau * freq) * env;
            sample += (float)GD.RandRange(-0.25, 0.25) * env * intensity;
            if (progress < 0.3f)
            {
                sample += Mathf.Sin(t * Mathf.Tau * 1800f) * (1.0f - progress / 0.3f) * 0.25f;
            }

            short pcm = (short)(Mathf.Clamp(sample * intensity, -1f, 1f) * short.MaxValue * 0.8f);
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

    private static AudioStreamWav GenerateThrowWhoosh()
    {
        int sampleRate = 22050;
        float duration = 0.16f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = Mathf.Sin(progress * Mathf.Pi);
            float freq = Mathf.Lerp(220f, 620f, Mathf.Sin(progress * Mathf.Pi));
            float tone = Mathf.Sin(t * Mathf.Tau * freq) * env;
            float windNoise = (float)GD.RandRange(-0.35, 0.35) * env;

            float sample = tone * 0.5f + windNoise * 0.5f;
            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.85f);
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

    private static AudioStreamWav GeneratePickupBeep()
    {
        int sampleRate = 22050;
        float duration = 0.11f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env;
            float freq;
            if (progress < 0.45f)
            {
                env = Mathf.Sin((progress / 0.45f) * Mathf.Pi);
                freq = 1500f;
            }
            else if (progress < 0.55f)
            {
                env = 0f;
                freq = 0f;
            }
            else
            {
                env = Mathf.Sin(((progress - 0.55f) / 0.45f) * Mathf.Pi);
                freq = 2200f;
            }

            float sample = (freq > 0) ? (Mathf.Sin(t * Mathf.Tau * freq) * 0.7f + Mathf.Sin(t * Mathf.Tau * freq * 2f) * 0.3f) * env : 0f;
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

    private static AudioStreamWav GenerateFastballWhoosh()
    {
        int sampleRate = 22050;
        float duration = 0.20f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = Mathf.Sin(progress * Mathf.Pi);
            env = env * env;
            float freq = Mathf.Lerp(380f, 1400f, Mathf.Sin(progress * Mathf.Pi));
            float tone = Mathf.Sin(t * Mathf.Tau * freq) * env;
            float windNoise = (float)GD.RandRange(-0.45, 0.45) * env;

            float sample = tone * 0.45f + windNoise * 0.55f;
            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.95f);
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

    private static AudioStreamWav GenerateBatCrack()
    {
        int sampleRate = 22050;
        float duration = 0.18f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Sharp initial transient "CRACK"
            float popEnv = Mathf.Clamp(1.0f - (progress / 0.15f), 0f, 1f);
            popEnv = popEnv * popEnv * popEnv;
            float popTone = Mathf.Sin(t * Mathf.Tau * 1450f) * popEnv;
            float noisePop = (float)GD.RandRange(-0.6, 0.6) * popEnv;

            // Hollow wooden bat body resonance
            float woodEnv = Mathf.Clamp(1.0f - progress, 0f, 1f);
            woodEnv = woodEnv * woodEnv;
            float woodTone = (Mathf.Sin(t * Mathf.Tau * 420f) * 0.6f + Mathf.Sin(t * Mathf.Tau * 780f) * 0.4f) * woodEnv;

            float sample = (popTone * 0.5f + noisePop * 0.5f) * 0.7f + woodTone * 0.6f;
            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.95f);
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

    private static AudioStreamWav GenerateMetalClang()
    {
        int sampleRate = 22050;
        float duration = 0.25f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Sharp bright metal clang with ringing overtone
            float ringEnv = Mathf.Exp(-progress * 9.0f);
            float f1 = 1680f;
            float f2 = 2540f;
            float clang = (Mathf.Sin(t * Mathf.Tau * f1) * 0.6f + Mathf.Sin(t * Mathf.Tau * f2) * 0.4f) * ringEnv;

            // Initial strike tap
            float tapEnv = Mathf.Clamp(1.0f - (progress / 0.08f), 0f, 1f);
            float tap = (float)GD.RandRange(-0.4, 0.4) * tapEnv;

            float sample = clang * 0.75f + tap * 0.25f;
            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.95f);
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

    private static AudioStreamWav GenerateParryThud()
    {
        int sampleRate = 22050;
        float duration = 0.14f;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float env = Mathf.Clamp(1.0f - progress, 0f, 1f);
            env = env * env * env;
            float freq = Mathf.Lerp(450f, 120f, progress);
            float thud = Mathf.Sin(t * Mathf.Tau * freq) * env;
            float slap = (float)GD.RandRange(-0.3, 0.3) * Mathf.Clamp(1.0f - (progress / 0.2f), 0f, 1f);

            float sample = thud * 0.65f + slap * 0.35f;
            short pcm = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue * 0.9f);
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
