using Godot;
using System;

public partial class HealthBar : CanvasLayer
{
    [Export] private ColorRect ColorRect; // Legacy binding if present

    private Control _rootContainer;
    private PanelContainer _barFrame;
    private ColorRect _ghostBar;
    private ColorRect _activeBar;
    private Label _hpLabel;
    private Label _heartIcon;
    private PanelContainer _perkBadge;
    private Label _perkBadgeLabel;

    private float _maxBarWidth = 220f;
    private int _lastHealth = -1;
    private int _currentMaxHealth = 200;
    private Tween _ghostTween;
    private Tween _dangerTween;
    private bool _isDangerPulsing = false;

    public override void _Ready()
    {
        // Hide legacy node if it exists
        if (ColorRect != null)
        {
            ColorRect.Visible = false;
            var parent = ColorRect.GetParent() as Control;
            if (parent != null) parent.Visible = false;
        }

        BuildComicHealthUI();
    }

    private void BuildComicHealthUI()
    {
        // Position at bottom-left corner with comfortable margin
        _rootContainer = new Control
        {
            Name = "ComicHealthGauge",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _rootContainer.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _rootContainer.AnchorLeft = 0f;
        _rootContainer.AnchorTop = 1f;
        _rootContainer.AnchorRight = 0f;
        _rootContainer.AnchorBottom = 1f;
        _rootContainer.OffsetLeft = 20;
        _rootContainer.OffsetRight = 244;
        _rootContainer.OffsetTop = -64;
        _rootContainer.OffsetBottom = -18;
        _rootContainer.CustomMinimumSize = new Vector2(224, 46);
        AddChild(_rootContainer);

        var vbox = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        vbox.AddThemeConstantOverride("separation", 2);
        _rootContainer.AddChild(vbox);

        // 1. Top row: Status title & Perk badge
        var topRow = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        topRow.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(topRow);

        _heartIcon = new Label
        {
            Text = "❤️ HP",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        UITheme.FormatComicLabel(_heartIcon, UITheme.BodyFont, 11, UITheme.PaperWhite, UITheme.InkBlack, 2);
        topRow.AddChild(_heartIcon);

        // Perk Badge
        _perkBadge = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _perkBadge.AddThemeStyleboxOverride("panel", UITheme.CreatePriceBadgeStyle(UITheme.CardHeader, 4));
        topRow.AddChild(_perkBadge);

        _perkBadgeLabel = new Label
        {
            Text = "PERK: NONE",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        UITheme.FormatComicLabel(_perkBadgeLabel, UITheme.BodyFont, 10, UITheme.FlyerYellow, UITheme.InkBlack, 2);
        _perkBadge.AddChild(_perkBadgeLabel);

        // Update perk label if PlayerController exists
        UpdatePerkBadge();

        // 2. Health Bar Track (Comic Ink Frame)
        _barFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(_maxBarWidth, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _barFrame.AddThemeStyleboxOverride("panel", UITheme.CreateComicCard(UITheme.CardDark, UITheme.InkBlack, 6, 2, 2));
        vbox.AddChild(_barFrame);

        // Track interior container for fills
        var fillContainer = new Control
        {
            CustomMinimumSize = new Vector2(_maxBarWidth, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true
        };
        _barFrame.AddChild(fillContainer);

        // Damage Ghost Bar (Warm orange-red trailing catch-up bar)
        _ghostBar = new ColorRect
        {
            Name = "GhostBar",
            Color = new Color(1.0f, 0.55f, 0.15f, 0.9f),
            CustomMinimumSize = new Vector2(_maxBarWidth, 16),
            Size = new Vector2(_maxBarWidth, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        fillContainer.AddChild(_ghostBar);

        // Active Health Fill Bar (Vibrant produce green)
        _activeBar = new ColorRect
        {
            Name = "ActiveBar",
            Color = UITheme.FreshGreen,
            CustomMinimumSize = new Vector2(_maxBarWidth, 16),
            Size = new Vector2(_maxBarWidth, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        fillContainer.AddChild(_activeBar);

        // Centered Numeric HP Readout
        _hpLabel = new Label
        {
            Name = "HpLabel",
            Text = "200 / 200 HP",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _hpLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        UITheme.FormatComicLabel(_hpLabel, UITheme.BodyFont, 11, Colors.White, UITheme.InkBlack, 2);
        _barFrame.AddChild(_hpLabel);
    }

    public void UpdatePerkBadge()
    {
        if (_perkBadgeLabel == null) return;
        PlayerPerk perk = PlayerController.Instance?.CurrentPerk ?? PlayerPerk.None;
        if (perk == PlayerPerk.None)
        {
            _perkBadge.Visible = false;
        }
        else
        {
            _perkBadge.Visible = true;
            string name = perk.ToString().ToUpper();
            string icon = perk switch
            {
                PlayerPerk.BargainHunter => "💰",
                PlayerPerk.PowerArm => "💪",
                PlayerPerk.Tank => "🛡️",
                PlayerPerk.Scavenger => "🎒",
                PlayerPerk.SpeedDemon => "⚡",
                PlayerPerk.StickyFingers => "🧤",
                _ => "⭐"
            };
            _perkBadgeLabel.Text = $"{icon} {name}";
        }
    }

    public void Refresh(int currentHealth, int maxHealth)
    {
        _currentMaxHealth = Mathf.Max(1, maxHealth);
        float pct = Mathf.Clamp((float)currentHealth / _currentMaxHealth, 0f, 1f);
        float targetWidth = _maxBarWidth * pct;

        if (_activeBar != null)
        {
            // Immediate change for active bar with slight squash/stretch
            _activeBar.Size = new Vector2(targetWidth, 16);

            // Dynamic color coding based on remaining health
            if (pct > 0.50f)
            {
                _activeBar.Color = UITheme.FreshGreen;
            }
            else if (pct > 0.25f)
            {
                _activeBar.Color = UITheme.ClearanceOrange;
            }
            else
            {
                _activeBar.Color = UITheme.ActionRed;
            }
        }

        // Catch-up ghost bar animation (smooth trailing damage indicator)
        if (_ghostBar != null)
        {
            if (_lastHealth != -1 && currentHealth < _lastHealth)
            {
                // Health went down: cancel any prior tween, wait 0.25s, then glide down
                if (_ghostTween != null && _ghostTween.IsValid())
                {
                    _ghostTween.Kill();
                }

                _ghostTween = CreateTween();
                _ghostTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                _ghostTween.TweenInterval(0.25f);
                _ghostTween.TweenProperty(_ghostBar, "size:x", targetWidth, 0.45f);
            }
            else
            {
                // Health healed or initial setup: snap ghost immediately
                _ghostBar.Size = new Vector2(targetWidth, 16);
            }
        }

        if (_hpLabel != null)
        {
            _hpLabel.Text = $"{Mathf.Max(0, currentHealth)} / {_currentMaxHealth} HP";
        }

        // Low health danger alarm pulse (< 30% HP)
        bool isLowHealth = pct <= 0.30f && currentHealth > 0;
        SetLowHealthPulse(isLowHealth);

        _lastHealth = currentHealth;
        UpdatePerkBadge();
    }

    private void SetLowHealthPulse(bool enable)
    {
        if (enable == _isDangerPulsing) return;
        _isDangerPulsing = enable;

        if (_dangerTween != null && _dangerTween.IsValid())
        {
            _dangerTween.Kill();
            _dangerTween = null;
        }

        if (_isDangerPulsing && _rootContainer != null)
        {
            _dangerTween = CreateTween();
            _dangerTween.SetLoops();
            _dangerTween.TweenProperty(_rootContainer, "modulate", new Color(1.3f, 0.5f, 0.5f, 1f), 0.35f);
            _dangerTween.TweenProperty(_rootContainer, "modulate", Colors.White, 0.35f);
        }
        else if (_rootContainer != null)
        {
            _rootContainer.Modulate = Colors.White;
        }
    }
}
