using Godot;

public partial class PlayerController : CharacterBody3D
{
    [Export] private Node3D Head;
    [Export] private Camera3D Camera;
    [Export] private RayCast3D RayCast;
    [Export] private Node3D ItemHand;
    [Export] private float PickUpRange = 2.0f;
    const float Speed = 5.0f;
    const float Accel = 30.0f;
    const float Friction = 25.0f;
    const float JumpVelocity = 4.5f;
    const float Sensitivity = 0.002f;
    const float Gravity = 9.8f;
    static readonly float MaxPitch = Mathf.DegToRad(85f);

    Product _heldItem;
    Product _highlightedItem;

    public override void _Ready()
    {
        if (Head == null)
        {
            Head = GetNode<Node3D>("Head");
        }
        if (Camera == null)
        {
            Camera = GetNode<Camera3D>("Camera");
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Visible) GetTree().Quit();
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            if (motion.Relative.Length() > 500f) return;
            Head.RotateY(-motion.Relative.X * Sensitivity);
            float pitch = Camera.Rotation.X - motion.Relative.Y * Sensitivity;
            pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            Camera.Rotation = new Vector3(pitch, Camera.Rotation.Y, Camera.Rotation.Z);
        }

        if (@event is InputEventMouseButton)
        {
            if (Input.MouseMode == Input.MouseModeEnum.Visible)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }

        if (Input.IsActionJustPressed("interact") && _heldItem != null)
        {
            _heldItem.Dropped();
            _heldItem = null;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Raycasting Stuff
        RayCast.ForceRaycastUpdate();

        Product newHighlight = null;
        if (RayCast.GetCollider() is Product product && product != _heldItem)
        {
            newHighlight = product;

            // Pick Up
            if (Input.IsActionJustPressed("interact") && _heldItem == null)
            {
                float distance = product.GlobalPosition.DistanceTo(GlobalPosition);
                if (distance <= PickUpRange)
                {
                    _heldItem = product;
                    _heldItem.PickedUp();
                    _heldItem.GetParent()?.RemoveChild(_heldItem);
                    ItemHand.AddChild(_heldItem);
                    _heldItem.Position = Vector3.Zero;
                }
            }
        }

        if (newHighlight != _highlightedItem)
        {
            _highlightedItem?.OutlineOff();
            _highlightedItem = newHighlight;
            _highlightedItem?.OutlineOn();
        }
        // Movement
        if (IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, -Gravity, Velocity.Z);
        }
        else
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * (float)delta, Velocity.Z);
        }

        if (Input.IsActionJustPressed("jump") && IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
        }

        Vector2 movementAxis = Input.GetVector("move_left", "move_right", "move_back", "move_forward");
        Vector3 direction = new Vector3(movementAxis.X, 0, -movementAxis.Y);
        Vector3 worldDir = Head.GlobalBasis * direction;
        Vector3 target = worldDir * Speed;
        float newX = Mathf.MoveToward(Velocity.X, target.X, Accel * (float)delta);
        float newZ = Mathf.MoveToward(Velocity.Z, target.Z, Accel * (float)delta);
        if (movementAxis != Vector2.Zero)
        {
            Velocity = new Vector3(newX, Velocity.Y, newZ);
        }
        else
        {
            newX = Mathf.MoveToward(Velocity.X, 0, Friction * (float)delta);
            newZ = Mathf.MoveToward(Velocity.Z, 0, Friction * (float)delta);
            Velocity = new Vector3(newX, Velocity.Y, newZ);
        }
        MoveAndSlide();
    }
}
