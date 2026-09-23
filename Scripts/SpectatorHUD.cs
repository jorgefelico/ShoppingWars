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
        StyleSpectatorUI();

        if (PrevButton != null)
        {
            PrevButton.Pressed += () => PreviousRequested?.Invoke();
        }

        if (NextButton != null)
        {
            NextButton.Pressed += () => NextRequested?.Invoke();
        }
    }

    private void StyleSpectatorUI()
    {
        // Style Top Banner Container
        var topContainer = GetNodeOrNull<Control>("TopContainer");
        if (topContainer != null)
        {
            // Create a stylish comic backing panel if not present
            var existingBg = topContainer.GetNodeOrNull<Panel>("ComicBannerBg");
            if (existingBg == null)
            {
                var bgPanel = new Panel
                {
                    Name = "ComicBannerBg",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ShowBehindParent = true
                };
                bgPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                bgPanel.OffsetLeft = -24;
                bgPanel.OffsetRight = 24;
                bgPanel.OffsetTop = -10;
                bgPanel.OffsetBottom = 12;
                bgPanel.AddThemeStyleboxOverride("panel", UITheme.CreateComicCard(
                    new Color(0.08f, 0.09f, 0.14f, 0.94f),
                    UITheme.ActionRed,
                    cornerRadius: 10,
                    borderWidth: 3,
                    shadowOffset: 4
                ));
                topContainer.AddChild(bgPanel);
                topContainer.MoveChild(bgPanel, 0);
            }
        }

        if (TitleLabel != null)
        {
            TitleLabel.Modulate = Colors.White;
            UITheme.FormatComicLabel(TitleLabel, UITheme.TitleFont, 36, UITheme.ActionRed, UITheme.InkBlack, 6, UITheme.InkBlack, new Vector2I(3, 3));
        }

        if (SpectatingLabel != null)
        {
            SpectatingLabel.Modulate = Colors.White;
            UITheme.FormatComicLabel(SpectatingLabel, UITheme.BodyFont, 20, UITheme.ElectricCyan, UITheme.InkBlack, 4, UITheme.InkBlack, new Vector2I(2, 2));
        }

        if (HintLabel != null)
        {
            UITheme.FormatComicLabel(HintLabel, UITheme.BodyFont, 14, UITheme.PaperCream, UITheme.InkBlack, 3);
        }

        if (PrevButton != null)
        {
            UITheme.ApplyArcadeButton(PrevButton, UITheme.ElectricCyan, fontSize: 15);
        }

        if (NextButton != null)
        {
            UITheme.ApplyArcadeButton(NextButton, UITheme.ElectricCyan, fontSize: 15);
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
            TitleLabel.AddThemeColorOverride("font_color", UITheme.ActionRed);
        }

        if (SpectatingLabel != null)
        {
            SpectatingLabel.Text = $"SPECTATING: {playerName.ToUpper()}";
            SpectatingLabel.AddThemeColorOverride("font_color", UITheme.ElectricCyan);
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
            TitleLabel.AddThemeColorOverride("font_color", UITheme.ActionRed);
        }

        if (SpectatingLabel != null)
        {
            SpectatingLabel.Text = "NO LIVING PLAYERS TO SPECTATE";
            SpectatingLabel.AddThemeColorOverride("font_color", UITheme.SubtitleGray);
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
