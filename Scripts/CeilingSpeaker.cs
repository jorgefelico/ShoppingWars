using Godot;

public partial class CeilingSpeaker : Node3D
{
    [Export] public AudioStreamPlayer3D AudioPlayer;
    [Export] public AudioStreamPlayer3D SFXPlayer;

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
    }
}
