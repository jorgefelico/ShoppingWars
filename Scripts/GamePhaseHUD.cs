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

    public override void _Ready()
    {
        if (EventContainer != null)
        {
            EventContainer.Visible = false;
        }
    }

    public override void _Process(double delta)
    {
        if(GameManager.Instance == null) return;

        switch(GameManager.Instance.CurrentPhase)
        {
            case GamePhase.Lobby:
                PhaseLabel.Text = "LOBBY - WAITING FOR PLAYERS";
                PhaseLabel.Modulate = Colors.Yellow;
                MoneyLabel.Visible = false;
                break;
            case GamePhase.Shopping:
                PhaseLabel.Text = "SHOPPING PHASE";
                PhaseLabel.Modulate = Colors.Cyan;
                MoneyLabel.Visible = true;
                MoneyLabel.Text = $"${PlayerController.Instance?.Money}";
                MoneyLabel.Modulate = PlayerController.Instance?.Money != 0 ? Colors.Green : Colors.Red;
                break;
            case GamePhase.BattleRoyale:
                PhaseLabel.Text = "BATTLE ROYALE - FIGHT!";
                PhaseLabel.Modulate = Colors.Red;
                MoneyLabel.Visible = false;
                break;
        }

        float remaining = GameManager.Instance.TimeRemaining;
        TimeSpan time = TimeSpan.FromSeconds(remaining);
        TimerLabel.Text = time.ToString(@"mm\:ss");

        UpdateAmbientEventHUD();
    }

    private void UpdateAmbientEventHUD()
    {
        if (AmbientEventManager.Instance == null) return;

        var activeEvent = AmbientEventManager.Instance.ActiveEvent;
        if (activeEvent != null)
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