using Godot;

public partial class PlayerController : CharacterBody3D
{
    [Export]
    private Node3D Head;
    [Export]
    private Camera3D Camera;
    const float Speed = 5.0f;
    const float Accel = 30.0f;
    const float Friction = 25.0f;
    const float JumpVelocity = 4.5f;
    const float Sensitivity = 0.002f;
    const float Gravity = 9.8f;
    static readonly float MaxPitch = Mathf.DegToRad(85f);

    public override void _Ready()
    {
        if(Head == null)
        {
            Head = GetNode<Node3D>("Head");
        }
        if(Camera == null)
        {
            Camera = GetNode<Camera3D>("Camera");
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if(@event.IsActionPressed("ui_cancel")) {
            if(Input.MouseMode == Input.MouseModeEnum.Visible) GetTree().Quit();
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if(@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            if(motion.Relative.Length() > 500f) return;
            
            Head.RotateY(-motion.Relative.X * Sensitivity);
            float pitch = Camera.Rotation.X - motion.Relative.Y * Sensitivity;
            pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            Camera.Rotation = new Vector3(pitch, Camera.Rotation.Y, Camera.Rotation.Z);
        }
    }

}
