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

    public override void _Ready()
    {
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
    }

    public override void _ExitTree()
    {
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
        if (GameManager.Instance == null) return;

        switch (GameManager.Instance.CurrentPhase)
        {
            case GamePhase.Lobby:
                PhaseLabel.Text = "LOBBY - WAITING FOR PLAYERS";
                PhaseLabel.Modulate = Colors.Yellow;
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
            case GamePhase.BattleRoyale:
                PhaseLabel.Text = "BATTLE ROYALE - FIGHT!";
                PhaseLabel.Modulate = Colors.Red;
                MoneyLabel.Visible = false;
                if (GameOverPanel != null) GameOverPanel.Visible = false;
                break;
            case GamePhase.GameOver:
                PhaseLabel.Text = "MATCH OVER";
                PhaseLabel.Modulate = Colors.Gold;
                MoneyLabel.Visible = false;
                UpdateGameOverHUD();
                break;
        }

        float remaining = GameManager.Instance.TimeRemaining;
        TimeSpan time = TimeSpan.FromSeconds(remaining);
        TimerLabel.Text = time.ToString(@"mm\:ss");

        UpdateAmbientEventHUD();
    }

    private void UpdateGameOverHUD()
    {
        if (GameOverPanel == null) return;

        GameOverPanel.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        string winner = GameManager.Instance.WinnerName;
        bool isDraw = GameManager.Instance.IsDraw;
        string myName = SteamManager.Instance?.GetPersonaName() ?? (PlayerController.Instance != null ? PlayerController.Instance.PlayerName : "");

        if (isDraw)
        {
            if (GameOverHeaderLabel != null)
            {
                GameOverHeaderLabel.Text = "💀 MATCH OVER 💀";
                GameOverHeaderLabel.Modulate = new Color(0.95f, 0.3f, 0.3f);
            }
            if (WinnerNameLabel != null)
            {
                WinnerNameLabel.Text = "NO SURVIVORS";
                WinnerNameLabel.Modulate = new Color(0.95f, 0.2f, 0.2f);
            }
            if (SubtitleLabel != null)
            {
                SubtitleLabel.Text = "All shoppers were eliminated in the store royale!";
            }
            if (StatsDetailLabel != null)
            {
                StatsDetailLabel.Text = "Draw Match — No Winner Declared";
            }
        }
        else if (!string.IsNullOrEmpty(winner) && winner.Equals(myName, StringComparison.OrdinalIgnoreCase))
        {
            if (GameOverHeaderLabel != null)
            {
                GameOverHeaderLabel.Text = "👑 VICTORY ROYALE 👑";
                GameOverHeaderLabel.Modulate = new Color(1.0f, 0.85f, 0.1f);
            }
            if (WinnerNameLabel != null)
            {
                WinnerNameLabel.Text = "YOU WON!";
                WinnerNameLabel.Modulate = new Color(0.2f, 1.0f, 0.4f);
            }
            if (SubtitleLabel != null)
            {
                SubtitleLabel.Text = "🎉 You are the Last Shopper Standing! 🎉";
            }
            if (StatsDetailLabel != null)
            {
                StatsDetailLabel.Text = $"Store Champion: {winner} | Shopping Wars Winner";
            }
        }
        else
        {
            if (GameOverHeaderLabel != null)
            {
                GameOverHeaderLabel.Text = "🏆 MATCH OVER 🏆";
                GameOverHeaderLabel.Modulate = new Color(1.0f, 0.85f, 0.1f);
            }
            if (WinnerNameLabel != null)
            {
                WinnerNameLabel.Text = $"WINNER: {winner.ToUpper()}";
                WinnerNameLabel.Modulate = new Color(0.3f, 0.85f, 1.0f);
            }
            if (SubtitleLabel != null)
            {
                SubtitleLabel.Text = $"{winner} is the Last Shopper Standing!";
            }
            if (StatsDetailLabel != null)
            {
                StatsDetailLabel.Text = $"Store Champion: {winner}";
            }
        }

        if (PlayAgainButton != null)
        {
            if (Multiplayer.IsServer())
            {
                PlayAgainButton.Text = "🔄 RESTART MATCH";
                PlayAgainButton.Disabled = false;
            }
            else
            {
                PlayAgainButton.Text = "WAITING FOR HOST...";
                PlayAgainButton.Disabled = true;
            }
        }
    }

    private void OnPlayAgainPressed()
    {
        if (Multiplayer.IsServer())
        {
            GameManager.Instance?.RestartGame();
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
}