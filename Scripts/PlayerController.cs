using Godot;

public partial class PlayerController : CharacterBody3D
{
    [Export] private Node3D Head;
    [Export] private Camera3D Camera;
    [Export] private RayCast3D RayCast;
    [Export] private Node3D ItemHand;
    [Export] private Inventory Inventory;
    [Export] public Health Health;
    [Export] private CanvasLayer DeathOverlay;
    [Export] private float PickUpRange = 2.0f;
    [Export] private float ThrowVelocity = 50.0f;
    [Export] float WalkSpeed = 5.0f;
    [Export] float RunMultiplier = 1.5f;
    const float Accel = 30.0f;
    const float Friction = 25.0f;
    const float JumpVelocity = 4.5f;
    const float Sensitivity = 0.002f;
    const float Gravity = 9.8f;
    static readonly float MaxPitch = Mathf.DegToRad(85f);
    bool IsRunning = false;
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

    public override void _Process(double delta)
    {
        if(Health.IsDead && DeathOverlay.Visible == false)
        {
            DeathOverlay.Visible = true;
            Inventory.DropLoot();
        }
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
    }

    public override void _PhysicsProcess(double delta)
    {
        HandleThrow();
        UpdateTargeting();
        HandleInteract();
        HandleInventoryActions();
        HandleMovement(delta);
    }

    private void UpdateTargeting()
    {
        RayCast.ForceRaycastUpdate();
        Product newHighlight = RayCast.GetCollider() is Product product && product != _heldItem ? product : null;

        if (newHighlight != _highlightedItem)
        {
            _highlightedItem?.OutlineOff();
            _highlightedItem = newHighlight;
            _highlightedItem?.OutlineOn();
        }
    }

    private void HandleInteract()
    {
        if (!Input.IsActionJustPressed("interact")) return;

        if (RayCast.GetCollider() is Product product && product.GlobalPosition.DistanceTo(GlobalPosition) <= PickUpRange)
        {
            if (Inventory.IsInventoryFull()) return;
            if (_heldItem != null)
            {
                _heldItem.Visible = false;
            }

            // Get New Item
            _heldItem = product;
            _heldItem.CollisionLayer = 0;
            _heldItem.CollisionMask = 0;
            _heldItem.Freeze = true;
            _heldItem.Reparent(ItemHand);
            _heldItem.Position = Vector3.Zero;
            Inventory.AddItem(_heldItem);
        }
        else if (_heldItem != null)
        {
            _heldItem.Reparent(GetTree().CurrentScene);
            _heldItem.CollisionLayer = 1;
            _heldItem.CollisionMask = 3;
            _heldItem.Freeze = false;
            _heldItem = null;
            Inventory.RemoveCurrentSelectedItem();
            return;
        }
    }

    private void HandleMovement(double delta)
    {
        if (Input.IsActionPressed("sprint"))
        {
            IsRunning = true;
        }
        else
        {
            IsRunning = false;
        }

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
        Vector3 target = worldDir * WalkSpeed * (IsRunning ? RunMultiplier : 1);
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

    private void HandleThrow()
    {
        // Throwing
        if (Input.IsActionJustPressed("fire") && _heldItem != null)
        {
            Vector3 camForward = -Camera.GlobalBasis.Z;
            Vector3 aimPoint = Camera.GlobalPosition + camForward * 10.0f;
            Vector3 dir = (aimPoint - _heldItem.GlobalPosition).Normalized();
            _heldItem.Reparent(GetTree().CurrentScene);
            _heldItem.CollisionLayer = 1;
            _heldItem.CollisionMask = 3;
            _heldItem.Freeze = false;
            _heldItem.LinearVelocity = dir * ThrowVelocity;
            _heldItem = null;
            Inventory.RemoveCurrentSelectedItem();
        }
    }

    private void HandleInventoryActions()
    {
        if (Input.IsActionJustPressed("slot1"))
        {
            SwitchInventorySlot(0);
        }
        if (Input.IsActionJustPressed("slot2"))
        {
            SwitchInventorySlot(1);
        }
        if (Input.IsActionJustPressed("slot3"))
        {
            SwitchInventorySlot(2);
        }
        if (Input.IsActionJustPressed("slot4"))
        {
            SwitchInventorySlot(3);
        }
        if (Input.IsActionJustPressed("slot5"))
        {
            SwitchInventorySlot(4);
        }
        if (Input.IsActionJustPressed("scroll_up"))
        {
            SwitchInventorySlot(Inventory.SelectNextItem());
        }
        if (Input.IsActionJustPressed("scroll_down"))
        {
            SwitchInventorySlot(Inventory.SelectPreviousItem());
        }
    }

    private void SwitchInventorySlot(int index)
    {
        if (_heldItem != null) _heldItem.Visible = false;
        _heldItem = Inventory.GetItem(index);
        Inventory.SetCurrentSelectedItem(index);
        if (_heldItem == null) return;
        _heldItem.Visible = true;
    }
}
