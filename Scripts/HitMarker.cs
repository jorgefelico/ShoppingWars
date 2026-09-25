using Godot;
using System;

public partial class HitMarker : Control
{
    private float _alpha = 0f;
    private Color _drawColor = Colors.White;
    private AudioStreamPlayer _audioPlayer;
    private AudioStreamWav _hitSound;
    private AudioStreamWav _killSound;

    public override void _Ready()
    {
        // Center on screen
        AnchorLeft = 0.5f;
        AnchorTop = 0.5f;
        AnchorRight = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -25f;
        OffsetTop = -25f;
        OffsetRight = 25f;
        OffsetBottom = 25f;
        MouseFilter = MouseFilterEnum.Ignore;

        _hitSound = CreateProceduralTick(frequency: 1150f, duration: 0.045f, isKill: false);
        _killSound = CreateProceduralTick(frequency: 520f, duration: 0.09f, isKill: true);

        _audioPlayer = new AudioStreamPlayer();
        _audioPlayer.Bus = "Master";
        _audioPlayer.VolumeDb = -2.0f;
        AddChild(_audioPlayer);
    }

    public override void _Process(double delta)
    {
        if (_alpha > 0f)
        {
            _alpha = Mathf.Max(0f, _alpha - (float)delta * 5.0f);
            QueueRedraw();
        }
    }

    public void Flash(bool isKill = false)
    {
        _alpha = 1.0f;
        _drawColor = isKill ? new Color(1.0f, 0.15f, 0.15f, 1.0f) : Colors.White;

        if (_audioPlayer != null)
        {
            _audioPlayer.Stream = isKill ? _killSound : _hitSound;
            _audioPlayer.PitchScale = (float)GD.RandRange(0.96, 1.04);
            _audioPlayer.Play();
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_alpha <= 0.001f) return;

        Color c = new Color(_drawColor.R, _drawColor.G, _drawColor.B, _alpha);
        Vector2 center = Size * 0.5f;

        float innerDist = 4.5f;
        float lineLength = 5.0f;
        float lineWidth = 1.8f;

        // 4 diagonal tick lines radiating outward from crosshair center
        DrawLine(center + new Vector2(-innerDist, -innerDist), center + new Vector2(-innerDist - lineLength, -innerDist - lineLength), c, lineWidth, true);
        DrawLine(center + new Vector2(innerDist, -innerDist), center + new Vector2(innerDist + lineLength, -innerDist - lineLength), c, lineWidth, true);
        DrawLine(center + new Vector2(-innerDist, innerDist), center + new Vector2(-innerDist - lineLength, innerDist + lineLength), c, lineWidth, true);
        DrawLine(center + new Vector2(innerDist, innerDist), center + new Vector2(innerDist + lineLength, innerDist + lineLength), c, lineWidth, true);
    }

    private AudioStreamWav CreateProceduralTick(float frequency, float duration, bool isKill)
    {
        int sampleRate = 22050;
        int numSamples = (int)(sampleRate * duration);
        byte[] data = new byte[numSamples * 2];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            // Snappy exponential-like decay envelope
            float env = Mathf.Clamp(1.0f - progress, 0f, 1f);
            env = env * env;

            float f = frequency;
            if (isKill)
            {
                // Descending crunch pitch for kill
                f = Mathf.Lerp(frequency, frequency * 0.6f, progress);
            }

            float sample = Mathf.Sin(t * Mathf.Tau * f) * env * 0.45f;
            short pcm = (short)(sample * short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav();
        wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
        wav.MixRate = sampleRate;
        wav.Data = data;
        return wav;
    }
}
