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

    // "Join A Friend" popup: lists friends' active lobbies (discovered via
    // rich presence) so players can join directly without the Steam overlay.
    private Control _friendsPopup;
    private VBoxContainer _friendsListContainer;
    private Label _friendsStatus;
    private Button _friendsRefreshButton;
    private bool _friendsAutoRescan;

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
        if (!(@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)) return;

        if (_onboardPopup != null && _onboardPopup.Visible)
        {
            CancelOnboarding();
        }
        else if (_friendsPopup != null && _friendsPopup.Visible)
        {
            CloseFriendsPopup();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
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
        if (!SteamManager.Instance.IsSteamInitialized)
        {
            if (StatusLabel != null) StatusLabel.Text = "Steam is not initialized.";
            return;
        }
        if (_friendsPopup == null) BuildFriendsPopup();
        _friendsPopup.Visible = true;
        _friendsAutoRescan = true;
        ScanFriendLobbies();
    }

    private void BuildFriendsPopup()
    {
        _friendsPopup = new Panel();
        _friendsPopup.Name = "FriendsPopup";
        _friendsPopup.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _friendsPopup.MouseFilter = Control.MouseFilterEnum.Stop;
        _friendsPopup.Visible = false;
        _friendsPopup.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                CloseFriendsPopup();
            }
        };

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.04f, 0.05f, 0.08f, 0.90f);
        _friendsPopup.AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Pass;
        _friendsPopup.AddChild(center);

        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(640, 380);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.AddThemeStyleboxOverride("panel", OnboardingContent.CreateCardStyle());
        center.AddChild(card);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        card.AddChild(vbox);

        var title = new Label();
        title.Text = "🛒 JOIN A FRIEND";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(title);

        _friendsStatus = new Label();
        _friendsStatus.HorizontalAlignment = HorizontalAlignment.Center;
        _friendsStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _friendsStatus.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f, 0.9f));
        vbox.AddChild(_friendsStatus);

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0, 140);
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        vbox.AddChild(scroll);

        _friendsListContainer = new VBoxContainer();
        _friendsListContainer.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_friendsListContainer);

        var footer = new HBoxContainer();
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        footer.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(footer);

        _friendsRefreshButton = new Button();
        _friendsRefreshButton.Text = "🔄 REFRESH";
        _friendsRefreshButton.CustomMinimumSize = new Vector2(160, 40);
        _friendsRefreshButton.Pressed += () => ScanFriendLobbies();
        footer.AddChild(_friendsRefreshButton);

        var closeButton = new Button();
        closeButton.Text = "CLOSE [ESC]";
        closeButton.CustomMinimumSize = new Vector2(160, 40);
        closeButton.Pressed += CloseFriendsPopup;
        footer.AddChild(closeButton);

        AddChild(_friendsPopup);
    }

    private async void ScanFriendLobbies()
    {
        if (_friendsPopup == null || !_friendsPopup.Visible) return;
        _friendsStatus.Text = "Searching for friends in active matches...";
        foreach (Node child in _friendsListContainer.GetChildren())
        {
            child.QueueFree();
        }

        var lobbies = SteamManager.Instance.GetFriendsWithActiveLobbies();

        if (lobbies.Count == 0)
        {
            _friendsStatus.Text = "No friends with an active match.\nHave a friend hit 'Host Steam Lobby', then refresh — rich presence can take a few seconds to propagate.";

            // Rich presence propagation is async: re-scan once after a short delay.
            if (_friendsAutoRescan)
            {
                _friendsAutoRescan = false;
                await ToSignal(GetTree().CreateTimer(4.0f), SceneTreeTimer.SignalName.Timeout);
                if (_friendsPopup != null && _friendsPopup.Visible) ScanFriendLobbies();
            }
        }
        else
        {
            _friendsStatus.Text = lobbies.Count == 1
                ? "1 friend match available:"
                : $"{lobbies.Count} friend matches available:";
            foreach (var (steamId, name, lobbyId) in lobbies)
            {
                var joinBtn = CreateJoinButton($"🛒 {name.ToUpper()} — JOIN MATCH");
                joinBtn.Pressed += () => JoinFriendLobby(lobbyId, name);
                _friendsListContainer.AddChild(joinBtn);
            }
        }
    }

    private Button CreateJoinButton(string text)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(560, 44);
        btn.AddThemeFontSizeOverride("font_size", 16);
        btn.FocusMode = Control.FocusModeEnum.All;

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
        btn.AddThemeStyleboxOverride("normal", btnStyle);

        var hoverStyle = (StyleBoxFlat)btnStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.20f, 0.55f, 0.30f, 1.0f);
        hoverStyle.BorderColor = new Color(0.5f, 1.0f, 0.6f, 1.0f);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        var focusStyle = (StyleBoxFlat)btnStyle.Duplicate();
        focusStyle.BorderColor = Colors.White;
        focusStyle.BorderWidthLeft = 3;
        focusStyle.BorderWidthTop = 3;
        focusStyle.BorderWidthRight = 3;
        focusStyle.BorderWidthBottom = 3;
        btn.AddThemeStyleboxOverride("focus", focusStyle);

        return btn;
    }

    private void JoinFriendLobby(ulong lobbyId, string friendName)
    {
        CloseFriendsPopup();
        ShowOnboarding($"✅ GOT IT — JOIN {friendName.ToUpper()}'S MATCH", () =>
        {
            if (StatusLabel != null) StatusLabel.Text = $"Joining {friendName}'s match...";
            SteamManager.Instance?.JoinLobbyById(lobbyId);
        });
    }

    private void CloseFriendsPopup()
    {
        if (_friendsPopup == null || !_friendsPopup.Visible) return;
        _friendsPopup.Visible = false;
        if (StatusLabel != null) StatusLabel.Text = "Main Menu Ready";
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
