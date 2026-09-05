using Godot;

public partial class PlayerController : CharacterBody3D, IDamageable
{
    static public PlayerController Instance { get; private set; }
    [Export] private Node3D Head;
    [Export] private Camera3D Camera;
    [Export] private RayCast3D RayCast;
    [Export] private Node3D ItemHand;
    [Export] public Inventory Inventory;
    [Export] private InventoryBar InventoryBar;
    [Export] private CanvasLayer CrossHair;
    [Export] private HealthBar HealthBar;
    [Export] public Health Health;
    [Export] private CanvasLayer DeathOverlay;
    [Export] private Label3D NameCard;
    [Export] public float PickUpRange = 2.0f;
    [Export] private float ThrowVelocity = 50.0f;
    [Export] float WalkSpeed = 5.0f;
    [Export] float RunMultiplier = 1.5f;
    [Export] int StartingMoney = 100;
    public int Money { get; private set; }
    [Export] public Vector3 SyncPosition = Vector3.Zero;
    [Export] public Vector3 SyncHeadRotation = Vector3.Zero;
    [Export] public Vector3 SyncCameraRotation = Vector3.Zero;
    [Export] public string PlayerName = "";
    const float Accel = 30.0f;
    const float Friction = 25.0f;
    const float JumpVelocity = 4.5f;
    const float Sensitivity = 0.002f;
    const float Gravity = 9.8f;
    static readonly float MaxPitch = Mathf.DegToRad(85f);
    bool IsRunning = false;
    public Product HeldItem { get; private set; }
    IInteractable _highlightedItem;
    bool InputDisabled = false;

    public override void _Ready()
    {
        AddToGroup("Players");

        if (Head == null) Head = GetNode<Node3D>("Head");
        if (Camera == null) Camera = GetNode<Camera3D>("Camera");

        if (int.TryParse(Name, out int peerId))
        {
            SetMultiplayerAuthority(peerId);
            GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer")?.SetMultiplayerAuthority(peerId);
        }

        GD.Print($"[PlayerController] _Ready on node '{Name}' | Authority: {GetMultiplayerAuthority()} | MyUniqueId: {Multiplayer.GetUniqueId()} | IsLocalAuth: {IsMultiplayerAuthority()}");

        if (IsMultiplayerAuthority())
        {
            Instance = this;
            if (NameCard != null) NameCard.Visible = false;
            PlayerName = SteamManager.Instance?.GetPersonaName() ?? $"Player {Name}";
            if (NameCard != null) NameCard.Text = PlayerName;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            if (Camera != null)
            {
                Camera.MakeCurrent();
                GD.Print($"[PlayerController] Activated Camera for Local Authority Player '{Name}'");
            }
            Rpc(nameof(SyncPlayerName), SteamManager.Instance.GetPersonaName() ?? $"Player {Name}");
        }
        else
        {
            if (Camera != null) Camera.Current = false;

            // Delete UI elements on remote player clones so their UI never renders locally!
            foreach (Node child in GetChildren())
            {
                if (child is CanvasLayer canvasLayer)
                {
                    canvasLayer.QueueFree();
                }
            }
        }

        Money = StartingMoney;
    }

    public override void _EnterTree()
    {
        if (int.TryParse(Name, out int peerId))
        {
            SetMultiplayerAuthority(peerId);
            GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer")?.SetMultiplayerAuthority(peerId);
        }
    }

    public override void _Process(double delta)
    {
        if (IsMultiplayerAuthority())
        {
            if (Health == null) return;

            if (Health.IsDead)
            {
                if (!InputDisabled) InputDisabled = true;
                if (DeathOverlay != null && !DeathOverlay.Visible) DeathOverlay.Visible = true;

                if (Multiplayer.IsServer() && HeldItem != null)
                {
                    Inventory.DropLoot();
                    HeldItem = null;
                }
            }
        }
        else
        {
            if (NameCard != null && !string.IsNullOrEmpty(PlayerName) && NameCard.Text != PlayerName)
            {
                NameCard.Text = PlayerName;
            }
            // Smooth framerate-independent network interpolation for remote player clones
            if (SyncPosition != Vector3.Zero)
            {
                float distance = GlobalPosition.DistanceTo(SyncPosition);
                if (distance > 6.0f)
                {
                    GlobalPosition = SyncPosition;
                }
                else
                {
                    float lerpFactor = 1.0f - Mathf.Exp(-22.0f * (float)delta);
                    GlobalPosition = GlobalPosition.Lerp(SyncPosition, lerpFactor);
                }
            }

            float rotLerpFactor = 1.0f - Mathf.Exp(-22.0f * (float)delta);
            if (Head != null)
            {
                Head.Rotation = new Vector3(
                    Mathf.LerpAngle(Head.Rotation.X, SyncHeadRotation.X, rotLerpFactor),
                    Mathf.LerpAngle(Head.Rotation.Y, SyncHeadRotation.Y, rotLerpFactor),
                    Mathf.LerpAngle(Head.Rotation.Z, SyncHeadRotation.Z, rotLerpFactor)
                );
            }

            if (Camera != null)
            {
                Camera.Rotation = new Vector3(
                    Mathf.LerpAngle(Camera.Rotation.X, SyncCameraRotation.X, rotLerpFactor),
                    Mathf.LerpAngle(Camera.Rotation.Y, SyncCameraRotation.Y, rotLerpFactor),
                    Mathf.LerpAngle(Camera.Rotation.Z, SyncCameraRotation.Z, rotLerpFactor)
                );
            }
        }
    }


    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsMultiplayerAuthority()) return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Visible) GetTree().Quit();
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            if (InputDisabled) return;
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
        if (!IsMultiplayerAuthority()) return;
        if (InputDisabled) return;
        HandleThrow();
        UpdateTargeting();
        HandleInteract();
        HandleInventoryActions();
        HandleMovement(delta);

        SyncPosition = GlobalPosition;
        SyncHeadRotation = Head != null ? Head.Rotation : Vector3.Zero;
        SyncCameraRotation = Camera != null ? Camera.Rotation : Vector3.Zero;
    }

    private void UpdateTargeting()
    {
        RayCast.ForceRaycastUpdate();
        if (RayCast.GetCollider() is IInteractable interactable)
        {
            if (interactable is Product p && p == HeldItem) return;

            if (interactable != _highlightedItem)
            {
                _highlightedItem?.OutlineOff();
                _highlightedItem = interactable;
                _highlightedItem.OutlineOn();
            }
        }
        else if (_highlightedItem != null)
        {
            _highlightedItem.OutlineOff();
            _highlightedItem = null;
        }
    }

    private void HandleInteract()
    {
        if (InputDisabled) return;
        if (!Input.IsActionJustPressed("interact")) return;

        if (RayCast.GetCollider() is IInteractable interactable)
        {
            interactable.Interact(this);
        }
        else if (HeldItem != null)
        {
            if (GameManager.Instance?.CurrentPhase == GamePhase.Shopping) HeldItem.IsForSale = true;
            Rpc(nameof(RPCDropItem), HeldItem.GetPath());
            return;

        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RPCPickupItem(NodePath nodePath)
    {
        Product item = GetNodeOrNull<Product>(nodePath);
        if (item == null) return;

        // Hide any previously held items on all clients
        if (ItemHand != null)
        {
            foreach (Node child in ItemHand.GetChildren())
            {
                if (child is Product p) p.Visible = false;
            }
        }

        // Parent new item to hand and make only it visible
        item.CollisionLayer = 0;
        item.CollisionMask = 0;
        item.Freeze = true;
        item.Reparent(ItemHand);
        item.Position = Vector3.Zero;
        item.Visible = true;

        if (IsMultiplayerAuthority())
        {
            HeldItem = item;
            Inventory.AddItem(HeldItem);
            Rpc(nameof(RpcSyncActiveHeldItem), HeldItem.GetPath());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RPCDropItem(NodePath nodePath)
    {
        Product item = GetNodeOrNull<Product>(nodePath);
        if (item == null) return;
        item.Reparent(GetTree().CurrentScene);
        item.CollisionLayer = 1;
        item.CollisionMask = 3;
        item.Freeze = false;
        item.Visible = true;

        if (IsMultiplayerAuthority())
        {
            HeldItem = null;
            Inventory.RemoveCurrentSelectedItem();
            Rpc(nameof(RpcSyncActiveHeldItem), new NodePath());
        }
    }



    private void HandleMovement(double delta)
    {
        if (InputDisabled) return;
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
        if (Input.IsActionJustPressed("fire") && HeldItem != null && GameManager.Instance?.CurrentPhase == GamePhase.BattleRoyale)
        {
            Vector3 camForward = -Camera.GlobalBasis.Z;
            Vector3 aimPoint = Camera.GlobalPosition + camForward * 10.0f;
            Vector3 dir = (aimPoint - HeldItem.GlobalPosition).Normalized();
            float speed = ThrowVelocity * HeldItem.ThrowMultiplier;
            NodePath itemPath = HeldItem.GetPath();
            Rpc(nameof(RpcThrowItem), itemPath, dir * speed);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcThrowItem(NodePath itemPath, Vector3 launchVelocity)
    {
        Product item = GetNodeOrNull<Product>(itemPath);
        if (item == null) return;

        item.Thrower = this;
        item.Reparent(GetTree().CurrentScene);
        item.GlobalPosition = Camera.GlobalPosition + (-Camera.GlobalBasis.Z * 0.5f);
        item.CollisionLayer = 1;
        item.CollisionMask = 3;
        item.Freeze = false;
        item.Visible = true;
        item.LinearVelocity = launchVelocity;

        if (IsMultiplayerAuthority())
        {
            HeldItem = null;
            Inventory.RemoveCurrentSelectedItem();
            Rpc(nameof(RpcSyncActiveHeldItem), new NodePath());
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
        if (HeldItem != null) HeldItem.Visible = false;
        HeldItem = Inventory.GetItem(index);
        Inventory.SetCurrentSelectedItem(index);
        if (HeldItem != null)
        {
            HeldItem.Visible = true;
        }

        NodePath activeItemPath = HeldItem != null ? HeldItem.GetPath() : new NodePath();
        Rpc(nameof(RpcSyncActiveHeldItem), activeItemPath);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcSyncActiveHeldItem(NodePath activeItemPath)
    {
        if (ItemHand == null) return;
        foreach (Node child in ItemHand.GetChildren())
        {
            if (child is Product p)
            {
                p.Visible = !activeItemPath.IsEmpty && p.GetPath() == activeItemPath;
            }
        }
    }

    public bool TryDeductMoney(int amount)
    {
        if (Money < amount) return false;
        Money -= amount;
        GD.Print($"[Store] Purchased item for ${amount}. Money remaining: ${Money}");
        return true;
    }

    public void TakeDamage(int amount, Node3D source = null)
    {
        Health?.TakeDamage(amount);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncPlayerName(string name)
    {
        if (NameCard == null) return;
        NameCard.Text = name;
    }
}
