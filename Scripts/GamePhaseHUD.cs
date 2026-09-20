using System;
using Godot;

public partial class GamePhaseHUD : CanvasLayer
{
    [Export] private Label PhaseLabel;
    [Export] private Label TimerLabel;
    [Export] private Label MoneyLabel;
    [Export] private Label EventLabel;
    [Export] private Label EventDescLabel;
    [Export] private Control EventContainer;

    [Export] private Control GameOverPanel;
    [Export] private Label GameOverHeaderLabel;
    [Export] private Label WinnerNameLabel;
    [Export] private Label SubtitleLabel;
    [Export] private Label StatsDetailLabel;
    [Export] private Button PlayAgainButton;
    [Export] private Button MainMenuButton;

    public static GamePhaseHUD Instance { get; private set; }
    private Label _killFeedLabel;
    private float _killFeedTimer = 0f;
    private int _lastDisplayedMoney = -1;

    // Mr. Henderson Intercom HUD
    private PanelContainer _intercomCard;
    private Label _intercomAvatarLabel;
    private Label _intercomMessageLabel;
    private Label _intercomLiveDot;
    private Tween _intercomTween;
    private float _intercomDisplayTimer = 0f;
    private float _intercomPulseTimer = 0f;
    private bool _subscribedToManager = false;

    // Store Bounty HUD
    private Label _bountyHUDLabel;
    private float _bountyHUDTimer = 0f;

    private void UpdateMoneyDisplay()
    {
        if (MoneyLabel == null) return;
        int currentMoney = PlayerController.Instance != null ? PlayerController.Instance.Money : 0;
        MoneyLabel.Text = $"💵 ${currentMoney:N0}";

        if (_lastDisplayedMoney != -1 && _lastDisplayedMoney != currentMoney)
        {
            // Cash register punch animation!
            MoneyLabel.PivotOffset = MoneyLabel.Size / 2f;
            Tween tween = CreateTween();
            tween.TweenProperty(MoneyLabel, "scale", new Vector2(1.28f, 1.28f), 0.08f);
            tween.TweenProperty(MoneyLabel, "scale", Vector2.One, 0.15f);

            MoneyLabel.Modulate = currentMoney > _lastDisplayedMoney 
                ? new Color(0.2f, 1.0f, 0.4f)  // Green on money gained
                : new Color(1.0f, 0.85f, 0.2f); // Gold on money spent
        }
        else if (_lastDisplayedMoney == -1)
        {
            MoneyLabel.Modulate = currentMoney > 0 ? new Color(0.25f, 0.95f, 0.35f) : Colors.White;
        }

        _lastDisplayedMoney = currentMoney;
    }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        Instance = this;

        _killFeedLabel = new Label();
        _killFeedLabel.Name = "KillFeedLabel";
        _killFeedLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _killFeedLabel.Position = new Vector2(20, 70);
        _killFeedLabel.Size = new Vector2(300, 40);
        _killFeedLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
        _killFeedLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _killFeedLabel.AddThemeConstantOverride("outline_size", 4);
        _killFeedLabel.AddThemeFontSizeOverride("font_size", 16);
        _killFeedLabel.Visible = false;
        AddChild(_killFeedLabel);

        _bountyHUDLabel = new Label();
        _bountyHUDLabel.Name = "BountyHUDLabel";
        _bountyHUDLabel.Position = new Vector2(20, 105);
        _bountyHUDLabel.Size = new Vector2(500, 35);
        _bountyHUDLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.1f));
        _bountyHUDLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _bountyHUDLabel.AddThemeConstantOverride("outline_size", 4);
        _bountyHUDLabel.AddThemeFontSizeOverride("font_size", 15);
        _bountyHUDLabel.Visible = false;
        AddChild(_bountyHUDLabel);

        if (EventContainer != null)
        {
            EventContainer.Visible = false;
        }

        if (MoneyLabel != null)
        {
            MoneyLabel.AddThemeFontSizeOverride("font_size", 28);
            MoneyLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            MoneyLabel.AddThemeConstantOverride("outline_size", 6);
        }

        if (GameOverPanel != null)
        {
            GameOverPanel.Visible = false;
        }

        if (PlayAgainButton != null)
        {
            PlayAgainButton.Pressed += OnPlayAgainPressed;
        }

        if (MainMenuButton != null)
        {
            MainMenuButton.Pressed += OnMainMenuPressed;
        }

        SettingsManager.Initialize();
        BuildTutorialModal();
        BuildPerkModal();
        BuildPauseMenu();
        BuildIntercomHUD();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged += OnGamePhaseChanged;
        }

        PlayerPerk currentPerk = PlayerController.Instance?.CurrentPerk ?? PlayerPerk.None;
        UpdatePerkDisplay(currentPerk);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (ManagerAnnouncer.Instance != null)
        {
            ManagerAnnouncer.Instance.ManagerAnnounced -= OnManagerAnnounced;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        }

        if (PlayAgainButton != null)
        {
            PlayAgainButton.Pressed -= OnPlayAgainPressed;
        }

        if (MainMenuButton != null)
        {
            MainMenuButton.Pressed -= OnMainMenuPressed;
        }
    }

    public override void _Process(double delta)
    {
        if (IsAnyModalOpen && Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (GameManager.Instance == null) return;

        switch (GameManager.Instance.CurrentPhase)
        {
            case GamePhase.Lobby:
                PhaseLabel.Text = "LOBBY - WAITING FOR PLAYERS";
                PhaseLabel.Modulate = Colors.Yellow;
                MoneyLabel.Visible = false;
                if (GameOverPanel != null) GameOverPanel.Visible = false;
                break;
            case GamePhase.ShoppingTransition:
                PhaseLabel.Text = $"ROUND {GameManager.Instance.CurrentRound} / BEST OF {GameManager.Instance.RoundsToWin * 2 - 1} - PREPARE TO SHOP!";
                PhaseLabel.Modulate = Colors.Gold;
                MoneyLabel.Visible = false;
                if (GameOverPanel != null) GameOverPanel.Visible = false;
                break;
            case GamePhase.Shopping:
                PhaseLabel.Text = "SHOPPING PHASE";
                PhaseLabel.Modulate = Colors.Cyan;
                MoneyLabel.Visible = true;
                UpdateMoneyDisplay();
                if (GameOverPanel != null) GameOverPanel.Visible = false;
                break;
            case GamePhase.BattleTransition:
                PhaseLabel.Text = "STORE LOCKDOWN - PREPARE FOR BATTLE!";
                PhaseLabel.Modulate = Colors.OrangeRed;
                MoneyLabel.Visible = true;
                UpdateMoneyDisplay();
                if (GameOverPanel != null) GameOverPanel.Visible = false;
                break;
            case GamePhase.BattleRoyale:
                if (ArenaZoneManager.Instance != null && ArenaZoneManager.Instance.IsActive)
                {
                    PlayerController localPlayer = PlayerController.Instance;
                    bool isSpectating = localPlayer != null && (localPlayer.IsSpectating || (localPlayer.Health != null && localPlayer.Health.IsDead));
                    PlayerController activeTarget = isSpectating ? localPlayer?.CurrentSpectatedPlayer : localPlayer;

                    Vector3 targetPos = (activeTarget != null && GodotObject.IsInstanceValid(activeTarget)) 
                        ? activeTarget.GlobalPosition 
                        : (localPlayer != null ? localPlayer.GlobalPosition : Vector3.Zero);

                    bool targetOutside = (activeTarget != null && GodotObject.IsInstanceValid(activeTarget)) && ArenaZoneManager.Instance.IsPlayerOutside(targetPos);

                    if (targetOutside && !isSpectating)
                    {
                        float dist = ArenaZoneManager.Instance.GetDistanceToSafeZone(targetPos);
                        PhaseLabel.Text = $"⚠️ OUTSIDE SAFE ZONE! (-6 HP/s) — RETURN TO AISLES ({dist:0.0}m) ⚠️";
                        PhaseLabel.Modulate = (Time.GetTicksMsec() % 500 < 250) ? Colors.Red : Colors.Yellow;
                    }
                    else if (targetOutside && isSpectating)
                    {
                        string spectatedName = activeTarget.PlayerName;
                        float dist = ArenaZoneManager.Instance.GetDistanceToSafeZone(targetPos);
                        PhaseLabel.Text = $"⚠️ {spectatedName} IS OUTSIDE SAFE ZONE! ({dist:0.0}m) ⚠️";
                        PhaseLabel.Modulate = (Time.GetTicksMsec() % 500 < 250) ? Colors.OrangeRed : Colors.Yellow;
                    }
                    else
                    {
                        PhaseLabel.Text = $"BATTLE ROYALE — {ArenaZoneManager.Instance.ZoneStatusMessage}";
                        PhaseLabel.Modulate = ArenaZoneManager.Instance.IsShrinking ? Colors.Orange : Colors.Red;
                    }
                }
                else
                {
                    PhaseLabel.Text = "BATTLE ROYALE - FIGHT!";
                    PhaseLabel.Modulate = Colors.Red;
                }
                MoneyLabel.Visible = true;
                UpdateMoneyDisplay();
                if (GameOverPanel != null) GameOverPanel.Visible = false;
                break;
            case GamePhase.RoundOver:
                PhaseLabel.Text = $"ROUND {GameManager.Instance.CurrentRound} OVER";
                PhaseLabel.Modulate = Colors.Gold;
                MoneyLabel.Visible = false;
                UpdateGameOverHUD(false);
                break;
            case GamePhase.GameOver:
                PhaseLabel.Text = "MATCH OVER";
                PhaseLabel.Modulate = Colors.Gold;
                MoneyLabel.Visible = false;
                UpdateGameOverHUD(true);
                break;
        }

        float remaining = GameManager.Instance.TimeRemaining;
        TimeSpan time = TimeSpan.FromSeconds(remaining);
        TimerLabel.Text = time.ToString(@"mm\:ss");

        if (_killFeedTimer > 0f)
        {
            _killFeedTimer -= (float)delta;
            if (_killFeedTimer <= 0f && _killFeedLabel != null)
            {
                _killFeedLabel.Visible = false;
            }
        }

        if (_bountyHUDTimer > 0f)
        {
            _bountyHUDTimer -= (float)delta;
            if (_bountyHUDTimer <= 0f && _bountyHUDLabel != null)
            {
                _bountyHUDLabel.Visible = false;
            }
        }

        if (!_subscribedToManager && ManagerAnnouncer.Instance != null)
        {
            ManagerAnnouncer.Instance.ManagerAnnounced += OnManagerAnnounced;
            _subscribedToManager = true;
        }

        if (_intercomDisplayTimer > 0f)
        {
            _intercomDisplayTimer -= (float)delta;
            _intercomPulseTimer += (float)delta * 5.0f;
            if (_intercomLiveDot != null)
            {
                float alpha = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_intercomPulseTimer));
                _intercomLiveDot.Modulate = new Color(1f, 0.2f, 0.2f, alpha);
            }

            if (_intercomDisplayTimer <= 0f)
            {
                HideIntercom();
            }
        }

        UpdateAmbientEventHUD();
    }

    public void ShowEliminationNotification(string killer, string victim)
    {
        _killFeedTimer = 4.0f;
        if (_killFeedLabel != null)
        {
            _killFeedLabel.Text = $"💀 {killer} eliminated {victim}!";
            _killFeedLabel.Visible = true;
        }
    }

    public void ShowBountyNotification(string targetName, int amount)
    {
        _bountyHUDTimer = 6.0f;
        if (_bountyHUDLabel != null)
        {
            _bountyHUDLabel.Text = $"🎯 STORE BOUNTY: ${amount} ON {targetName.ToUpper()}! 🎯";
            _bountyHUDLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.1f));
            _bountyHUDLabel.Visible = true;
        }
    }

    public void ShowBountyClaimedNotification(string killerName, string targetName, int amount)
    {
        _bountyHUDTimer = 6.0f;
        if (_bountyHUDLabel != null)
        {
            _bountyHUDLabel.Text = $"💰 BOUNTY CLAIMED: {killerName.ToUpper()} KILLED {targetName.ToUpper()} (+${amount})! 💰";
            _bountyHUDLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
            _bountyHUDLabel.Visible = true;
        }
    }

    private void UpdateGameOverHUD(bool isGameOver)
    {
        if (GameOverPanel == null) return;

        GameOverPanel.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        string winner = GameManager.Instance.WinnerName;
        bool isDraw = GameManager.Instance.IsDraw;
        string myName = SteamManager.Instance?.GetPersonaName() ?? (PlayerController.Instance != null ? PlayerController.Instance.PlayerName : "");

        string scores = "";
        foreach (var kvp in GameManager.Instance.RoundWins)
        {
            scores += $"Player {kvp.Key}: {kvp.Value} Wins  ";
        }

        if (isDraw)
        {
            if (GameOverHeaderLabel != null)
            {
                GameOverHeaderLabel.Text = isGameOver ? "💀 MATCH OVER 💀" : $"💀 ROUND {GameManager.Instance.CurrentRound} OVER 💀";
                GameOverHeaderLabel.Modulate = new Color(0.95f, 0.3f, 0.3f);
            }
            if (WinnerNameLabel != null)
            {
                WinnerNameLabel.Text = "NO SURVIVORS";
                WinnerNameLabel.Modulate = new Color(0.95f, 0.2f, 0.2f);
            }
            if (SubtitleLabel != null)
            {
                SubtitleLabel.Text = isGameOver ? "All shoppers were eliminated in the store royale!" : "All shoppers were eliminated this round!";
            }
            if (StatsDetailLabel != null)
            {
                StatsDetailLabel.Text = isGameOver ? $"Draw Match — No Winner Declared\n{scores}" : $"Draw Round\n{scores}";
            }
        }
        else if (!string.IsNullOrEmpty(winner) && winner.Equals(myName, StringComparison.OrdinalIgnoreCase))
        {
            if (GameOverHeaderLabel != null)
            {
                GameOverHeaderLabel.Text = isGameOver ? "👑 VICTORY ROYALE 👑" : $"👑 ROUND {GameManager.Instance.CurrentRound} WON 👑";
                GameOverHeaderLabel.Modulate = new Color(1.0f, 0.85f, 0.1f);
            }
            if (WinnerNameLabel != null)
            {
                WinnerNameLabel.Text = "YOU WON!";
                WinnerNameLabel.Modulate = new Color(0.2f, 1.0f, 0.4f);
            }
            if (SubtitleLabel != null)
            {
                SubtitleLabel.Text = isGameOver ? "🎉 You are the Match Champion! 🎉" : "🎉 You won this round! 🎉";
            }
            if (StatsDetailLabel != null)
            {
                StatsDetailLabel.Text = isGameOver ? $"Store Champion: {winner} | Shopping Wars Winner\n{scores}" : $"Round Winner: {winner}\n{scores}";
            }
        }
        else
        {
            if (GameOverHeaderLabel != null)
            {
                GameOverHeaderLabel.Text = isGameOver ? "🏆 MATCH OVER 🏆" : $"🏆 ROUND {GameManager.Instance.CurrentRound} OVER 🏆";
                GameOverHeaderLabel.Modulate = new Color(1.0f, 0.85f, 0.1f);
            }
            if (WinnerNameLabel != null)
            {
                WinnerNameLabel.Text = $"WINNER: {winner.ToUpper()}";
                WinnerNameLabel.Modulate = new Color(0.3f, 0.85f, 1.0f);
            }
            if (SubtitleLabel != null)
            {
                SubtitleLabel.Text = isGameOver ? $"{winner} is the Match Champion!" : $"{winner} won this round!";
            }
            if (StatsDetailLabel != null)
            {
                StatsDetailLabel.Text = isGameOver ? $"Store Champion: {winner}\n{scores}" : $"Round Winner: {winner}\n{scores}";
            }
        }

        if (PlayAgainButton != null)
        {
            PlayAgainButton.Visible = true;
            if (isGameOver)
            {
                if (Multiplayer.IsServer())
                {
                    PlayAgainButton.Text = "🔄 RESTART MATCH";
                    PlayAgainButton.Disabled = false;
                }
                else
                {
                    PlayAgainButton.Text = "WAITING FOR HOST TO RESTART...";
                    PlayAgainButton.Disabled = true;
                }
            }
            else
            {
                int countdown = Mathf.CeilToInt(GameManager.Instance.TimeRemaining);
                if (Multiplayer.IsServer())
                {
                    PlayAgainButton.Text = $"⏩ NEXT ROUND ({countdown}s)";
                    PlayAgainButton.Disabled = false;
                }
                else
                {
                    PlayAgainButton.Text = $"NEXT ROUND IN {countdown}s";
                    PlayAgainButton.Disabled = true;
                }
            }
        }
    }

    private void OnPlayAgainPressed()
    {
        if (Multiplayer.IsServer())
        {
            if (GameManager.Instance?.CurrentPhase == GamePhase.RoundOver)
            {
                GameManager.Instance.AdvanceToNextRound();
            }
            else if (GameManager.Instance?.CurrentPhase == GamePhase.GameOver)
            {
                GameManager.Instance.RestartGame();
            }
        }
    }

    private void OnMainMenuPressed()
    {
        NetworkManager.Instance?.ReturnToMainMenu();
    }

    private void UpdateAmbientEventHUD()
    {
        if (AmbientEventManager.Instance == null) return;

        var activeEvent = AmbientEventManager.Instance.ActiveEvent;
        if (activeEvent != null && GameManager.Instance?.CurrentPhase != GamePhase.GameOver)
        {
            if (EventContainer != null) EventContainer.Visible = true;
            if (EventLabel != null)
            {
                EventLabel.Visible = true;
                int seconds = Mathf.CeilToInt(AmbientEventManager.Instance.ActiveEventTimeRemaining);
                EventLabel.Text = $"⚡ {activeEvent.DisplayName} ({seconds}s) ⚡";
                EventLabel.Modulate = activeEvent.BannerColor;
            }
            if (EventDescLabel != null)
            {
                EventDescLabel.Visible = true;
                EventDescLabel.Text = activeEvent.Description;
            }
        }
        else
        {
            if (EventContainer != null) EventContainer.Visible = false;
            if (EventLabel != null) EventLabel.Visible = false;
            if (EventDescLabel != null) EventDescLabel.Visible = false;
        }
    }

    private Control _tutorialModal;
    private Button _tutorialCloseButton;
    private Label _helpHintLabel;
    private Control _pauseMenuModal;
    public bool IsTutorialOpen => _tutorialModal != null && _tutorialModal.Visible;
    public bool IsPauseMenuOpen => _pauseMenuModal != null && _pauseMenuModal.Visible;
    public bool IsAnyModalOpen => IsTutorialOpen || IsPerkModalOpen || IsPauseMenuOpen;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsTutorialOpen)
        {
            if (@event is InputEventKey tKey && tKey.Pressed && !tKey.Echo)
            {
                if (tKey.Keycode == Key.H || tKey.Keycode == Key.F1 ||
                    tKey.Keycode == Key.Escape || tKey.Keycode == Key.Space ||
                    tKey.Keycode == Key.Enter || tKey.Keycode == Key.KpEnter ||
                    tKey.Keycode == Key.E)
                {
                    CloseTutorial();
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            else if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("interact") || @event.IsActionPressed("jump"))
            {
                CloseTutorial();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.H || key.Keycode == Key.F1)
            {
                ToggleTutorial();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.P)
            {
                TogglePerkModal();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Escape)
            {
                if (IsPauseMenuOpen)
                {
                    ClosePauseMenu();
                    GetViewport().SetInputAsHandled();
                }
                else if (IsPerkModalOpen)
                {
                    ClosePerkModal();
                    GetViewport().SetInputAsHandled();
                }
                else if (IsTutorialOpen)
                {
                    CloseTutorial();
                    GetViewport().SetInputAsHandled();
                }
                else if (GameManager.Instance?.CurrentPhase != GamePhase.GameOver)
                {
                    OpenPauseMenu();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    public void OpenTutorial()
    {
        if (_tutorialModal != null)
        {
            if (IsPauseMenuOpen) ClosePauseMenu();
            if (IsPerkModalOpen) ClosePerkModal();
            _tutorialModal.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _tutorialCloseButton?.CallDeferred(Control.MethodName.GrabFocus);
        }
    }

    public void CloseTutorial()
    {
        if (_tutorialModal != null)
        {
            _tutorialModal.Visible = false;
            if (!IsAnyModalOpen &&
                GameManager.Instance?.CurrentPhase != GamePhase.GameOver &&
                GameManager.Instance?.CurrentPhase != GamePhase.RoundOver)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    public void ToggleTutorial()
    {
        if (IsTutorialOpen)
        {
            CloseTutorial();
        }
        else
        {
            OpenTutorial();
        }
    }

    public void OpenPauseMenu()
    {
        if (_pauseMenuModal != null)
        {
            if (IsTutorialOpen) CloseTutorial();
            if (IsPerkModalOpen) ClosePerkModal();
            RefreshPauseMenuSettings();
            _pauseMenuModal.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void ClosePauseMenu()
    {
        if (_pauseMenuModal != null)
        {
            _pauseMenuModal.Visible = false;
            if (!IsAnyModalOpen &&
                GameManager.Instance?.CurrentPhase != GamePhase.GameOver &&
                GameManager.Instance?.CurrentPhase != GamePhase.RoundOver)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    public void TogglePauseMenu()
    {
        if (IsPauseMenuOpen)
        {
            ClosePauseMenu();
        }
        else
        {
            OpenPauseMenu();
        }
    }

    private void BuildTutorialModal()
    {
        // Persistent hint label in bottom-left corner
        _helpHintLabel = new Label();
        _helpHintLabel.Name = "HelpHintLabel";
        _helpHintLabel.Text = "[H] How to Play & Controls";
        _helpHintLabel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _helpHintLabel.OffsetLeft = 20;
        _helpHintLabel.OffsetTop = -35;
        _helpHintLabel.OffsetRight = 260;
        _helpHintLabel.OffsetBottom = -10;
        _helpHintLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.75f));
        _helpHintLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _helpHintLabel.AddThemeConstantOverride("outline_size", 3);
        _helpHintLabel.AddThemeFontSizeOverride("font_size", 13);
        AddChild(_helpHintLabel);

        // Dark modal overlay backdrop
        _tutorialModal = new Panel();
        _tutorialModal.Name = "TutorialModal";
        _tutorialModal.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _tutorialModal.MouseFilter = Control.MouseFilterEnum.Stop;
        _tutorialModal.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                CloseTutorial();
            }
        };

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.04f, 0.05f, 0.08f, 0.90f);
        _tutorialModal.AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Pass;
        _tutorialModal.AddChild(center);

        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(820, 540);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.AddThemeStyleboxOverride("panel", OnboardingContent.CreateCardStyle());
        center.AddChild(card);

        var vbox = OnboardingContent.BuildCardBody();
        card.AddChild(vbox);

        // Footer button & tip
        var footer = new VBoxContainer();
        footer.AddThemeConstantOverride("separation", 6);
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(footer);

        var btnContainer = new CenterContainer();
        footer.AddChild(btnContainer);

        var closeBtn = new Button();
        closeBtn.Text = "✅ GOT IT! LET'S SHOP  [Space / Enter]";
        closeBtn.CustomMinimumSize = new Vector2(300, 44);
        closeBtn.AddThemeFontSizeOverride("font_size", 16);
        closeBtn.FocusMode = Control.FocusModeEnum.All;
        closeBtn.MouseFilter = Control.MouseFilterEnum.Stop;

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
        closeBtn.AddThemeStyleboxOverride("normal", btnStyle);

        var hoverStyle = (StyleBoxFlat)btnStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.20f, 0.55f, 0.30f, 1.0f);
        hoverStyle.BorderColor = new Color(0.5f, 1.0f, 0.6f, 1.0f);
        closeBtn.AddThemeStyleboxOverride("hover", hoverStyle);

        var focusStyle = (StyleBoxFlat)btnStyle.Duplicate();
        focusStyle.BorderColor = Colors.White;
        focusStyle.BorderWidthLeft = 3;
        focusStyle.BorderWidthTop = 3;
        focusStyle.BorderWidthRight = 3;
        focusStyle.BorderWidthBottom = 3;
        closeBtn.AddThemeStyleboxOverride("focus", focusStyle);

        closeBtn.Pressed += CloseTutorial;
        btnContainer.AddChild(closeBtn);
        _tutorialCloseButton = closeBtn;

        var tipLbl = new Label();
        tipLbl.Text = "Press [H] anytime during the match to toggle this guide!";
        tipLbl.HorizontalAlignment = HorizontalAlignment.Center;
        tipLbl.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 0.8f));
        tipLbl.AddThemeFontSizeOverride("font_size", 11);
        footer.AddChild(tipLbl);

        AddChild(_tutorialModal);

        // No auto-open: onboarding now happens in the main menu popup before
        // hosting/joining. This modal is the in-game [H] reference guide only.
        _tutorialModal.Visible = false;
    }

    private Control _perkModal;
    private Label _perkBadgeLabel;
    private readonly System.Collections.Generic.Dictionary<PlayerPerk, Button> _perkButtons = new();
    private readonly System.Collections.Generic.Dictionary<PlayerPerk, PanelContainer> _perkCards = new();

    public bool IsPerkModalOpen => _perkModal != null && _perkModal.Visible;

    public void OpenPerkModal()
    {
        if (_perkModal != null)
        {
            if (IsTutorialOpen) CloseTutorial();
            if (IsPauseMenuOpen) ClosePauseMenu();
            UpdatePerkDisplay(PlayerController.Instance?.CurrentPerk ?? PlayerPerk.None);
            _perkModal.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void ClosePerkModal()
    {
        if (_perkModal != null)
        {
            _perkModal.Visible = false;
            if (!IsAnyModalOpen &&
                GameManager.Instance?.CurrentPhase != GamePhase.GameOver &&
                GameManager.Instance?.CurrentPhase != GamePhase.RoundOver)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    public void TogglePerkModal()
    {
        if (IsPerkModalOpen)
        {
            ClosePerkModal();
        }
        else
        {
            OpenPerkModal();
        }
    }

    private void SelectPerk(PlayerPerk perk)
    {
        bool canChange = GameManager.Instance == null || 
                         GameManager.Instance.CurrentPhase == GamePhase.Lobby || 
                         GameManager.Instance.CurrentPhase == GamePhase.ShoppingTransition || 
                         GameManager.Instance.CurrentPhase == GamePhase.Shopping;

        if (!canChange)
        {
            GD.Print("[Perk] Perks are locked during Battle Royale!");
            return;
        }

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.SetPerk(perk);
        }
        UpdatePerkDisplay(perk);
    }

    public void UpdatePerkDisplay(PlayerPerk perk)
    {
        if (_perkBadgeLabel != null)
        {
            if (PerkDatabase.Perks.TryGetValue(perk, out var activeDef))
            {
                _perkBadgeLabel.Text = $"[P] Perk: {activeDef.Icon} {activeDef.Name} ({activeDef.Tagline})";
                _perkBadgeLabel.AddThemeColorOverride("font_color", activeDef.ThemeColor);
            }
            else
            {
                _perkBadgeLabel.Text = "[P] Perk: None (Press [P] to Choose)";
                _perkBadgeLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
            }
        }

        bool isLocked = GameManager.Instance != null && 
                        (GameManager.Instance.CurrentPhase == GamePhase.BattleTransition || 
                         GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale);

        foreach (var kvp in _perkButtons)
        {
            var p = kvp.Key;
            var btn = kvp.Value;
            bool isCurrent = (p == perk);

            if (isLocked)
            {
                btn.Disabled = !isCurrent;
                btn.Text = isCurrent ? "✅ ACTIVE" : "LOCKED";
            }
            else
            {
                btn.Disabled = false;
                btn.Text = isCurrent ? "✅ EQUIPPED" : "SELECT PERK";
            }

            if (_perkCards.TryGetValue(p, out var card))
            {
                if (PerkDatabase.Perks.TryGetValue(p, out var def))
                {
                    var cardStyle = new StyleBoxFlat();
                    cardStyle.BgColor = isCurrent ? new Color(0.12f, 0.22f, 0.18f, 0.95f) : new Color(0.10f, 0.13f, 0.20f, 0.95f);
                    cardStyle.BorderWidthLeft = isCurrent ? 3 : 2;
                    cardStyle.BorderWidthTop = isCurrent ? 3 : 2;
                    cardStyle.BorderWidthRight = isCurrent ? 3 : 2;
                    cardStyle.BorderWidthBottom = isCurrent ? 3 : 2;
                    cardStyle.BorderColor = isCurrent ? new Color(0.3f, 1.0f, 0.4f) : def.ThemeColor;
                    cardStyle.CornerRadiusTopLeft = 8;
                    cardStyle.CornerRadiusTopRight = 8;
                    cardStyle.CornerRadiusBottomLeft = 8;
                    cardStyle.CornerRadiusBottomRight = 8;
                    cardStyle.ContentMarginLeft = 12;
                    cardStyle.ContentMarginTop = 10;
                    cardStyle.ContentMarginRight = 12;
                    cardStyle.ContentMarginBottom = 10;
                    card.AddThemeStyleboxOverride("panel", cardStyle);
                }
            }
        }
    }

    private void OnGamePhaseChanged()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.CurrentPhase == GamePhase.ShoppingTransition)
        {
            if (PlayerController.Instance != null && PlayerController.Instance.CurrentPerk == PlayerPerk.None)
            {
                OpenPerkModal();
            }
        }
        else if (GameManager.Instance.CurrentPhase == GamePhase.BattleRoyale)
        {
            if (IsPerkModalOpen)
            {
                ClosePerkModal();
            }
        }

        UpdatePerkDisplay(PlayerController.Instance?.CurrentPerk ?? PlayerPerk.None);
    }

    private void BuildPerkModal()
    {
        // Persistent hint label in bottom-left corner right above the help hint
        _perkBadgeLabel = new Label();
        _perkBadgeLabel.Name = "PerkBadgeLabel";
        _perkBadgeLabel.Text = "[P] Perk: None (Press [P] to Choose)";
        _perkBadgeLabel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _perkBadgeLabel.OffsetLeft = 20;
        _perkBadgeLabel.OffsetTop = -65;
        _perkBadgeLabel.OffsetRight = 450;
        _perkBadgeLabel.OffsetBottom = -40;
        _perkBadgeLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
        _perkBadgeLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _perkBadgeLabel.AddThemeConstantOverride("outline_size", 3);
        _perkBadgeLabel.AddThemeFontSizeOverride("font_size", 13);
        AddChild(_perkBadgeLabel);

        // Dark modal overlay backdrop
        _perkModal = new Panel();
        _perkModal.Name = "PerkModal";
        _perkModal.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _perkModal.MouseFilter = Control.MouseFilterEnum.Stop;

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        _perkModal.AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Pass;
        _perkModal.AddChild(center);

        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(880, 560);
        var cardStyle = new StyleBoxFlat();
        cardStyle.BgColor = new Color(0.07f, 0.09f, 0.14f, 0.98f);
        cardStyle.BorderWidthLeft = 3;
        cardStyle.BorderWidthTop = 3;
        cardStyle.BorderWidthRight = 3;
        cardStyle.BorderWidthBottom = 3;
        cardStyle.BorderColor = new Color(1.0f, 0.82f, 0.2f, 0.95f);
        cardStyle.CornerRadiusTopLeft = 14;
        cardStyle.CornerRadiusTopRight = 14;
        cardStyle.CornerRadiusBottomLeft = 14;
        cardStyle.CornerRadiusBottomRight = 14;
        cardStyle.ExpandMarginLeft = 24;
        cardStyle.ExpandMarginTop = 18;
        cardStyle.ExpandMarginRight = 24;
        cardStyle.ExpandMarginBottom = 18;
        cardStyle.ShadowSize = 24;
        cardStyle.ShadowColor = new Color(0, 0, 0, 0.75f);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        card.AddChild(vbox);

        // Header
        var title = new Label();
        title.Text = "⭐ CHOOSE YOUR SHOPPER PERK ⭐";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.15f));
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        title.AddThemeConstantOverride("outline_size", 4);
        title.AddThemeFontSizeOverride("font_size", 24);
        vbox.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = "Equip 1 passive perk to customize your playstyle! Choose before Battle Royale begins.";
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 0.9f));
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(subtitle);

        vbox.AddChild(new HSeparator());

        // 3x2 Grid of Perks
        var grid = new GridContainer();
        grid.Columns = 3;
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 16);
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(grid);

        foreach (var perkPair in PerkDatabase.Perks)
        {
            PlayerPerk perkKey = perkPair.Key;
            PerkDefinition def = perkPair.Value;

            var perkCard = new PanelContainer();
            perkCard.CustomMinimumSize = new Vector2(265, 145);
            perkCard.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            perkCard.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            var pStyle = new StyleBoxFlat();
            pStyle.BgColor = new Color(0.10f, 0.13f, 0.20f, 0.95f);
            pStyle.BorderWidthLeft = 2;
            pStyle.BorderWidthTop = 2;
            pStyle.BorderWidthRight = 2;
            pStyle.BorderWidthBottom = 2;
            pStyle.BorderColor = def.ThemeColor;
            pStyle.CornerRadiusTopLeft = 8;
            pStyle.CornerRadiusTopRight = 8;
            pStyle.CornerRadiusBottomLeft = 8;
            pStyle.CornerRadiusBottomRight = 8;
            pStyle.ContentMarginLeft = 12;
            pStyle.ContentMarginTop = 10;
            pStyle.ContentMarginRight = 12;
            pStyle.ContentMarginBottom = 10;
            perkCard.AddThemeStyleboxOverride("panel", pStyle);

            var cVbox = new VBoxContainer();
            cVbox.AddThemeConstantOverride("separation", 4);
            perkCard.AddChild(cVbox);

            var nameLabel = new Label();
            nameLabel.Text = $"{def.Icon} {def.Name}";
            nameLabel.AddThemeColorOverride("font_color", def.ThemeColor);
            nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            nameLabel.AddThemeConstantOverride("outline_size", 2);
            nameLabel.AddThemeFontSizeOverride("font_size", 16);
            cVbox.AddChild(nameLabel);

            var tagLabel = new Label();
            tagLabel.Text = def.Tagline;
            tagLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.85f, 1.0f));
            tagLabel.AddThemeFontSizeOverride("font_size", 11);
            cVbox.AddChild(tagLabel);

            var descLabel = new Label();
            descLabel.Text = def.Description;
            descLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.88f, 0.92f));
            descLabel.AddThemeFontSizeOverride("font_size", 11);
            descLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            descLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            cVbox.AddChild(descLabel);

            var selectBtn = new Button();
            selectBtn.Text = "SELECT PERK";
            selectBtn.CustomMinimumSize = new Vector2(0, 30);
            selectBtn.AddThemeFontSizeOverride("font_size", 12);

            var btnNorm = new StyleBoxFlat();
            btnNorm.BgColor = new Color(0.18f, 0.24f, 0.38f, 0.95f);
            btnNorm.BorderColor = def.ThemeColor;
            btnNorm.BorderWidthLeft = 1;
            btnNorm.BorderWidthTop = 1;
            btnNorm.BorderWidthRight = 1;
            btnNorm.BorderWidthBottom = 1;
            btnNorm.CornerRadiusTopLeft = 6;
            btnNorm.CornerRadiusTopRight = 6;
            btnNorm.CornerRadiusBottomLeft = 6;
            btnNorm.CornerRadiusBottomRight = 6;
            selectBtn.AddThemeStyleboxOverride("normal", btnNorm);

            selectBtn.Pressed += () => SelectPerk(perkKey);
            cVbox.AddChild(selectBtn);

            grid.AddChild(perkCard);
            _perkCards[perkKey] = perkCard;
            _perkButtons[perkKey] = selectBtn;
        }

        vbox.AddChild(new HSeparator());

        // Footer
        var footer = new VBoxContainer();
        footer.AddThemeConstantOverride("separation", 6);
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(footer);

        var btnCenter = new CenterContainer();
        footer.AddChild(btnCenter);

        var closeBtn = new Button();
        closeBtn.Text = "✅ CONFIRM & CLOSE [P]";
        closeBtn.CustomMinimumSize = new Vector2(240, 38);
        closeBtn.AddThemeFontSizeOverride("font_size", 15);

        var closeStyle = new StyleBoxFlat();
        closeStyle.BgColor = new Color(0.14f, 0.42f, 0.22f, 0.95f);
        closeStyle.BorderColor = new Color(0.35f, 0.95f, 0.45f, 0.9f);
        closeStyle.BorderWidthLeft = 2;
        closeStyle.BorderWidthTop = 2;
        closeStyle.BorderWidthRight = 2;
        closeStyle.BorderWidthBottom = 2;
        closeStyle.CornerRadiusTopLeft = 8;
        closeStyle.CornerRadiusTopRight = 8;
        closeStyle.CornerRadiusBottomLeft = 8;
        closeStyle.CornerRadiusBottomRight = 8;
        closeBtn.AddThemeStyleboxOverride("normal", closeStyle);

        var closeHover = (StyleBoxFlat)closeStyle.Duplicate();
        closeHover.BgColor = new Color(0.20f, 0.55f, 0.30f, 1.0f);
        closeBtn.AddThemeStyleboxOverride("hover", closeHover);

        closeBtn.Pressed += ClosePerkModal;
        btnCenter.AddChild(closeBtn);

        var footerTip = new Label();
        footerTip.Text = "Press [P] or [Esc] to close. Perks lock in when Battle Royale begins!";
        footerTip.HorizontalAlignment = HorizontalAlignment.Center;
        footerTip.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 0.8f));
        footerTip.AddThemeFontSizeOverride("font_size", 11);
        footer.AddChild(footerTip);

        AddChild(_perkModal);
        _perkModal.Visible = false;
    }

    private HSlider _masterSlider;
    private Label _masterValLabel;
    private HSlider _sfxSlider;
    private Label _sfxValLabel;
    private HSlider _musicSlider;
    private Label _musicValLabel;
    private HSlider _sensSlider;
    private Label _sensValLabel;
    private HSlider _fovSlider;
    private Label _fovValLabel;
    private Button _fullscreenBtn;
    private Button _vsyncBtn;
    private Button _presetLowBtn;
    private Button _presetMedBtn;
    private Button _presetHighBtn;
    private Button _presetUltraBtn;
    private Label _vramLabel;
    private Button _aaBtn;
    private Button _shadowBtn;
    private Button _ssrBtn;
    private Button _fogBtn;

    private void RefreshPauseMenuSettings()
    {
        if (_masterSlider != null) _masterSlider.Value = (int)(SettingsManager.MasterVolume * 100);
        if (_masterValLabel != null) _masterValLabel.Text = $"{(int)(SettingsManager.MasterVolume * 100)}%";
        if (_sfxSlider != null) _sfxSlider.Value = (int)(SettingsManager.SfxVolume * 100);
        if (_sfxValLabel != null) _sfxValLabel.Text = $"{(int)(SettingsManager.SfxVolume * 100)}%";
        if (_musicSlider != null) _musicSlider.Value = (int)(SettingsManager.MusicVolume * 100);
        if (_musicValLabel != null) _musicValLabel.Text = $"{(int)(SettingsManager.MusicVolume * 100)}%";
        if (_sensSlider != null) _sensSlider.Value = SettingsManager.MouseSensitivity;
        if (_sensValLabel != null) _sensValLabel.Text = $"{SettingsManager.MouseSensitivity:0.00}x";
        if (_fovSlider != null) _fovSlider.Value = (int)SettingsManager.Fov;
        if (_fovValLabel != null) _fovValLabel.Text = $"{(int)SettingsManager.Fov}°";
        if (_fullscreenBtn != null) _fullscreenBtn.Text = SettingsManager.IsFullscreen ? "🖥️ Fullscreen: ON" : "🖥️ Fullscreen: OFF";
        if (_vsyncBtn != null) _vsyncBtn.Text = SettingsManager.IsVsync ? "⚡ V-Sync: ON" : "⚡ V-Sync: OFF";

        UpdateGraphicsSettingsUI();
    }

    private void UpdateGraphicsSettingsUI()
    {
        var curPreset = SettingsManager.CurrentPreset;
        if (_presetLowBtn != null) StylePresetButton(_presetLowBtn, curPreset == SettingsManager.GraphicsPreset.Low, SettingsManager.GetPresetColor(SettingsManager.GraphicsPreset.Low));
        if (_presetMedBtn != null) StylePresetButton(_presetMedBtn, curPreset == SettingsManager.GraphicsPreset.Medium, SettingsManager.GetPresetColor(SettingsManager.GraphicsPreset.Medium));
        if (_presetHighBtn != null) StylePresetButton(_presetHighBtn, curPreset == SettingsManager.GraphicsPreset.High, SettingsManager.GetPresetColor(SettingsManager.GraphicsPreset.High));
        if (_presetUltraBtn != null) StylePresetButton(_presetUltraBtn, curPreset == SettingsManager.GraphicsPreset.Ultra, SettingsManager.GetPresetColor(SettingsManager.GraphicsPreset.Ultra));

        if (_vramLabel != null)
        {
            _vramLabel.Text = SettingsManager.GetEstimatedVramString();
            _vramLabel.AddThemeColorOverride("font_color", SettingsManager.GetPresetColor(curPreset));
        }

        if (_aaBtn != null) _aaBtn.Text = SettingsManager.GetAntiAliasingLabel(SettingsManager.CurrentAntiAliasing);
        if (_shadowBtn != null) _shadowBtn.Text = SettingsManager.GetShadowQualityLabel(SettingsManager.CurrentShadowQuality);
        if (_ssrBtn != null) _ssrBtn.Text = SettingsManager.GetSsrQualityLabel(SettingsManager.CurrentSsr);
        if (_fogBtn != null) _fogBtn.Text = SettingsManager.GetVolumetricFogLabel(SettingsManager.CurrentVolumetricFog);
    }

    private void StylePresetButton(Button btn, bool isSelected, Color activeColor)
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

    private void BuildPauseMenu()
    {
        _pauseMenuModal = new Panel();
        _pauseMenuModal.Name = "PauseMenuModal";
        _pauseMenuModal.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pauseMenuModal.MouseFilter = Control.MouseFilterEnum.Stop;

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.03f, 0.04f, 0.07f, 0.90f);
        _pauseMenuModal.AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Pass;
        _pauseMenuModal.AddChild(center);

        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(660, 620);
        var cardStyle = new StyleBoxFlat();
        cardStyle.BgColor = new Color(0.07f, 0.09f, 0.14f, 0.98f);
        cardStyle.BorderWidthLeft = 3;
        cardStyle.BorderWidthTop = 3;
        cardStyle.BorderWidthRight = 3;
        cardStyle.BorderWidthBottom = 3;
        cardStyle.BorderColor = new Color(0.25f, 0.75f, 1.0f, 0.95f);
        cardStyle.CornerRadiusTopLeft = 14;
        cardStyle.CornerRadiusTopRight = 14;
        cardStyle.CornerRadiusBottomLeft = 14;
        cardStyle.CornerRadiusBottomRight = 14;
        cardStyle.ExpandMarginLeft = 24;
        cardStyle.ExpandMarginTop = 18;
        cardStyle.ExpandMarginRight = 24;
        cardStyle.ExpandMarginBottom = 18;
        cardStyle.ShadowSize = 28;
        cardStyle.ShadowColor = new Color(0, 0, 0, 0.8f);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        var rootVBox = new VBoxContainer();
        rootVBox.AddThemeConstantOverride("separation", 8);
        card.AddChild(rootVBox);

        // Header
        var title = new Label();
        title.Text = "⏸️ MATCH SETTINGS";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 1.0f));
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        title.AddThemeConstantOverride("outline_size", 4);
        title.AddThemeFontSizeOverride("font_size", 24);
        rootVBox.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = "⚠️ Live multiplayer match in progress — gameplay does NOT pause!";
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.25f));
        subtitle.AddThemeFontSizeOverride("font_size", 12);
        rootVBox.AddChild(subtitle);

        rootVBox.AddChild(new HSeparator());

        // Scrollable settings area
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        rootVBox.AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);

        // Audio Section
        var audioHeader = new Label();
        audioHeader.Text = "🔊 AUDIO SETTINGS";
        audioHeader.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 1.0f));
        audioHeader.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(audioHeader);

        _masterSlider = CreateSliderRow(vbox, "Master Volume", (int)(SettingsManager.MasterVolume * 100), "%", out _masterValLabel);
        _masterSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetMasterVolume((float)v / 100f);
            if (_masterValLabel != null) _masterValLabel.Text = $"{(int)v}%";
        };

        _sfxSlider = CreateSliderRow(vbox, "SFX & Hazards", (int)(SettingsManager.SfxVolume * 100), "%", out _sfxValLabel);
        _sfxSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetSfxVolume((float)v / 100f);
            if (_sfxValLabel != null) _sfxValLabel.Text = $"{(int)v}%";
        };

        _musicSlider = CreateSliderRow(vbox, "Music & Ambience", (int)(SettingsManager.MusicVolume * 100), "%", out _musicValLabel);
        _musicSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetMusicVolume((float)v / 100f);
            if (_musicValLabel != null) _musicValLabel.Text = $"{(int)v}%";
        };

        vbox.AddChild(new HSeparator());

        // Controls & Camera Section
        var controlsHeader = new Label();
        controlsHeader.Text = "🎮 CONTROLS & CAMERA";
        controlsHeader.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
        controlsHeader.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(controlsHeader);

        _sensSlider = CreateFloatSliderRow(vbox, "Mouse Sensitivity", SettingsManager.MouseSensitivity, 0.2f, 3.0f, 0.05f, "x", out _sensValLabel);
        _sensSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetMouseSensitivity((float)v);
            if (_sensValLabel != null) _sensValLabel.Text = $"{v:0.00}x";
        };

        _fovSlider = CreateSliderRow(vbox, "Field of View (FOV)", (int)SettingsManager.Fov, "°", out _fovValLabel, 70, 110);
        _fovSlider.ValueChanged += (v) =>
        {
            SettingsManager.SetFov((float)v);
            if (_fovValLabel != null) _fovValLabel.Text = $"{(int)v}°";
        };

        vbox.AddChild(new HSeparator());

        // Display Section
        var displayHeader = new Label();
        displayHeader.Text = "🖥️ DISPLAY";
        displayHeader.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 1.0f));
        displayHeader.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(displayHeader);

        var displayRow = new HBoxContainer();
        displayRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(displayRow);

        _fullscreenBtn = new Button();
        _fullscreenBtn.Text = SettingsManager.IsFullscreen ? "🖥️ Fullscreen: ON" : "🖥️ Fullscreen: OFF";
        _fullscreenBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _fullscreenBtn.CustomMinimumSize = new Vector2(0, 34);
        _fullscreenBtn.AddThemeFontSizeOverride("font_size", 12);
        _fullscreenBtn.Pressed += () =>
        {
            SettingsManager.SetFullscreen(!SettingsManager.IsFullscreen);
            _fullscreenBtn.Text = SettingsManager.IsFullscreen ? "🖥️ Fullscreen: ON" : "🖥️ Fullscreen: OFF";
        };
        displayRow.AddChild(_fullscreenBtn);

        _vsyncBtn = new Button();
        _vsyncBtn.Text = SettingsManager.IsVsync ? "⚡ V-Sync: ON" : "⚡ V-Sync: OFF";
        _vsyncBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _vsyncBtn.CustomMinimumSize = new Vector2(0, 34);
        _vsyncBtn.AddThemeFontSizeOverride("font_size", 12);
        _vsyncBtn.Pressed += () =>
        {
            SettingsManager.SetVsync(!SettingsManager.IsVsync);
            _vsyncBtn.Text = SettingsManager.IsVsync ? "⚡ V-Sync: ON" : "⚡ V-Sync: OFF";
        };
        displayRow.AddChild(_vsyncBtn);

        vbox.AddChild(new HSeparator());

        // Graphics & Performance Section
        var graphicsHeader = new Label();
        graphicsHeader.Text = "🎨 GRAPHICS & PERFORMANCE";
        graphicsHeader.AddThemeColorOverride("font_color", new Color(0.3f, 0.95f, 0.8f));
        graphicsHeader.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(graphicsHeader);

        // Presets row
        var presetsContainer = new VBoxContainer();
        presetsContainer.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(presetsContainer);

        var presetsRow = new HBoxContainer();
        presetsRow.AddThemeConstantOverride("separation", 8);
        presetsContainer.AddChild(presetsRow);

        _presetLowBtn = new Button();
        _presetLowBtn.Text = "🟢 LOW";
        _presetLowBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _presetLowBtn.CustomMinimumSize = new Vector2(0, 34);
        _presetLowBtn.AddThemeFontSizeOverride("font_size", 12);
        _presetLowBtn.Pressed += () =>
        {
            SettingsManager.SetGraphicsPreset(SettingsManager.GraphicsPreset.Low);
            UpdateGraphicsSettingsUI();
        };
        presetsRow.AddChild(_presetLowBtn);

        _presetMedBtn = new Button();
        _presetMedBtn.Text = "🟡 MEDIUM";
        _presetMedBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _presetMedBtn.CustomMinimumSize = new Vector2(0, 34);
        _presetMedBtn.AddThemeFontSizeOverride("font_size", 12);
        _presetMedBtn.Pressed += () =>
        {
            SettingsManager.SetGraphicsPreset(SettingsManager.GraphicsPreset.Medium);
            UpdateGraphicsSettingsUI();
        };
        presetsRow.AddChild(_presetMedBtn);

        _presetHighBtn = new Button();
        _presetHighBtn.Text = "🔵 HIGH";
        _presetHighBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _presetHighBtn.CustomMinimumSize = new Vector2(0, 34);
        _presetHighBtn.AddThemeFontSizeOverride("font_size", 12);
        _presetHighBtn.Pressed += () =>
        {
            SettingsManager.SetGraphicsPreset(SettingsManager.GraphicsPreset.High);
            UpdateGraphicsSettingsUI();
        };
        presetsRow.AddChild(_presetHighBtn);

        _presetUltraBtn = new Button();
        _presetUltraBtn.Text = "🟣 ULTRA";
        _presetUltraBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _presetUltraBtn.CustomMinimumSize = new Vector2(0, 34);
        _presetUltraBtn.AddThemeFontSizeOverride("font_size", 12);
        _presetUltraBtn.Pressed += () =>
        {
            SettingsManager.SetGraphicsPreset(SettingsManager.GraphicsPreset.Ultra);
            UpdateGraphicsSettingsUI();
        };
        presetsRow.AddChild(_presetUltraBtn);

        _vramLabel = new Label();
        _vramLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _vramLabel.AddThemeFontSizeOverride("font_size", 11);
        presetsContainer.AddChild(_vramLabel);

        // Advanced Quality Toggles (2x2 Grid)
        var advancedGrid = new GridContainer();
        advancedGrid.Columns = 2;
        advancedGrid.AddThemeConstantOverride("h_separation", 10);
        advancedGrid.AddThemeConstantOverride("v_separation", 8);
        vbox.AddChild(advancedGrid);

        _aaBtn = new Button();
        _aaBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _aaBtn.CustomMinimumSize = new Vector2(0, 32);
        _aaBtn.AddThemeFontSizeOverride("font_size", 11);
        _aaBtn.Pressed += () =>
        {
            var next = SettingsManager.CurrentAntiAliasing switch
            {
                SettingsManager.AntiAliasingMode.Off => SettingsManager.AntiAliasingMode.Fxaa,
                SettingsManager.AntiAliasingMode.Fxaa => SettingsManager.AntiAliasingMode.Taa,
                SettingsManager.AntiAliasingMode.Taa => SettingsManager.AntiAliasingMode.TaaAndSmaa,
                _ => SettingsManager.AntiAliasingMode.Off
            };
            SettingsManager.SetAntiAliasing(next);
            UpdateGraphicsSettingsUI();
        };
        advancedGrid.AddChild(_aaBtn);

        _shadowBtn = new Button();
        _shadowBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _shadowBtn.CustomMinimumSize = new Vector2(0, 32);
        _shadowBtn.AddThemeFontSizeOverride("font_size", 11);
        _shadowBtn.Pressed += () =>
        {
            var next = SettingsManager.CurrentShadowQuality switch
            {
                SettingsManager.ShadowQualityLevel.Low => SettingsManager.ShadowQualityLevel.Medium,
                SettingsManager.ShadowQualityLevel.Medium => SettingsManager.ShadowQualityLevel.High,
                SettingsManager.ShadowQualityLevel.High => SettingsManager.ShadowQualityLevel.Ultra,
                _ => SettingsManager.ShadowQualityLevel.Low
            };
            SettingsManager.SetShadowQuality(next);
            UpdateGraphicsSettingsUI();
        };
        advancedGrid.AddChild(_shadowBtn);

        _ssrBtn = new Button();
        _ssrBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _ssrBtn.CustomMinimumSize = new Vector2(0, 32);
        _ssrBtn.AddThemeFontSizeOverride("font_size", 11);
        _ssrBtn.Pressed += () =>
        {
            var next = SettingsManager.CurrentSsr switch
            {
                SettingsManager.QualityLevel.Off => SettingsManager.QualityLevel.Low,
                SettingsManager.QualityLevel.Low => SettingsManager.QualityLevel.Medium,
                SettingsManager.QualityLevel.Medium => SettingsManager.QualityLevel.High,
                _ => SettingsManager.QualityLevel.Off
            };
            SettingsManager.SetSsrQuality(next);
            UpdateGraphicsSettingsUI();
        };
        advancedGrid.AddChild(_ssrBtn);

        _fogBtn = new Button();
        _fogBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _fogBtn.CustomMinimumSize = new Vector2(0, 32);
        _fogBtn.AddThemeFontSizeOverride("font_size", 11);
        _fogBtn.Pressed += () =>
        {
            var next = SettingsManager.CurrentVolumetricFog switch
            {
                SettingsManager.QualityLevel.Off => SettingsManager.QualityLevel.Low,
                SettingsManager.QualityLevel.Low => SettingsManager.QualityLevel.High,
                _ => SettingsManager.QualityLevel.Off
            };
            SettingsManager.SetVolumetricFog(next);
            UpdateGraphicsSettingsUI();
        };
        advancedGrid.AddChild(_fogBtn);

        UpdateGraphicsSettingsUI();

        rootVBox.AddChild(new HSeparator());

        // Actions Row
        var actionsRow = new HBoxContainer();
        actionsRow.AddThemeConstantOverride("separation", 12);
        rootVBox.AddChild(actionsRow);

        var resumeBtn = new Button();
        resumeBtn.Text = "▶️ RESUME [Esc]";
        resumeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        resumeBtn.CustomMinimumSize = new Vector2(0, 40);
        resumeBtn.AddThemeFontSizeOverride("font_size", 14);

        var resumeStyle = new StyleBoxFlat();
        resumeStyle.BgColor = new Color(0.14f, 0.42f, 0.22f, 0.95f);
        resumeStyle.BorderColor = new Color(0.35f, 0.95f, 0.45f, 0.9f);
        resumeStyle.BorderWidthLeft = 2;
        resumeStyle.BorderWidthTop = 2;
        resumeStyle.BorderWidthRight = 2;
        resumeStyle.BorderWidthBottom = 2;
        resumeStyle.CornerRadiusTopLeft = 8;
        resumeStyle.CornerRadiusTopRight = 8;
        resumeStyle.CornerRadiusBottomLeft = 8;
        resumeStyle.CornerRadiusBottomRight = 8;
        resumeBtn.AddThemeStyleboxOverride("normal", resumeStyle);

        var resumeHover = (StyleBoxFlat)resumeStyle.Duplicate();
        resumeHover.BgColor = new Color(0.20f, 0.55f, 0.30f, 1.0f);
        resumeBtn.AddThemeStyleboxOverride("hover", resumeHover);
        resumeBtn.Pressed += ClosePauseMenu;
        actionsRow.AddChild(resumeBtn);

        var guideBtn = new Button();
        guideBtn.Text = "📖 GUIDE [H]";
        guideBtn.CustomMinimumSize = new Vector2(110, 40);
        guideBtn.AddThemeFontSizeOverride("font_size", 12);
        guideBtn.Pressed += () =>
        {
            ClosePauseMenu();
            OpenTutorial();
        };
        actionsRow.AddChild(guideBtn);

        var perksBtn = new Button();
        perksBtn.Text = "⭐ PERKS [P]";
        perksBtn.CustomMinimumSize = new Vector2(110, 40);
        perksBtn.AddThemeFontSizeOverride("font_size", 12);
        perksBtn.Pressed += () =>
        {
            ClosePauseMenu();
            OpenPerkModal();
        };
        actionsRow.AddChild(perksBtn);

        var leaveBtn = new Button();
        leaveBtn.Text = "🚪 LEAVE";
        leaveBtn.CustomMinimumSize = new Vector2(100, 40);
        leaveBtn.AddThemeFontSizeOverride("font_size", 12);

        var leaveStyle = new StyleBoxFlat();
        leaveStyle.BgColor = new Color(0.45f, 0.12f, 0.12f, 0.95f);
        leaveStyle.BorderColor = new Color(0.95f, 0.3f, 0.3f, 0.9f);
        leaveStyle.BorderWidthLeft = 2;
        leaveStyle.BorderWidthTop = 2;
        leaveStyle.BorderWidthRight = 2;
        leaveStyle.BorderWidthBottom = 2;
        leaveStyle.CornerRadiusTopLeft = 8;
        leaveStyle.CornerRadiusTopRight = 8;
        leaveStyle.CornerRadiusBottomLeft = 8;
        leaveStyle.CornerRadiusBottomRight = 8;
        leaveBtn.AddThemeStyleboxOverride("normal", leaveStyle);

        var leaveHover = (StyleBoxFlat)leaveStyle.Duplicate();
        leaveHover.BgColor = new Color(0.60f, 0.15f, 0.15f, 1.0f);
        leaveBtn.AddThemeStyleboxOverride("hover", leaveHover);
        leaveBtn.Pressed += () =>
        {
            NetworkManager.Instance?.ReturnToMainMenu();
        };
        actionsRow.AddChild(leaveBtn);

        AddChild(_pauseMenuModal);
        _pauseMenuModal.Visible = false;
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

    #region Mr. Henderson Intercom HUD
    private void BuildIntercomHUD()
    {
        _intercomCard = new PanelContainer();
        _intercomCard.Name = "IntercomCard";
        _intercomCard.MouseFilter = Control.MouseFilterEnum.Ignore;
        
        // Centered horizontally, 640px wide, 95px tall
        _intercomCard.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _intercomCard.AnchorLeft = 0.5f;
        _intercomCard.AnchorRight = 0.5f;
        _intercomCard.OffsetLeft = -320;
        _intercomCard.OffsetRight = 320;
        _intercomCard.OffsetTop = -150; // Initially hidden above screen
        _intercomCard.OffsetBottom = -50;
        _intercomCard.CustomMinimumSize = new Vector2(640, 95);

        var cardStyle = new StyleBoxFlat();
        cardStyle.BgColor = new Color(0.08f, 0.10f, 0.14f, 0.96f);
        cardStyle.BorderWidthLeft = 3;
        cardStyle.BorderWidthTop = 3;
        cardStyle.BorderWidthRight = 3;
        cardStyle.BorderWidthBottom = 3;
        cardStyle.BorderColor = new Color(1.0f, 0.82f, 0.20f, 0.95f); // Golden store hazard outline
        cardStyle.CornerRadiusTopLeft = 10;
        cardStyle.CornerRadiusTopRight = 10;
        cardStyle.CornerRadiusBottomRight = 10;
        cardStyle.CornerRadiusBottomLeft = 10;
        cardStyle.ShadowColor = new Color(0, 0, 0, 0.7f);
        cardStyle.ShadowSize = 10;
        _intercomCard.AddThemeStyleboxOverride("panel", cardStyle);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _intercomCard.AddChild(margin);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 14);
        margin.AddChild(hbox);

        // Left column: Avatar portrait + Live badge
        var leftCol = new VBoxContainer();
        leftCol.CustomMinimumSize = new Vector2(105, 80);
        leftCol.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddChild(leftCol);

        var liveHBox = new HBoxContainer();
        liveHBox.Alignment = BoxContainer.AlignmentMode.Center;
        _intercomLiveDot = new Label();
        _intercomLiveDot.Text = "🔴";
        _intercomLiveDot.AddThemeFontSizeOverride("font_size", 12);
        liveHBox.AddChild(_intercomLiveDot);

        var liveLabel = new Label();
        liveLabel.Text = "PA LIVE";
        liveLabel.AddThemeFontSizeOverride("font_size", 11);
        liveLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.35f, 0.35f));
        liveHBox.AddChild(liveLabel);
        leftCol.AddChild(liveHBox);

        var avatarPanel = new PanelContainer();
        var avatarStyle = new StyleBoxFlat();
        avatarStyle.BgColor = new Color(0.13f, 0.16f, 0.22f, 1f);
        avatarStyle.BorderWidthLeft = 1;
        avatarStyle.BorderWidthTop = 1;
        avatarStyle.BorderWidthRight = 1;
        avatarStyle.BorderWidthBottom = 1;
        avatarStyle.BorderColor = new Color(0.4f, 0.5f, 0.6f);
        avatarStyle.CornerRadiusTopLeft = 6;
        avatarStyle.CornerRadiusTopRight = 6;
        avatarStyle.CornerRadiusBottomRight = 6;
        avatarStyle.CornerRadiusBottomLeft = 6;
        avatarPanel.AddThemeStyleboxOverride("panel", avatarStyle);

        _intercomAvatarLabel = new Label();
        _intercomAvatarLabel.Text = "( ಠ_ಠ )";
        _intercomAvatarLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _intercomAvatarLabel.VerticalAlignment = VerticalAlignment.Center;
        _intercomAvatarLabel.AddThemeFontSizeOverride("font_size", 18);
        _intercomAvatarLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
        avatarPanel.AddChild(_intercomAvatarLabel);
        leftCol.AddChild(avatarPanel);

        var nameLabel = new Label();
        nameLabel.Text = "MR. HENDERSON";
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.25f));
        leftCol.AddChild(nameLabel);

        // Right column: Channel header + Dialogue text
        var rightCol = new VBoxContainer();
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightCol.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddChild(rightCol);

        var channelLabel = new Label();
        channelLabel.Text = "📻 STORE INTERCOM — GENERAL MANAGER";
        channelLabel.AddThemeFontSizeOverride("font_size", 11);
        channelLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.85f, 1.0f));
        channelLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        channelLabel.AddThemeConstantOverride("outline_size", 2);
        rightCol.AddChild(channelLabel);

        _intercomMessageLabel = new Label();
        _intercomMessageLabel.Text = "";
        _intercomMessageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _intercomMessageLabel.AddThemeFontSizeOverride("font_size", 14);
        _intercomMessageLabel.AddThemeColorOverride("font_color", Colors.White);
        _intercomMessageLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _intercomMessageLabel.AddThemeConstantOverride("outline_size", 3);
        rightCol.AddChild(_intercomMessageLabel);

        _intercomCard.Visible = false;
        AddChild(_intercomCard);

        if (ManagerAnnouncer.Instance != null)
        {
            ManagerAnnouncer.Instance.ManagerAnnounced += OnManagerAnnounced;
            _subscribedToManager = true;
        }
    }

    public void ShowIntercomAnnouncement(string text, ManagerEmotion emotion, float duration)
    {
        if (_intercomCard == null) return;

        _intercomMessageLabel.Text = $"\"{text}\"";
        _intercomDisplayTimer = duration;

        switch (emotion)
        {
            case ManagerEmotion.Neutral:
                _intercomAvatarLabel.Text = "( ಠ_ಠ )";
                _intercomAvatarLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 0.9f));
                break;
            case ManagerEmotion.Annoyed:
                _intercomAvatarLabel.Text = "( ¬_¬ )";
                _intercomAvatarLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.6f, 0.3f));
                break;
            case ManagerEmotion.Greedy:
                _intercomAvatarLabel.Text = "( $ ‿ $ )";
                _intercomAvatarLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1.0f, 0.4f));
                break;
            case ManagerEmotion.Panicked:
                _intercomAvatarLabel.Text = "( ⊙_⊙; )";
                _intercomAvatarLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.4f));
                break;
            case ManagerEmotion.Enraged:
                _intercomAvatarLabel.Text = "( >皿< )";
                _intercomAvatarLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.2f, 0.2f));
                break;
            case ManagerEmotion.Megaphone:
                _intercomAvatarLabel.Text = "📢( ᐛ )";
                _intercomAvatarLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.2f));
                break;
            case ManagerEmotion.Smug:
                _intercomAvatarLabel.Text = "( ˘‿˘ )";
                _intercomAvatarLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1.0f));
                break;
        }

        _intercomCard.Visible = true;
        _intercomCard.OffsetTop = -150;
        _intercomCard.OffsetBottom = -50;

        if (_intercomTween != null && _intercomTween.IsValid())
        {
            _intercomTween.Kill();
        }

        _intercomTween = CreateTween();
        _intercomTween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _intercomTween.TweenProperty(_intercomCard, "offset_top", 110.0f, 0.35f);
        _intercomTween.Parallel().TweenProperty(_intercomCard, "offset_bottom", 210.0f, 0.35f);
    }

    private void HideIntercom()
    {
        if (_intercomCard == null || !_intercomCard.Visible) return;

        if (_intercomTween != null && _intercomTween.IsValid())
        {
            _intercomTween.Kill();
        }

        _intercomTween = CreateTween();
        _intercomTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        _intercomTween.TweenProperty(_intercomCard, "offset_top", -150.0f, 0.28f);
        _intercomTween.Parallel().TweenProperty(_intercomCard, "offset_bottom", -50.0f, 0.28f);
        _intercomTween.TweenCallback(Callable.From(() =>
        {
            if (_intercomCard != null) _intercomCard.Visible = false;
        }));
    }

    private void OnManagerAnnounced(string line, int emotionIndex, float duration)
    {
        ShowIntercomAnnouncement(line, (ManagerEmotion)emotionIndex, duration);
    }
    #endregion
}