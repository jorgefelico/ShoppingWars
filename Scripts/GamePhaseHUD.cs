using System;
using Godot;

public partial class GamePhaseHUD : CanvasLayer
{
    [Export] private Label PhaseLabel;
    [Export] private Label TimerLabel;

    public override void _Process(double delta)
    {
        if(GameManager.Instance == null) return;

        switch(GameManager.Instance.CurrentPhase)
        {
            case GamePhase.Lobby:
                PhaseLabel.Text = "LOBBY - WAITING FOR PLAYERS";
                PhaseLabel.Modulate = Colors.Yellow;
                break;
            case GamePhase.Shopping:
                PhaseLabel.Text = "SHOPPING PHASE";
                PhaseLabel.Modulate = Colors.Cyan;
                break;
            case GamePhase.BattleRoyale:
                PhaseLabel.Text = "BATTLE ROYALE - FIGHT!";
                PhaseLabel.Modulate = Colors.Red;
                break;
        }

        float remaining = GameManager.Instance.TimeRemaining;
        TimeSpan time = TimeSpan.FromSeconds(remaining);
        TimerLabel.Text = time.ToString(@"mm\:ss");
    }
}