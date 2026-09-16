using System;
using Godot;

public partial class MainMenu : Control
{
    [Export] private Button HostButton;
    [Export] private Button JoinButton;
    [Export] private Button QuitButton;
    [Export] private VBoxContainer InviteContainer;
    [Export] private Label StatusLabel;

    public override void _Ready()
    {
        if (HostButton != null) HostButton.Pressed += OnHostPressed;
        if (JoinButton != null) JoinButton.Pressed += OnJoinPressed;

        if (QuitButton == null) QuitButton = GetNodeOrNull<Button>("VBoxContainer/Quit");
        if (QuitButton != null) QuitButton.Pressed += OnQuitPressed;

        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.OnInviteReceived += OnInviteReceived;
        }

        if (StatusLabel != null) StatusLabel.Text = "Main Menu Ready";
    }

    public override void _ExitTree()
    {
        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.OnInviteReceived -= OnInviteReceived;
        }
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
    private Control _onboardPopup;
    private Button _onboardConfirmButton;
    private Action _onboardAction;

    // Pre-match onboarding popup. Shown over the main menu before a lobby is
    // created/joined, so the heavy StoreInterior load + network handshake only
    // start once the player has read the guide and confirmed.
    private void BuildOnboardPopup()
    {
        _onboardPopup = new Panel();
        _onboardPopup.Name = "OnboardPopup";
        _onboardPopup.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _onboardPopup.MouseFilter = Control.MouseFilterEnum.Stop;
        _onboardPopup.Visible = false;
        _onboardPopup.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                CancelOnboarding();
            }
        };

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.04f, 0.05f, 0.08f, 0.90f);
        _onboardPopup.AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Pass;
        _onboardPopup.AddChild(center);

        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(820, 540);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.AddThemeStyleboxOverride("panel", OnboardingContent.CreateCardStyle());
        center.AddChild(card);

        var vbox = OnboardingContent.BuildCardBody();
        card.AddChild(vbox);

        var footer = new VBoxContainer();
        footer.AddThemeConstantOverride("separation", 6);
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(footer);

        var btnContainer = new CenterContainer();
        footer.AddChild(btnContainer);

        _onboardConfirmButton = new Button();
        _onboardConfirmButton.CustomMinimumSize = new Vector2(340, 44);
        _onboardConfirmButton.AddThemeFontSizeOverride("font_size", 16);
        _onboardConfirmButton.FocusMode = Control.FocusModeEnum.All;
        _onboardConfirmButton.MouseFilter = Control.MouseFilterEnum.Stop;

        var btnStyle = new StyleBoxFlat();
        btnStyle.BgColor = new Color(0.14f, 0.42f, 0.22f, 0.95f);
        btnStyle.BorderColor = new Color(0.35f, 0.95f, 0.45f, 0.9f);
        btnStyle.BorderWidthLeft = 2;
        btnStyle.BorderWidthTop = 2;
        btnStyle.BorderWidthRight = 2;
        btnStyle.BorderWidthBottom = 2;
        btnStyle.CornerRadiusTopLeft = 8;
        btnStyle.CornerRadiusTopRight = 8;
        btnStyle.CornerRadiusBottomLeft = 8;
        btnStyle.CornerRadiusBottomRight = 8;
        _onboardConfirmButton.AddThemeStyleboxOverride("normal", btnStyle);

        var hoverStyle = (StyleBoxFlat)btnStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.20f, 0.55f, 0.30f, 1.0f);
        hoverStyle.BorderColor = new Color(0.5f, 1.0f, 0.6f, 1.0f);
        _onboardConfirmButton.AddThemeStyleboxOverride("hover", hoverStyle);

        var focusStyle = (StyleBoxFlat)btnStyle.Duplicate();
        focusStyle.BorderColor = Colors.White;
        focusStyle.BorderWidthLeft = 3;
        focusStyle.BorderWidthTop = 3;
        focusStyle.BorderWidthRight = 3;
        focusStyle.BorderWidthBottom = 3;
        _onboardConfirmButton.AddThemeStyleboxOverride("focus", focusStyle);

        _onboardConfirmButton.Pressed += OnOnboardConfirmed;
        btnContainer.AddChild(_onboardConfirmButton);

        var tipLbl = new Label();
        tipLbl.Text = "Press [Space] to confirm — or [Esc] / click outside to go back (nothing connects yet).";
        tipLbl.HorizontalAlignment = HorizontalAlignment.Center;
        tipLbl.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 0.8f));
        tipLbl.AddThemeFontSizeOverride("font_size", 11);
        footer.AddChild(tipLbl);

        AddChild(_onboardPopup);
    }

    private void ShowOnboarding(string confirmText, Action onConfirm)
    {
        if (_onboardPopup == null) BuildOnboardPopup();
        _onboardAction = onConfirm;
        _onboardConfirmButton.Text = confirmText;
        _onboardPopup.Visible = true;
        _onboardConfirmButton.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void HideOnboarding()
    {
        if (_onboardPopup != null) _onboardPopup.Visible = false;
    }

    private void OnOnboardConfirmed()
    {
        Action action = _onboardAction;
        _onboardAction = null;
        HideOnboarding();
        action?.Invoke();
    }

    private void CancelOnboarding()
    {
        if (_onboardPopup == null || !_onboardPopup.Visible) return;
        _onboardAction = null;
        HideOnboarding();
        if (StatusLabel != null) StatusLabel.Text = "Main Menu Ready";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_onboardPopup != null && _onboardPopup.Visible &&
            @event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
        {
            CancelOnboarding();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnInviteReceived(ulong lobbyId, string friendName)
    {
        if (StatusLabel != null) StatusLabel.Text = $"📩 Game Invite Received from {friendName}!";

        if (InviteContainer == null) return;

        // Clear any previous invite buttons
        foreach (Node child in InviteContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Create an Accept Invite Button on Main Menu
        Button inviteBtn = new Button
        {
            Text = $"📩 ACCEPT INVITE FROM {friendName.ToUpper()} (CLICK TO JOIN)"
        };

        ulong targetLobby = lobbyId;
        inviteBtn.Pressed += () =>
        {
            ShowOnboarding($"✅ GOT IT — JOIN {friendName.ToUpper()}'S MATCH", () =>
            {
                if (StatusLabel != null) StatusLabel.Text = $"Joining {friendName}'s match...";
                SteamManager.Instance?.JoinLobbyById(targetLobby);
            });
        };

        InviteContainer.AddChild(inviteBtn);
    }

    private void OnJoinPressed()
    {
        if (StatusLabel != null) StatusLabel.Text = "Opening Steam Overlay / Joining...";
        if (!SteamManager.Instance.IsSteamInitialized) return;
        SteamManager.Instance.OpenFriendsInviteOverlay();
    }

    private void OnHostPressed()
    {
        GD.Print(SteamManager.Instance.IsOnline());
        if (!SteamManager.Instance.IsSteamInitialized || !SteamManager.Instance.IsOnline()) return;
        ShowOnboarding("🚀 GOT IT — HOST MATCH", () =>
        {
            if (StatusLabel != null) StatusLabel.Text = "Creating Steam Lobby...";
            SteamManager.Instance.HostLobby();
        });
    }
}
