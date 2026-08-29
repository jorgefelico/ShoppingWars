using System;
using Godot;

public partial class GamePhaseHUD : CanvasLayer
{
    [Export] private Label PhaseLabel;
    [Export] private Label TimerLabel;
    [Export] private Label MoneyLabel;

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
    }
}