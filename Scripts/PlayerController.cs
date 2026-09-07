using Godot;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody3D, IDamageable
{
    static public PlayerController Instance { get; private set; }
    [Export] public Node3D Head { get; private set; }
    [Export] public Camera3D Camera { get; private set; }
    [Export] private RayCast3D RayCast;
    [Export] private Node3D ItemHand;
    [Export] public Inventory Inventory;
    [Export] private InventoryBar InventoryBar;
    [Export] private CanvasLayer CrossHair;
    [Export] private HealthBar HealthBar;
    [Export] public Health Health;
    [Export] private CanvasLayer DeathOverlay;
    [Export] private Label3D NameCard;
    [Export] public SpotLight3D Flashlight;
    [Export] public MeshInstance3D MeshInstance;
    [Export] public float PickUpRange = 2.0f;
    [Export] private float ThrowVelocity = 50.0f;
    [Export] float WalkSpeed = 5.0f;
    [Export] float RunMultiplier = 1.5f;
    public float SpeedModifier { get; set; } = 1.0f;
    public float GravityModifier { get; set; } = 1.0f;
    public float JumpModifier { get; set; } = 1.0f;
    [Export] int StartingMoney = 100;
    public int Money { get; private set; }
    [Export] public Vector3 SyncPosition = Vector3.Zero;
    [Export] public Vector3 SyncHeadRotation = Vector3.Zero;
    [Export] public Vector3 SyncCameraRotation = Vector3.Zero;
    private string _playerName = "";
    [Export]
    public string PlayerName
    {
        get => string.IsNullOrEmpty(_playerName) ? $"Player {Name}" : _playerName;
        set
        {
            _playerName = value;
            if (NameCard == null) NameCard = GetNodeOrNull<Label3D>("NameCard");
            if (NameCard != null && !string.IsNullOrEmpty(value))
            {
                NameCard.Text = value;
            }
        }
    }
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
    public bool IsSpectating { get; private set; } = false;
    private PlayerController _currentSpectatedPlayer;
    private int _spectatedIndex = 0;
    private bool _isBeingSpectated = false;

    public override void _Ready()
    {
        AddToGroup("Players");

        if (Head == null) Head = GetNode<Node3D>("Head");
        if (Camera == null) Camera = GetNode<Camera3D>("Head/Camera");
        if (MeshInstance == null) MeshInstance = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (Health == null) Health = GetNodeOrNull<Health>("Health");
        if (Inventory == null) Inventory = GetNodeOrNull<Inventory>("Inventory");

        if (int.TryParse(Name, out int peerId))
        {
            SetMultiplayerAuthority(peerId);
        }

        SyncPosition = GlobalPosition;
        SyncHeadRotation = Head != null ? Head.Rotation : Vector3.Zero;
        SyncCameraRotation = Camera != null ? Camera.Rotation : Vector3.Zero;

        GD.Print($"[PlayerController] _Ready on node '{Name}' | Authority: {GetMultiplayerAuthority()} | MyUniqueId: {Multiplayer.GetUniqueId()} | IsLocalAuth: {IsMultiplayerAuthority()}");

        if (IsMultiplayerAuthority())
        {
            Instance = this;
            GetNodeOrNull<AudioListener3D>("Head/AudioListener3D")?.MakeCurrent();

            if (NameCard != null) NameCard.Visible = false;
            if (string.IsNullOrEmpty(PlayerName))
            {
                PlayerName = SteamManager.Instance?.GetPersonaName() ?? $"Player {Name}";
            }
            Input.MouseMode = Input.MouseModeEnum.Captured;
            if (Camera != null)
            {
                Camera.MakeCurrent();
                GD.Print($"[PlayerController] Activated Camera for Local Authority Player '{Name}'");
            }
            Rpc(nameof(SyncPlayerName), PlayerName);

            if (DeathOverlay is SpectatorHUD specHud)
            {
                specHud.PreviousRequested += SpectatePreviousPlayer;
                specHud.NextRequested += SpectateNextPlayer;
            }
        }
        else
        {
            if (Camera != null)
            {
                Camera.Current = false;
            }

            if (NameCard != null)
            {
                NameCard.Visible = true;
                if (!string.IsNullOrEmpty(PlayerName))
                {
                    NameCard.Text = PlayerName;
                }
            }

            // Delete UI elements on remote player clones so their UI never renders locally!
            foreach (Node child in GetChildren())
            {
                if (child is CanvasLayer canvasLayer)
                {
                    canvasLayer.QueueFree();
                }
            }
        }

        if (Flashlight == null) Flashlight = GetNodeOrNull<SpotLight3D>("Head/Camera/Flashlight");
        if (Flashlight != null) SetFlashlight(false);
        if (Health != null)
        {
            Health.Died += OnPlayerDied;
        }

        Money = StartingMoney;
    }

    public override void _ExitTree()
    {
        if (Health != null)
        {
            Health.Died -= OnPlayerDied;
        }
        if (DeathOverlay is SpectatorHUD specHud)
        {
            specHud.PreviousRequested -= SpectatePreviousPlayer;
            specHud.NextRequested -= SpectateNextPlayer;
        }
        if (_currentSpectatedPlayer != null && GodotObject.IsInstanceValid(_currentSpectatedPlayer))
        {
            _currentSpectatedPlayer.SetSpectateTargetActive(false);
        }
    }

    public override void _EnterTree()
    {
        if (int.TryParse(Name, out int peerId))
        {
            SetMultiplayerAuthority(peerId);
        }
    }

    public override void _Process(double delta)
    {
        if (IsMultiplayerAuthority())
        {
            if (Health == null) return;

            if (Health.IsDead)
            {
                if (!IsSpectating)
                {
                    StartSpectating();
                }
                UpdateSpectatingState();
                return;
            }
        }
        else
        {
            if (Health != null && Health.IsDead)
            {
                if (MeshInstance != null && MeshInstance.Visible) MeshInstance.Visible = false;
                if (NameCard != null && NameCard.Visible) NameCard.Visible = false;
            }
            else if (!_isBeingSpectated)
            {
                if (NameCard != null)
                {
                    if (!NameCard.Visible) NameCard.Visible = true;
                    if (!string.IsNullOrEmpty(PlayerName) && NameCard.Text != PlayerName)
                    {
                        NameCard.Text = PlayerName;
                    }
                }
                if (MeshInstance != null && !MeshInstance.Visible)
                {
                    MeshInstance.Visible = true;
                }
            }

            // Smooth framerate-independent network interpolation for remote player clones
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

        if (GameManager.Instance?.CurrentPhase == GamePhase.GameOver)
        {
            if (Input.MouseMode != Input.MouseModeEnum.Visible)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            if (@event.IsActionPressed("ui_cancel"))
            {
                GetTree().Quit();
            }
            return;
        }

        if (IsSpectating)
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                if (Input.MouseMode == Input.MouseModeEnum.Visible) GetTree().Quit();
                Input.MouseMode = Input.MouseModeEnum.Visible;
                return;
            }

            if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    SpectateNextPlayer();
                }
                else if (mouseButton.ButtonIndex == MouseButton.Right)
                {
                    SpectatePreviousPlayer();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    SpectatePreviousPlayer();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    SpectateNextPlayer();
                }
                return;
            }

            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                if (keyEvent.Keycode == Key.A || keyEvent.Keycode == Key.Left)
                {
                    SpectatePreviousPlayer();
                    return;
                }
                if (keyEvent.Keycode == Key.D || keyEvent.Keycode == Key.Right || keyEvent.Keycode == Key.Space)
                {
                    SpectateNextPlayer();
                    return;
                }
            }

            return;
        }

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

        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F && !key.Echo)
        {
            ToggleFlashlight();
        }
    }

    public static void SetGlobalFlashlight(bool enabled)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            foreach (Node node in tree.GetNodesInGroup("Players"))
            {
                if (node is PlayerController pc && pc.IsMultiplayerAuthority())
                {
                    pc.SetFlashlight(enabled);
                }
            }
        }
    }

    public void ToggleFlashlight()
    {
        if (Flashlight == null) return;
        SetFlashlight(!Flashlight.Visible);
    }

    public void SetFlashlight(bool enabled)
    {
        if (Flashlight == null) return;
        Flashlight.Visible = enabled;
        if (Multiplayer.HasMultiplayerPeer() && IsMultiplayerAuthority())
        {
            Rpc(nameof(RpcSyncFlashlight), enabled);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcSyncFlashlight(bool visible)
    {
        if (Flashlight != null) Flashlight.Visible = visible;

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(RpcSyncFlashlight), visible);
                    }
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority()) return;
        if (InputDisabled || GameManager.Instance?.CurrentPhase == GamePhase.GameOver) return;
        HandleThrow();
        UpdateTargeting();
        HandleInteract();
        HandleDropItem();
        HandleInventoryActions();
        HandleMovement(delta);

        SyncPosition = GlobalPosition;
        SyncHeadRotation = Head != null ? Head.Rotation : Vector3.Zero;
        SyncCameraRotation = Camera != null ? Camera.Rotation : Vector3.Zero;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncTransform), SyncPosition, SyncHeadRotation, SyncCameraRotation);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void RpcSyncTransform(Vector3 position, Vector3 headRotation, Vector3 cameraRotation)
    {
        SyncPosition = position;
        SyncHeadRotation = headRotation;
        SyncCameraRotation = cameraRotation;

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(RpcSyncTransform), position, headRotation, cameraRotation);
                    }
                }
            }
        }
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
    }

    private void HandleDropItem()
    {
        if (InputDisabled) return;
        if (!Input.IsActionJustPressed("drop_item")) return;

        if (HeldItem != null)
        {
            if (GameManager.Instance?.CurrentPhase == GamePhase.Shopping) HeldItem.IsForSale = true;
            Rpc(nameof(RPCDropItem), HeldItem.GetPath());
        }
    }

    public void UpdateHandItemVisibility()
    {
        if (ItemHand == null) return;
        HeldItem = Inventory != null ? Inventory.GetItem(Inventory.selectedItemIndex) : null;
        foreach (Node child in ItemHand.GetChildren())
        {
            if (child is Product p)
            {
                p.Visible = (HeldItem != null && p == HeldItem);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RPCPickupItem(NodePath nodePath)
    {
        Product item = GetNodeOrNull<Product>(nodePath);
        if (item == null) return;

        // Parent new item to hand and disable physics
        item.CollisionLayer = 0;
        item.CollisionMask = 0;
        item.Freeze = true;
        item.Reparent(ItemHand);
        item.Position = Vector3.Zero;
        item.Visible = false;

        if (IsMultiplayerAuthority())
        {
            Inventory.AddItem(item);
            UpdateHandItemVisibility();

            NodePath activePath = HeldItem != null ? HeldItem.GetPath() : new NodePath();
            Rpc(nameof(RpcSyncActiveHeldItem), activePath);
        }

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(RPCPickupItem), nodePath);
                    }
                }
            }
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
            Inventory.RemoveItem(item);
            UpdateHandItemVisibility();

            NodePath activePath = HeldItem != null ? HeldItem.GetPath() : new NodePath();
            Rpc(nameof(RpcSyncActiveHeldItem), activePath);
        }

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(RPCDropItem), nodePath);
                    }
                }
            }
        }
    }



    public static void SetGlobalSpeedModifier(float modifier)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            foreach (Node node in tree.GetNodesInGroup("Players"))
            {
                if (node is PlayerController pc)
                {
                    pc.SpeedModifier = modifier;
                }
            }
        }
    }

    public static void SetGlobalGravityModifier(float gravityMod, float jumpMod = 1.0f)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            foreach (Node node in tree.GetNodesInGroup("Players"))
            {
                if (node is PlayerController pc)
                {
                    pc.GravityModifier = gravityMod;
                    pc.JumpModifier = jumpMod;
                }
            }
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

        float effectiveGravity = Gravity * GravityModifier;
        if (IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, -effectiveGravity, Velocity.Z);
        }
        else
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - effectiveGravity * (float)delta, Velocity.Z);
        }

        if (Input.IsActionJustPressed("jump") && IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, JumpVelocity * JumpModifier, Velocity.Z);
        }

        Vector2 movementAxis = Input.GetVector("move_left", "move_right", "move_back", "move_forward");
        Vector3 direction = new Vector3(movementAxis.X, 0, -movementAxis.Y);
        Vector3 worldDir = Head.GlobalBasis * direction;
        Vector3 target = worldDir * WalkSpeed * SpeedModifier * (IsRunning ? RunMultiplier : 1);
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
            Inventory.RemoveItem(item);
            UpdateHandItemVisibility();

            NodePath activePath = HeldItem != null ? HeldItem.GetPath() : new NodePath();
            Rpc(nameof(RpcSyncActiveHeldItem), activePath);
        }

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(RpcThrowItem), itemPath, launchVelocity);
                    }
                }
            }
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
        Inventory.SetCurrentSelectedItem(index);
        UpdateHandItemVisibility();

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

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(RpcSyncActiveHeldItem), activeItemPath);
                    }
                }
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
        if (!Multiplayer.IsServer())
        {
            if (Multiplayer.HasMultiplayerPeer())
            {
                RpcId(1, nameof(RpcRequestDamage), amount, source != null ? source.GetPath() : new NodePath());
            }
            return;
        }

        if (Health == null) Health = GetNodeOrNull<Health>("Health");
        Health?.TakeDamage(amount);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcRequestDamage(int amount, NodePath sourcePath)
    {
        if (!Multiplayer.IsServer()) return;
        Node3D source = !sourcePath.IsEmpty ? GetNodeOrNull<Node3D>(sourcePath) : null;
        TakeDamage(amount, source);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void SyncPlayerName(string name)
    {
        PlayerName = name;
        if (NameCard != null)
        {
            NameCard.Text = name;
        }

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(SyncPlayerName), name);
                    }
                }
            }
        }
    }

    public void SetSpectateTargetActive(bool isBeingSpectated)
    {
        _isBeingSpectated = isBeingSpectated;
        if (MeshInstance != null)
        {
            MeshInstance.Visible = !isBeingSpectated && (Health == null || !Health.IsDead);
        }
        if (NameCard != null)
        {
            NameCard.Visible = !isBeingSpectated && (Health == null || !Health.IsDead);
        }
    }

    public List<PlayerController> GetLivingPlayers()
    {
        var list = new List<PlayerController>();
        foreach (Node node in GetTree().GetNodesInGroup("Players"))
        {
            if (node is PlayerController pc && GodotObject.IsInstanceValid(pc) && pc.IsInsideTree() && pc != this)
            {
                Health health = pc.Health ?? pc.GetNodeOrNull<Health>("Health");
                if (health != null && !health.IsDead && health.CurrentHealth > 0)
                {
                    list.Add(pc);
                }
            }
        }
        return list;
    }

    public void StartSpectating()
    {
        IsSpectating = true;
        InputDisabled = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        _highlightedItem?.OutlineOff();
        _highlightedItem = null;

        if (CrossHair != null) CrossHair.Visible = false;
        if (InventoryBar != null) InventoryBar.Visible = false;
        if (HealthBar != null) HealthBar.Visible = false;
        if (DeathOverlay != null) DeathOverlay.Visible = true;
        SetFlashlight(false);

        CollisionLayer = 0;
        CollisionMask = 0;
        GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.SetDeferred("disabled", true);
        if (MeshInstance != null) MeshInstance.Visible = false;
        if (NameCard != null) NameCard.Visible = false;

        var livingPlayers = GetLivingPlayers();
        if (livingPlayers.Count > 0)
        {
            _spectatedIndex = 0;
            SpectatePlayer(livingPlayers[_spectatedIndex]);
        }
        else
        {
            SpectatePlayer(null);
        }
    }

    public void SpectatePlayer(PlayerController target)
    {
        if (_currentSpectatedPlayer != null && GodotObject.IsInstanceValid(_currentSpectatedPlayer))
        {
            _currentSpectatedPlayer.SetSpectateTargetActive(false);
        }

        _currentSpectatedPlayer = target;
        var specHud = DeathOverlay as SpectatorHUD;

        if (_currentSpectatedPlayer != null && GodotObject.IsInstanceValid(_currentSpectatedPlayer))
        {
            _currentSpectatedPlayer.SetSpectateTargetActive(true);
            _currentSpectatedPlayer.Camera?.MakeCurrent();
            _currentSpectatedPlayer.GetNodeOrNull<AudioListener3D>("Head/AudioListener3D")?.MakeCurrent();
            specHud?.SetSpectating(_currentSpectatedPlayer.PlayerName);
        }
        else
        {
            if (Camera != null)
            {
                Camera.MakeCurrent();
            }
            GetNodeOrNull<AudioListener3D>("Head/AudioListener3D")?.MakeCurrent();
            specHud?.SetNoLivingPlayers();
        }
    }

    public void SpectateNextPlayer()
    {
        if (!IsSpectating) return;
        var livingPlayers = GetLivingPlayers();
        if (livingPlayers.Count == 0)
        {
            SpectatePlayer(null);
            return;
        }

        _spectatedIndex = (_spectatedIndex + 1) % livingPlayers.Count;
        SpectatePlayer(livingPlayers[_spectatedIndex]);
    }

    public void SpectatePreviousPlayer()
    {
        if (!IsSpectating) return;
        var livingPlayers = GetLivingPlayers();
        if (livingPlayers.Count == 0)
        {
            SpectatePlayer(null);
            return;
        }

        _spectatedIndex = (_spectatedIndex - 1 + livingPlayers.Count) % livingPlayers.Count;
        SpectatePlayer(livingPlayers[_spectatedIndex]);
    }

    private void UpdateSpectatingState()
    {
        if (_currentSpectatedPlayer != null)
        {
            if (!GodotObject.IsInstanceValid(_currentSpectatedPlayer) ||
                !_currentSpectatedPlayer.IsInsideTree() ||
                _currentSpectatedPlayer.Health == null ||
                _currentSpectatedPlayer.Health.IsDead)
            {
                var livingPlayers = GetLivingPlayers();
                if (livingPlayers.Count > 0)
                {
                    _spectatedIndex = _spectatedIndex % livingPlayers.Count;
                    SpectatePlayer(livingPlayers[_spectatedIndex]);
                }
                else
                {
                    SpectatePlayer(null);
                }
            }
        }
        else
        {
            var livingPlayers = GetLivingPlayers();
            if (livingPlayers.Count > 0)
            {
                _spectatedIndex = 0;
                SpectatePlayer(livingPlayers[0]);
            }
        }
    }

    private void OnPlayerDied()
    {
        CollisionLayer = 0;
        CollisionMask = 0;
        GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.SetDeferred("disabled", true);
        if (MeshInstance != null) MeshInstance.Visible = false;
        if (NameCard != null) NameCard.Visible = false;

        if (IsMultiplayerAuthority())
        {
            StartSpectating();
        }

        DropAllHeldItems();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcDropLoot()
    {
        DropAllHeldItems();

        if (Multiplayer.IsServer())
        {
            long senderId = Multiplayer.GetRemoteSenderId();
            if (senderId != 0 && senderId != 1)
            {
                foreach (long peerId in Multiplayer.GetPeers())
                {
                    if (peerId != senderId)
                    {
                        RpcId(peerId, nameof(RpcDropLoot));
                    }
                }
            }
        }
    }

    public void DropAllHeldItems()
    {
        if (ItemHand != null)
        {
            var children = new Godot.Collections.Array<Node>(ItemHand.GetChildren());
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Product product && GodotObject.IsInstanceValid(product))
                {
                    product.Reparent(GetTree().CurrentScene, true);
                    product.GlobalPosition = GlobalPosition + new Vector3(0, 0.5f, 0);
                    product.Freeze = false;
                    product.CollisionLayer = 1;
                    product.CollisionMask = 3;
                    product.Visible = true;
                    product.IsForSale = false;
                    product.WasBought = true;
                    product.CanBePickedUp = true;

                    float angle = (float)(i * (2.0 * Mathf.Pi / Mathf.Max(1, children.Count)));
                    Vector3 scatterDir = new Vector3(Mathf.Cos(angle), 1.0f, Mathf.Sin(angle)).Normalized();
                    product.LinearVelocity = scatterDir * 2.5f;
                }
            }
        }

        if (Inventory != null)
        {
            Inventory.DropLoot();
        }
        HeldItem = null;
    }
}
