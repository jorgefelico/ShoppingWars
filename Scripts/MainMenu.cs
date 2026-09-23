using System;
using Godot;

public partial class MainMenu : Control
{
    [Export] private Button HostButton;
    [Export] private Button JoinButton;
    [Export] private Button SettingsButton;
    [Export] private Button QuitButton;
    [Export] private VBoxContainer InviteContainer;
    [Export] private Label StatusLabel;

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

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        try
        {
            SettingsManager.Initialize();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MainMenu] Error during SettingsManager.Initialize: {ex.Message}");
        }

        if (HostButton == null) HostButton = GetNodeOrNull<Button>("VBoxContainer/Host");
        if (HostButton != null)
        {
            HostButton.Pressed += OnHostPressed;
        }

        if (JoinButton == null) JoinButton = GetNodeOrNull<Button>("VBoxContainer/Join");
        if (JoinButton != null)
        {
            JoinButton.Pressed += OnJoinPressed;
        }

        if (SettingsButton == null) SettingsButton = GetNodeOrNull<Button>("VBoxContainer/Settings");
        if (SettingsButton != null)
        {
            SettingsButton.Pressed += OnSettingsPressed;
        }

        if (QuitButton == null) QuitButton = GetNodeOrNull<Button>("VBoxContainer/Quit");
        if (QuitButton != null)
        {
            QuitButton.Pressed += OnQuitPressed;
        }

        if (StatusLabel == null) StatusLabel = GetNodeOrNull<Label>("StatusLabel");
        if (StatusLabel != null)
        {
            StatusLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            StatusLabel.Text = "Main Menu Ready";
        }

        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.OnInviteReceived += OnInviteReceived;
        }

        StyleMainMenuUI();
    }

    private void StyleMainMenuUI()
    {
        // 1. Create comic game title marquee if not present
        var titleNode = GetNodeOrNull<Control>("ComicTitleMarquee");
        if (titleNode == null)
        {
            var titleBox = new VBoxContainer
            {
                Name = "ComicTitleMarquee",
                MouseFilter = MouseFilterEnum.Ignore
            };
            titleBox.SetAnchorsPreset(LayoutPreset.TopWide);
            titleBox.AnchorLeft = 0.5f;
            titleBox.AnchorRight = 0.5f;
            titleBox.AnchorTop = 0f;
            titleBox.AnchorBottom = 0f;
            titleBox.OffsetLeft = -320;
            titleBox.OffsetRight = 320;
            titleBox.OffsetTop = 40;
            titleBox.OffsetBottom = 160;
            titleBox.Alignment = BoxContainer.AlignmentMode.Center;
            titleBox.AddThemeConstantOverride("separation", 2);

            var titleLbl = new Label
            {
                Text = "SHOPPING WARS",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            UITheme.FormatComicLabel(titleLbl, UITheme.TitleFont, 68, UITheme.FlyerYellow, UITheme.InkBlack, 6, UITheme.InkBlack, new Vector2I(4, 5));
            titleBox.AddChild(titleLbl);

            var subtitlePill = new PanelContainer
            {
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            subtitlePill.AddThemeStyleboxOverride("panel", UITheme.CreatePriceBadgeStyle(UITheme.ActionRed, 6));
            titleBox.AddChild(subtitlePill);

            var subtitleLbl = new Label
            {
                Text = "★ EVERY AISLE IS A BATTLEGROUND • STORE #404 ★",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            UITheme.FormatComicLabel(subtitleLbl, UITheme.BodyFont, 13, Colors.White, UITheme.InkBlack, 2);
            subtitlePill.AddChild(subtitleLbl);

            AddChild(titleBox);

            // Subtle comic idle float
            var tw = CreateTween();
            tw.SetLoops();
            tw.TweenProperty(titleBox, "position:y", 36f, 1.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tw.TweenProperty(titleBox, "position:y", 44f, 1.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }

        // 2. Style arcade buttons
        if (HostButton != null)
        {
            HostButton.Text = "🛒  HOST STEAM LOBBY";
            HostButton.CustomMinimumSize = new Vector2(280, 48);
            UITheme.ApplyArcadeButton(HostButton, UITheme.FreshGreen, UITheme.EmeraldDark, 16, 9);
        }

        if (JoinButton != null)
        {
            JoinButton.Text = "🏷️  JOIN A FRIEND";
            JoinButton.CustomMinimumSize = new Vector2(280, 48);
            UITheme.ApplyArcadeButton(JoinButton, UITheme.ElectricCyan, UITheme.CyanDark, 16, 9);
        }

        if (SettingsButton != null)
        {
            SettingsButton.Text = "⚙️  SETTINGS & GRAPHICS";
            SettingsButton.CustomMinimumSize = new Vector2(280, 44);
            UITheme.ApplyArcadeButton(SettingsButton, UITheme.ClearanceOrange, null, 15, 9);
        }

        if (QuitButton != null)
        {
            QuitButton.Text = "🚪  QUIT TO DESKTOP";
            QuitButton.CustomMinimumSize = new Vector2(280, 44);
            UITheme.ApplyArcadeButton(QuitButton, UITheme.ActionRed, UITheme.CrimsonDark, 15, 9);
        }

        var btnVBox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
        if (btnVBox != null)
        {
            btnVBox.OffsetTop = -30;
            btnVBox.OffsetBottom = 160;
            btnVBox.AddThemeConstantOverride("separation", 12);
        }

        if (StatusLabel != null)
        {
            UITheme.FormatComicLabel(StatusLabel, UITheme.BodyFont, 14, UITheme.FlyerYellow, UITheme.InkBlack, 3);
        }
    }

    public override void _ExitTree()
    {
        if (HostButton != null)
        {
            HostButton.Pressed -= OnHostPressed;
        }
        if (JoinButton != null)
        {
            JoinButton.Pressed -= OnJoinPressed;
        }
        if (SettingsButton != null)
        {
            SettingsButton.Pressed -= OnSettingsPressed;
        }
        if (QuitButton != null)
        {
            QuitButton.Pressed -= OnQuitPressed;
        }

        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.OnInviteReceived -= OnInviteReceived;
        }
    }

    private void OnQuitPressed()
    {
        GD.Print("[MainMenu] Quit to Desktop pressed. Exiting game...");

        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer != null)
        {
            try
            {
                Multiplayer.MultiplayerPeer.Close();
            }
            catch { }
            Multiplayer.MultiplayerPeer = null;
        }

        SteamManager.Instance?.Shutdown();

        GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
        GetTree().Quit();

        // Guaranteed process termination in case background GDExtension or audio threads don't exit promptly
        System.Environment.Exit(0);
    }
    

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
        _onboardConfirmButton.FocusMode = Control.FocusModeEnum.All;
        _onboardConfirmButton.MouseFilter = Control.MouseFilterEnum.Stop;
        UITheme.ApplyArcadeButton(_onboardConfirmButton, UITheme.FreshGreen, UITheme.EmeraldDark, 16, 8);
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
        else if (_settingsPopup != null && _settingsPopup.Visible)
        {
            CloseSettingsPopup();
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
        UITheme.FormatComicLabel(title, UITheme.TitleFont, 32, UITheme.FlyerYellow, UITheme.InkBlack, 4);
        vbox.AddChild(title);

        _friendsStatus = new Label();
        _friendsStatus.HorizontalAlignment = HorizontalAlignment.Center;
        _friendsStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        UITheme.FormatComicLabel(_friendsStatus, UITheme.BodyFont, 13, UITheme.PaperWhite, UITheme.InkBlack, 2);
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
        UITheme.ApplyArcadeButton(_friendsRefreshButton, UITheme.ElectricCyan, UITheme.CyanDark, 14, 8);
        footer.AddChild(_friendsRefreshButton);

        var closeButton = new Button();
        closeButton.Text = "CLOSE [ESC]";
        closeButton.CustomMinimumSize = new Vector2(160, 40);
        closeButton.Pressed += CloseFriendsPopup;
        UITheme.ApplyArcadeButton(closeButton, UITheme.ActionRed, UITheme.CrimsonDark, 14, 8);
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
        btn.FocusMode = Control.FocusModeEnum.All;
        UITheme.ApplyArcadeButton(btn, UITheme.FreshGreen, UITheme.EmeraldDark, 15, 8);
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

    // ==================== SETTINGS & GRAPHICS MODAL ====================
    private Control _settingsPopup;
    private Button _menuFullscreenBtn;
    private Button _menuVsyncBtn;
    private Button _menuPresetLowBtn;
    private Button _menuPresetMedBtn;
    private Button _menuPresetHighBtn;
    private Button _menuPresetUltraBtn;
    private Label _menuVramLabel;
    private Button _menuAaBtn;
    private Button _menuShadowBtn;
    private Button _menuSsrBtn;
    private Button _menuFogBtn;
    private HSlider _menuMasterSlider;
    private Label _menuMasterValLabel;
    private HSlider _menuSfxSlider;
    private Label _menuSfxValLabel;
    private HSlider _menuMusicSlider;
    private Label _menuMusicValLabel;
    private HSlider _menuSensSlider;
    private Label _menuSensValLabel;
    private HSlider _menuFovSlider;
    private Label _menuFovValLabel;

    private void OnSettingsPressed()
    {
        OpenSettingsPopup();
    }

    private void OpenSettingsPopup()
    {
        if (_settingsPopup == null) BuildSettingsPopup();
        RefreshMenuSettings();
        _settingsPopup.Visible = true;
    }

    private void CloseSettingsPopup()
    {
        if (_settingsPopup == null || !_settingsPopup.Visible) return;
        _settingsPopup.Visible = false;
        SettingsManager.SaveSettings();
        if (StatusLabel != null) StatusLabel.Text = "Settings saved.";
    }

    private void RefreshMenuSettings()
    {
        if (_menuMasterSlider != null) _menuMasterSlider.Value = (int)(SettingsManager.MasterVolume * 100);
        if (_menuMasterValLabel != null) _menuMasterValLabel.Text = $"{(int)(SettingsManager.MasterVolume * 100)}%";
        if (_menuSfxSlider != null) _menuSfxSlider.Value = (int)(SettingsManager.SfxVolume * 100);
        if (_menuSfxValLabel != null) _menuSfxValLabel.Text = $"{(int)(SettingsManager.SfxVolume * 100)}%";
        if (_menuMusicSlider != null) _menuMusicSlider.Value = (int)(SettingsManager.MusicVolume * 100);
        if (_menuMusicValLabel != null) _menuMusicValLabel.Text = $"{(int)(SettingsManager.MusicVolume * 100)}%";
        if (_menuSensSlider != null) _menuSensSlider.Value = SettingsManager.MouseSensitivity;
        if (_menuSensValLabel != null) _menuSensValLabel.Text = $"{SettingsManager.MouseSensitivity:0.00}x";
        if (_menuFovSlider != null) _menuFovSlider.Value = (int)SettingsManager.Fov;
        if (_menuFovValLabel != null) _menuFovValLabel.Text = $"{(int)SettingsManager.Fov}°";
        if (_menuFullscreenBtn != null) _menuFullscreenBtn.Text = SettingsManager.IsFullscreen ? "🖥️ Fullscreen: ON" : "🖥️ Fullscreen: OFF";
        if (_menuVsyncBtn != null) _menuVsyncBtn.Text = SettingsManager.IsVsync ? "⚡ V-Sync: ON" : "⚡ V-Sync: OFF";

        UpdateMenuGraphicsUI();
    }

    private void UpdateMenuGraphicsUI()
    {
        var curPreset = SettingsManager.CurrentPreset;
        if (_menuPresetLowBtn != null) StyleMenuPresetButton(_menuPresetLowBtn, curPreset == SettingsManager.GraphicsPreset.Low, SettingsManager.GetPresetColor(SettingsManager.GraphicsPreset.Low));
        if (_menuPresetMedBtn != null) StyleMenuPresetButton(_menuPresetMedBtn, curPreset == SettingsManager.GraphicsPreset.Medium, SettingsManager.GetPresetColor(SettingsManager.GraphicsPreset.Medium));
        if (_menuPresetHighBtn != null) StyleMenuPresetButton(_menuPresetHighBtn, curPreset == SettingsManager.GraphicsPreset.High, SettingsManager.GetPresetColor(SettingsManager.GraphicsPreset.High));
        if (_menuPresetUltraBtn != null) StyleMenuPresetButton(_menuPresetUltraBtn, curPreset == SettingsManager.GraphicsPreset.Ultra, SettingsManager.GetPresetColor(SettingsManager.GraphicsPreset.Ultra));

        if (_menuVramLabel != null)
        {
            _menuVramLabel.Text = SettingsManager.GetEstimatedVramString();
            _menuVramLabel.AddThemeColorOverride("font_color", SettingsManager.GetPresetColor(curPreset));
        }

        if (_menuAaBtn != null) _menuAaBtn.Text = SettingsManager.GetAntiAliasingLabel(SettingsManager.CurrentAntiAliasing);
        if (_menuShadowBtn != null) _menuShadowBtn.Text = SettingsManager.GetShadowQualityLabel(SettingsManager.CurrentShadowQuality);
        if (_menuSsrBtn != null) _menuSsrBtn.Text = SettingsManager.GetSsrQualityLabel(SettingsManager.CurrentSsr);
        if (_menuFogBtn != null) _menuFogBtn.Text = SettingsManager.GetVolumetricFogLabel(SettingsManager.CurrentVolumetricFog);
    }

    private void StyleMenuPresetButton(Button btn, bool isSelected, Color activeColor)
    {
        var style = new StyleBoxFlat();
        style.CornerRadiusTopLeft = 6;
        style.CornerRadiusTopRight = 6;
        style.CornerRadiusBottomLeft = 6;
        style.CornerRadiusBottomRight = 6;
        if (isSelected)
        {
            style.BgColor = new Color(activeColor.R * 0.35f, activeColor.G * 0.35f, activeColor.B * 0.35f, 0.95f);
            style.BorderColor = activeColor;
            style.BorderWidthLeft = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthRight = 2;
            style.BorderWidthBottom = 2;
            btn.AddThemeColorOverride("font_color", Colors.White);
        }
        else
        {
            style.BgColor = new Color(0.10f, 0.12f, 0.16f, 0.85f);
            style.BorderColor = new Color(0.30f, 0.35f, 0.45f, 0.6f);
            style.BorderWidthLeft = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthBottom = 1;
            btn.AddThemeColorOverride("font_color", new Color(0.70f, 0.75f, 0.85f));
        }
        btn.AddThemeStyleboxOverride("normal", style);

        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = isSelected
            ? new Color(activeColor.R * 0.5f, activeColor.G * 0.5f, activeColor.B * 0.5f, 1.0f)
            : new Color(0.18f, 0.22f, 0.28f, 0.95f);
        btn.AddThemeStyleboxOverride("hover", hover);
    }

    private void BuildSettingsPopup()
    {
        _settingsPopup = new Panel();
        _settingsPopup.Name = "SettingsPopup";
        _settingsPopup.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _settingsPopup.MouseFilter = Control.MouseFilterEnum.Stop;
        _settingsPopup.Visible = false;
        _settingsPopup.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                CloseSettingsPopup();
            }
        };

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        _settingsPopup.AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Pass;
        _settingsPopup.AddChild(center);

        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(680, 580);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.AddThemeStyleboxOverride("panel", OnboardingContent.CreateCardStyle());
        center.AddChild(card);

        var rootVBox = new VBoxContainer();
        rootVBox.AddThemeConstantOverride("separation", 10);
        card.AddChild(rootVBox);

        var title = new Label();
        title.Text = "⚙️ GAME & GRAPHICS SETTINGS";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        UITheme.FormatComicLabel(title, UITheme.TitleFont, 32, UITheme.FlyerYellow, UITheme.InkBlack, 4);
        rootVBox.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = "Configure display, graphics presets, audio, and controls.";
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        UITheme.FormatComicLabel(subtitle, UITheme.BodyFont, 13, UITheme.SubtitleGray, UITheme.InkBlack, 2);
        rootVBox.AddChild(subtitle);

        rootVBox.AddChild(new HSeparator());

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        rootVBox.AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);

        // Display
        var displayHeader = new Label();
        displayHeader.Text = "🖥️ DISPLAY";
        displayHeader.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 1.0f));
        displayHeader.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(displayHeader);

        var displayRow = new HBoxContainer();
        displayRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(displayRow);

        _menuFullscreenBtn = new Button();
        _menuFullscreenBtn.Text = SettingsManager.IsFullscreen ? "🖥️ Fullscreen: ON" : "🖥️ Fullscreen: OFF";
        _menuFullscreenBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuFullscreenBtn.CustomMinimumSize = new Vector2(0, 34);
        _menuFullscreenBtn.AddThemeFontSizeOverride("font_size", 12);
        _menuFullscreenBtn.Pressed += () =>
        {
            SettingsManager.SetFullscreen(!SettingsManager.IsFullscreen);
            _menuFullscreenBtn.Text = SettingsManager.IsFullscreen ? "🖥️ Fullscreen: ON" : "🖥️ Fullscreen: OFF";
        };
        displayRow.AddChild(_menuFullscreenBtn);

        _menuVsyncBtn = new Button();
        _menuVsyncBtn.Text = SettingsManager.IsVsync ? "⚡ V-Sync: ON" : "⚡ V-Sync: OFF";
        _menuVsyncBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuVsyncBtn.CustomMinimumSize = new Vector2(0, 34);
        _menuVsyncBtn.AddThemeFontSizeOverride("font_size", 12);
        _menuVsyncBtn.Pressed += () =>
        {
            SettingsManager.SetVsync(!SettingsManager.IsVsync);
            _menuVsyncBtn.Text = SettingsManager.IsVsync ? "⚡ V-Sync: ON" : "⚡ V-Sync: OFF";
        };
        displayRow.AddChild(_menuVsyncBtn);

        vbox.AddChild(new HSeparator());

        // Graphics & Performance Section
        var graphicsHeader = new Label();
        graphicsHeader.Text = "🎨 GRAPHICS PRESETS & HARDWARE PROFILE";
        graphicsHeader.AddThemeColorOverride("font_color", new Color(0.3f, 0.95f, 0.8f));
        graphicsHeader.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(graphicsHeader);

        var presetsContainer = new VBoxContainer();
        presetsContainer.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(presetsContainer);

        var presetsRow = new HBoxContainer();
        presetsRow.AddThemeConstantOverride("separation", 8);
        presetsContainer.AddChild(presetsRow);

        _menuPresetLowBtn = new Button();
        _menuPresetLowBtn.Text = "🟢 LOW";
        _menuPresetLowBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuPresetLowBtn.CustomMinimumSize = new Vector2(0, 34);
        _menuPresetLowBtn.AddThemeFontSizeOverride("font_size", 12);
        _menuPresetLowBtn.Pressed += () =>
        {
            SettingsManager.SetGraphicsPreset(SettingsManager.GraphicsPreset.Low);
            UpdateMenuGraphicsUI();
        };
        presetsRow.AddChild(_menuPresetLowBtn);

        _menuPresetMedBtn = new Button();
        _menuPresetMedBtn.Text = "🟡 MEDIUM";
        _menuPresetMedBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuPresetMedBtn.CustomMinimumSize = new Vector2(0, 34);
        _menuPresetMedBtn.AddThemeFontSizeOverride("font_size", 12);
        _menuPresetMedBtn.Pressed += () =>
        {
            SettingsManager.SetGraphicsPreset(SettingsManager.GraphicsPreset.Medium);
            UpdateMenuGraphicsUI();
        };
        presetsRow.AddChild(_menuPresetMedBtn);

        _menuPresetHighBtn = new Button();
        _menuPresetHighBtn.Text = "🔵 HIGH";
        _menuPresetHighBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuPresetHighBtn.CustomMinimumSize = new Vector2(0, 34);
        _menuPresetHighBtn.AddThemeFontSizeOverride("font_size", 12);
        _menuPresetHighBtn.Pressed += () =>
        {
            SettingsManager.SetGraphicsPreset(SettingsManager.GraphicsPreset.High);
            UpdateMenuGraphicsUI();
        };
        presetsRow.AddChild(_menuPresetHighBtn);

        _menuPresetUltraBtn = new Button();
        _menuPresetUltraBtn.Text = "🟣 ULTRA";
        _menuPresetUltraBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuPresetUltraBtn.CustomMinimumSize = new Vector2(0, 34);
        _menuPresetUltraBtn.AddThemeFontSizeOverride("font_size", 12);
        _menuPresetUltraBtn.Pressed += () =>
        {
            SettingsManager.SetGraphicsPreset(SettingsManager.GraphicsPreset.Ultra);
            UpdateMenuGraphicsUI();
        };
        presetsRow.AddChild(_menuPresetUltraBtn);

        _menuVramLabel = new Label();
        _menuVramLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _menuVramLabel.AddThemeFontSizeOverride("font_size", 11);
        presetsContainer.AddChild(_menuVramLabel);

        // Advanced Quality Toggles (2x2 Grid)
        var advancedGrid = new GridContainer();
        advancedGrid.Columns = 2;
        advancedGrid.AddThemeConstantOverride("h_separation", 10);
        advancedGrid.AddThemeConstantOverride("v_separation", 8);
        vbox.AddChild(advancedGrid);

        _menuAaBtn = new Button();
        _menuAaBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuAaBtn.CustomMinimumSize = new Vector2(0, 32);
        _menuAaBtn.AddThemeFontSizeOverride("font_size", 11);
        _menuAaBtn.Pressed += () =>
        {
            var next = SettingsManager.CurrentAntiAliasing switch
            {
                SettingsManager.AntiAliasingMode.Off => SettingsManager.AntiAliasingMode.Fxaa,
                SettingsManager.AntiAliasingMode.Fxaa => SettingsManager.AntiAliasingMode.Taa,
                SettingsManager.AntiAliasingMode.Taa => SettingsManager.AntiAliasingMode.TaaAndSmaa,
                _ => SettingsManager.AntiAliasingMode.Off
            };
            SettingsManager.SetAntiAliasing(next);
            UpdateMenuGraphicsUI();
        };
        advancedGrid.AddChild(_menuAaBtn);

        _menuShadowBtn = new Button();
        _menuShadowBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuShadowBtn.CustomMinimumSize = new Vector2(0, 32);
        _menuShadowBtn.AddThemeFontSizeOverride("font_size", 11);
        _menuShadowBtn.Pressed += () =>
        {
            var next = SettingsManager.CurrentShadowQuality switch
            {
                SettingsManager.ShadowQualityLevel.Low => SettingsManager.ShadowQualityLevel.Medium,
                SettingsManager.ShadowQualityLevel.Medium => SettingsManager.ShadowQualityLevel.High,
                SettingsManager.ShadowQualityLevel.High => SettingsManager.ShadowQualityLevel.Ultra,
                _ => SettingsManager.ShadowQualityLevel.Low
            };
            SettingsManager.SetShadowQuality(next);
            UpdateMenuGraphicsUI();
        };
        advancedGrid.AddChild(_menuShadowBtn);

        _menuSsrBtn = new Button();
        _menuSsrBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuSsrBtn.CustomMinimumSize = new Vector2(0, 32);
        _menuSsrBtn.AddThemeFontSizeOverride("font_size", 11);
        _menuSsrBtn.Pressed += () =>
        {
            var next = SettingsManager.CurrentSsr switch
            {
                SettingsManager.QualityLevel.Off => SettingsManager.QualityLevel.Low,
                SettingsManager.QualityLevel.Low => SettingsManager.QualityLevel.Medium,
                SettingsManager.QualityLevel.Medium => SettingsManager.QualityLevel.High,
                _ => SettingsManager.QualityLevel.Off
            };
            SettingsManager.SetSsrQuality(next);
            UpdateMenuGraphicsUI();
        };
        advancedGrid.AddChild(_menuSsrBtn);

        _menuFogBtn = new Button();
        _menuFogBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _menuFogBtn.CustomMinimumSize = new Vector2(0, 32);
        _menuFogBtn.AddThemeFontSizeOverride("font_size", 11);
        _menuFogBtn.Pressed += () =>
        {
            var next = SettingsManager.CurrentVolumetricFog switch
            {
                SettingsManager.QualityLevel.Off => SettingsManager.QualityLevel.Low,
                SettingsManager.QualityLevel.Low => SettingsManager.QualityLevel.High,
                _ => SettingsManager.QualityLevel.Off
            };
            SettingsManager.SetVolumetricFog(next);
            UpdateMenuGraphicsUI();
        };
        advancedGrid.AddChild(_menuFogBtn);

        vbox.AddChild(new HSeparator());

        // Audio Section
        var audioHeader = new Label();
        audioHeader.Text = "🔊 AUDIO SETTINGS";
        audioHeader.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 1.0f));
        audioHeader.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(audioHeader);

        _menuMasterSlider = CreateSliderRow(vbox, "Master Volume", (int)(SettingsManager.MasterVolume * 100), "%", out _menuMasterValLabel);
        _menuMasterSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetMasterVolume((float)v / 100f);
            if (_menuMasterValLabel != null) _menuMasterValLabel.Text = $"{(int)v}%";
        };

        _menuSfxSlider = CreateSliderRow(vbox, "SFX & Hazards", (int)(SettingsManager.SfxVolume * 100), "%", out _menuSfxValLabel);
        _menuSfxSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetSfxVolume((float)v / 100f);
            if (_menuSfxValLabel != null) _menuSfxValLabel.Text = $"{(int)v}%";
        };

        _menuMusicSlider = CreateSliderRow(vbox, "Music & Ambience", (int)(SettingsManager.MusicVolume * 100), "%", out _menuMusicValLabel);
        _menuMusicSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetMusicVolume((float)v / 100f);
            if (_menuMusicValLabel != null) _menuMusicValLabel.Text = $"{(int)v}%";
        };

        vbox.AddChild(new HSeparator());

        // Controls & Camera Section
        var controlsHeader = new Label();
        controlsHeader.Text = "🎮 CONTROLS & CAMERA";
        controlsHeader.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
        controlsHeader.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(controlsHeader);

        _menuSensSlider = CreateFloatSliderRow(vbox, "Mouse Sensitivity", SettingsManager.MouseSensitivity, 0.2f, 3.0f, 0.05f, "x", out _menuSensValLabel);
        _menuSensSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetMouseSensitivity((float)v);
            if (_menuSensValLabel != null) _menuSensValLabel.Text = $"{v:0.00}x";
        };

        _menuFovSlider = CreateSliderRow(vbox, "Field of View (FOV)", (int)SettingsManager.Fov, "°", out _menuFovValLabel, 70, 110);
        _menuFovSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetFov((float)v);
            if (_menuFovValLabel != null) _menuFovValLabel.Text = $"{(int)v}°";
        };

        UpdateMenuGraphicsUI();

        rootVBox.AddChild(new HSeparator());

        // Footer Action
        var footer = new CenterContainer();
        rootVBox.AddChild(footer);

        var closeBtn = new Button();
        closeBtn.Text = "💾 SAVE & CLOSE [ESC]";
        closeBtn.CustomMinimumSize = new Vector2(280, 44);
        UITheme.ApplyArcadeButton(closeBtn, UITheme.FreshGreen, UITheme.EmeraldDark, 15, 8);
        closeBtn.Pressed += CloseSettingsPopup;
        footer.AddChild(closeBtn);

        AddChild(_settingsPopup);
    }

    private HSlider CreateSliderRow(VBoxContainer parent, string labelText, int initialValue, string unit, out Label valueLabel, int min = 0, int max = 100)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        parent.AddChild(row);

        var lbl = new Label();
        lbl.Text = labelText;
        lbl.CustomMinimumSize = new Vector2(160, 0);
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
        row.AddChild(lbl);

        var slider = new HSlider();
        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Step = 1;
        slider.Value = initialValue;
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        slider.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(slider);

        valueLabel = new Label();
        valueLabel.Text = $"{initialValue}{unit}";
        valueLabel.CustomMinimumSize = new Vector2(55, 0);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        valueLabel.AddThemeFontSizeOverride("font_size", 13);
        valueLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
        row.AddChild(valueLabel);

        return slider;
    }

    private HSlider CreateFloatSliderRow(VBoxContainer parent, string labelText, float initialValue, float min, float max, float step, string unit, out Label valueLabel)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        parent.AddChild(row);

        var lbl = new Label();
        lbl.Text = labelText;
        lbl.CustomMinimumSize = new Vector2(160, 0);
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
        row.AddChild(lbl);

        var slider = new HSlider();
        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Step = step;
        slider.Value = initialValue;
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        slider.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(slider);

        valueLabel = new Label();
        valueLabel.Text = $"{initialValue:0.00}{unit}";
        valueLabel.CustomMinimumSize = new Vector2(55, 0);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        valueLabel.AddThemeFontSizeOverride("font_size", 13);
        valueLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
        row.AddChild(valueLabel);

        return slider;
    }
}
