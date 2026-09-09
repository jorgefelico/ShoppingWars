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

        if (EventContainer != null)
        {
            EventContainer.Visible = false;
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

        BuildTutorialModal();
        BuildPerkModal();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GamePhaseChanged += OnGamePhaseChanged;
        }

        PlayerPerk currentPerk = PlayerController.Instance?.CurrentPerk ?? PlayerPerk.None;
        UpdatePerkDisplay(currentPerk);
    }

    public override void _ExitTree()
    {
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
        if ((IsTutorialOpen || IsPerkModalOpen) && Input.MouseMode != Input.MouseModeEnum.Visible)
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
                MoneyLabel.Text = $"${PlayerController.Instance?.Money}";
                MoneyLabel.Modulate = PlayerController.Instance?.Money != 0 ? Colors.Green : Colors.Red;
                if (GameOverPanel != null) GameOverPanel.Visible = false;
                break;
            case GamePhase.BattleTransition:
                PhaseLabel.Text = "STORE LOCKDOWN - PREPARE FOR BATTLE!";
                PhaseLabel.Modulate = Colors.OrangeRed;
                MoneyLabel.Visible = false;
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
                MoneyLabel.Visible = false;
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
    private Label _helpHintLabel;
    public bool IsTutorialOpen => _tutorialModal != null && _tutorialModal.Visible;

    public override void _UnhandledInput(InputEvent @event)
    {
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
                if (IsPerkModalOpen)
                {
                    ClosePerkModal();
                    GetViewport().SetInputAsHandled();
                }
                else if (IsTutorialOpen)
                {
                    CloseTutorial();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    public void OpenTutorial()
    {
        if (_tutorialModal != null)
        {
            _tutorialModal.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void CloseTutorial()
    {
        if (_tutorialModal != null)
        {
            _tutorialModal.Visible = false;
            if (!IsPerkModalOpen &&
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

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.04f, 0.05f, 0.08f, 0.90f);
        _tutorialModal.AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Pass;
        _tutorialModal.AddChild(center);

        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(820, 540);
        var cardStyle = new StyleBoxFlat();
        cardStyle.BgColor = new Color(0.08f, 0.10f, 0.15f, 0.98f);
        cardStyle.BorderWidthLeft = 3;
        cardStyle.BorderWidthTop = 3;
        cardStyle.BorderWidthRight = 3;
        cardStyle.BorderWidthBottom = 3;
        cardStyle.BorderColor = new Color(1.0f, 0.8f, 0.2f, 0.95f);
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
        vbox.AddThemeConstantOverride("separation", 10);
        card.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text = "🛒 WELCOME TO SHOPPING WARS! 🛒";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.15f));
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        title.AddThemeConstantOverride("outline_size", 4);
        title.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = "Supermarket Battle Royale — Player Guide & Controls";
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 0.9f));
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(subtitle);

        vbox.AddChild(new HSeparator());

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 24);
        vbox.AddChild(columns);

        // Left column: Game Rules
        var leftCol = new VBoxContainer();
        leftCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leftCol.AddThemeConstantOverride("separation", 6);
        columns.AddChild(leftCol);

        var leftTitle = new Label();
        leftTitle.Text = "🎯 MATCH PHASES & RULES";
        leftTitle.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 1.0f));
        leftTitle.AddThemeFontSizeOverride("font_size", 15);
        leftCol.AddChild(leftTitle);

        string[] rules = new string[]
        {
            "🟡 1. LOBBY: Walk to the red button & press [E] to Ready Up!",
            "⭐ 2. PERKS [P]: Equip 1 passive perk (Speed, Tank, Power Arm, Scavenger, etc.)!",
            "🔵 3. SHOPPING (50s): Start with $175. Run aisles & press [E] to buy weapons!",
            "🔴 4. BATTLE ROYALE: Store locks down! Items deal lethal damage when thrown.",
            "⚔️ 5. MELEE BRAWLING: Right Click to swing your item or punch bare-handed!",
            "🌐 6. SAFE ZONE: Stay inside the glowing barrier! Outside deals 6 damage/s.",
            "📦 7. SCAVENGING: Out of ammo? Grab shelf items mid-fight for free!",
            "💀 8. KILL REWARDS: Eliminating a player awards +35 HP, +$50 & speed boost!",
            "💊 9. HEALING: Press [R] with Pill Bottles or Soda Cans to restore HP!",
            "🤖 10. HAZARDS: Dodge patrolling Groomba vacuums & dynamic store events!"
        };

        foreach (var rule in rules)
        {
            var lbl = new Label();
            lbl.Text = rule;
            lbl.AddThemeFontSizeOverride("font_size", 12);
            lbl.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.94f));
            lbl.AutowrapMode = TextServer.AutowrapMode.Word;
            leftCol.AddChild(lbl);
        }

        columns.AddChild(new VSeparator());

        // Right column: Controls
        var rightCol = new VBoxContainer();
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightCol.AddThemeConstantOverride("separation", 5);
        columns.AddChild(rightCol);

        var rightTitle = new Label();
        rightTitle.Text = "⌨️ CONTROLS GUIDE";
        rightTitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
        rightTitle.AddThemeFontSizeOverride("font_size", 15);
        rightCol.AddChild(rightTitle);

        (string key, string desc)[] controls = new (string, string)[]
        {
            ("[ W A S D ]", "Move Around"),
            ("[ SHIFT ]", "Sprint (Faster Movement)"),
            ("[ SPACE ]", "Jump"),
            ("[ E ]", "Interact / Buy / Scavenge"),
            ("[ LMB / Left Click ]", "Throw Held Weapon"),
            ("[ RMB / Right Click ]", "Melee Swing / Fist Punch"),
            ("[ R ]", "Eat / Drink (Heal HP)"),
            ("[ Q ]", "Drop Held Item"),
            ("[ 1 - 6 / Wheel ]", "Select Inventory Slot"),
            ("[ P ]", "Choose Shopper Perk"),
            ("[ F ]", "Toggle Flashlight"),
            ("[ H ]", "Open / Close this Guide Anytime")
        };

        foreach (var ctrl in controls)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var keyLbl = new Label();
            keyLbl.Text = ctrl.key;
            keyLbl.CustomMinimumSize = new Vector2(145, 0);
            keyLbl.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.5f));
            keyLbl.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(keyLbl);

            var descLbl = new Label();
            descLbl.Text = ctrl.desc;
            descLbl.AddThemeColorOverride("font_color", new Color(0.88f, 0.88f, 0.9f));
            descLbl.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(descLbl);

            rightCol.AddChild(row);
        }

        vbox.AddChild(new HSeparator());

        // Footer button & tip
        var footer = new VBoxContainer();
        footer.AddThemeConstantOverride("separation", 6);
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(footer);

        var btnContainer = new CenterContainer();
        footer.AddChild(btnContainer);

        var closeBtn = new Button();
        closeBtn.Text = "✅ GOT IT! LET'S SHOP";
        closeBtn.CustomMinimumSize = new Vector2(240, 42);
        closeBtn.AddThemeFontSizeOverride("font_size", 16);

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

        closeBtn.Pressed += CloseTutorial;
        btnContainer.AddChild(closeBtn);

        var tipLbl = new Label();
        tipLbl.Text = "Press [H] anytime during the match to toggle this guide!";
        tipLbl.HorizontalAlignment = HorizontalAlignment.Center;
        tipLbl.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 0.8f));
        tipLbl.AddThemeFontSizeOverride("font_size", 11);
        footer.AddChild(tipLbl);

        AddChild(_tutorialModal);

        // Show automatically if in Lobby phase!
        if (GameManager.Instance == null || GameManager.Instance.CurrentPhase == GamePhase.Lobby)
        {
            OpenTutorial();
        }
        else
        {
            _tutorialModal.Visible = false;
        }
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
            if (!IsTutorialOpen &&
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
}