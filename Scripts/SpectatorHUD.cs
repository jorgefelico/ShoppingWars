using Godot;
using System;

public partial class SpectatorHUD : CanvasLayer
{
    [Export] private Label TitleLabel;
    [Export] private Label SpectatingLabel;
    [Export] private Label HintLabel;
    [Export] private Button PrevButton;
    [Export] private Button NextButton;
    [Export] private Control ControlsContainer;

    public event Action PreviousRequested;
    public event Action NextRequested;

    public override void _Ready()
    {
        if (PrevButton != null)
        {
            PrevButton.Pressed += () => PreviousRequested?.Invoke();
        }

        if (NextButton != null)
        {
            NextButton.Pressed += () => NextRequested?.Invoke();
        }
    }

    public override void _Process(double delta)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase == GamePhase.GameOver)
        {
            if (ControlsContainer != null && ControlsContainer.Visible)
            {
                ControlsContainer.Visible = false;
            }
        }
    }

    public void SetSpectating(string playerName)
    {
        if (TitleLabel != null)
        {
            TitleLabel.Text = "ELIMINATED";
            TitleLabel.Modulate = new Color(0.95f, 0.2f, 0.2f);
        }

        if (SpectatingLabel != null)
        {
            SpectatingLabel.Text = $"SPECTATING: {playerName.ToUpper()}";
            SpectatingLabel.Modulate = new Color(0.3f, 0.85f, 1.0f);
        }

        if (HintLabel != null)
        {
            HintLabel.Text = "[Left Click / A] Previous  |  [Right Click / D] Next";
            HintLabel.Visible = true;
        }

        if (ControlsContainer != null)
        {
            ControlsContainer.Visible = true;
        }
    }

    public void SetNoLivingPlayers()
    {
        if (TitleLabel != null)
        {
            TitleLabel.Text = "ELIMINATED";
            TitleLabel.Modulate = new Color(0.95f, 0.2f, 0.2f);
        }

        if (SpectatingLabel != null)
        {
            SpectatingLabel.Text = "NO LIVING PLAYERS TO SPECTATE";
            SpectatingLabel.Modulate = new Color(0.8f, 0.8f, 0.8f);
        }

        if (HintLabel != null)
        {
            HintLabel.Visible = false;
        }

        if (ControlsContainer != null)
        {
            ControlsContainer.Visible = false;
        }
    }
}
