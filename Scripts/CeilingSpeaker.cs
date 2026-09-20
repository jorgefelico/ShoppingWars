using Godot;

public partial class CeilingSpeaker : Node3D
{
    [Export] public AudioStreamPlayer3D AudioPlayer;
    [Export] public AudioStreamPlayer3D SFXPlayer;
    [Export] public AudioStreamPlayer3D VoicePlayer;
    [Export] public MeshInstance3D BroadcastLed;

    private StandardMaterial3D _ledMaterial;
    private static readonly Color LedIdleColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    private static readonly Color LedActiveColor = new Color(0.2f, 1.0f, 0.35f, 1f);

    public override void _Ready()
    {
        if (AudioPlayer == null)
        {
            AudioPlayer = GetNodeOrNull<AudioStreamPlayer3D>("AudioPlayer");
        }
        if (SFXPlayer == null)
        {
            SFXPlayer = GetNodeOrNull<AudioStreamPlayer3D>("SFXPlayer");
        }
        if (VoicePlayer == null)
        {
            VoicePlayer = GetNodeOrNull<AudioStreamPlayer3D>("VoicePlayer");
        }
        if (BroadcastLed == null)
        {
            BroadcastLed = GetNodeOrNull<MeshInstance3D>("BroadcastLed");
        }

        if (BroadcastLed != null)
        {
            _ledMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = LedIdleColor
            };
            BroadcastLed.MaterialOverride = _ledMaterial;
        }
    }

    public override void _Process(double delta)
    {
        if (_ledMaterial == null) return;

        bool isActive = (VoicePlayer != null && VoicePlayer.Playing) || (SFXPlayer != null && SFXPlayer.Playing);
        _ledMaterial.AlbedoColor = isActive ? LedActiveColor : LedIdleColor;
    }
}
