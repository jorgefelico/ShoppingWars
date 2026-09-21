using Godot;
using System;

public partial class ItemHoverBox : Control
{
    private PanelContainer _cardPanel;
    private TextureRect _iconRect;
    private Label _titleLabel;
    private PanelContainer _priceBadge;
    private Label _priceLabel;
    private StyleBoxFlat _badgeStyle;
    private ColorRect _divider;
    private VBoxContainer _effectsVBox;
    private HBoxContainer _damageHBox;
    private Label _damageLabel;
    private Label _durabilityLabel;
    private Label _healLabel;
    private Label _specialLabel;
    private HBoxContainer _promptHBox;
    private PanelContainer _keyBadge;
    private Label _keyLabel;
    private Label _actionLabel;

    private Tween _fadeTween;
    private Product _currentProduct;
    private IInteractable _currentInteractable;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        Modulate = new Color(1, 1, 1, 0);

        // Center on screen, positioned just below the crosshair
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.End;
        OffsetLeft = -105f;
        OffsetRight = 105f;
        OffsetTop = 28f;

        BuildUI();
    }

    private void BuildUI()
    {
        // 1. Root Card Panel (Comic-book styled box)
        _cardPanel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(200, 0)
        };

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.11f, 0.94f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 3,
            BorderColor = new Color(0f, 0f, 0f, 1f),
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 7,
            ContentMarginBottom = 7,
            ShadowColor = new Color(0f, 0f, 0f, 0.5f),
            ShadowSize = 4,
            ShadowOffset = new Vector2(0, 2)
        };
        _cardPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_cardPanel);

        // 2. Main Vertical Layout
        var mainVBox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        mainVBox.AddThemeConstantOverride("separation", 5);
        _cardPanel.AddChild(mainVBox);

        // 3. Header: Icon, Item Name & Price/Status Badge
        var headerHBox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        headerHBox.AddThemeConstantOverride("separation", 7);
        mainVBox.AddChild(headerHBox);

        _iconRect = new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(24, 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Visible = false
        };
        headerHBox.AddChild(_iconRect);

        _titleLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 14);
        _titleLabel.AddThemeColorOverride("font_color", Colors.White);
        _titleLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _titleLabel.AddThemeConstantOverride("outline_size", 3);
        headerHBox.AddChild(_titleLabel);

        // Price / Status Badge (Pill Style)
        _priceBadge = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _badgeStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.50f, 0.22f, 0.95f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0f, 0f, 0f, 0.85f),
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 1,
            ContentMarginBottom = 1
        };
        _priceBadge.AddThemeStyleboxOverride("panel", _badgeStyle);
        headerHBox.AddChild(_priceBadge);

        _priceLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _priceLabel.AddThemeFontSizeOverride("font_size", 11);
        _priceLabel.AddThemeColorOverride("font_color", Colors.White);
        _priceLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _priceLabel.AddThemeConstantOverride("outline_size", 2);
        _priceBadge.AddChild(_priceLabel);

        // 4. Divider Line
        _divider = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 1),
            Color = new Color(1f, 1f, 1f, 0.16f)
        };
        mainVBox.AddChild(_divider);

        // 5. Effects Container (Damage, Durability, Heal, Explosive)
        _effectsVBox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _effectsVBox.AddThemeConstantOverride("separation", 3);
        mainVBox.AddChild(_effectsVBox);

        // Damage Row (Throw Damage + Durability)
        _damageHBox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _damageHBox.AddThemeConstantOverride("separation", 6);
        _effectsVBox.AddChild(_damageHBox);

        _damageLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _damageLabel.AddThemeFontSizeOverride("font_size", 12);
        _damageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.55f, 0.20f)); // Comic damage orange
        _damageLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _damageLabel.AddThemeConstantOverride("outline_size", 3);
        _damageHBox.AddChild(_damageLabel);

        _durabilityLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _durabilityLabel.AddThemeFontSizeOverride("font_size", 11);
        _durabilityLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.78f, 0.90f)); // Steel blue
        _durabilityLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _durabilityLabel.AddThemeConstantOverride("outline_size", 2);
        _damageHBox.AddChild(_durabilityLabel);

        // Heal Row
        _healLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _healLabel.AddThemeFontSizeOverride("font_size", 12);
        _healLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.95f, 0.45f)); // Healthy neon green
        _healLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _healLabel.AddThemeConstantOverride("outline_size", 3);
        _effectsVBox.AddChild(_healLabel);

        // Special Explosive Row
        _specialLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _specialLabel.AddThemeFontSizeOverride("font_size", 11);
        _specialLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.35f, 0.35f)); // Crimson blast
        _specialLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _specialLabel.AddThemeConstantOverride("outline_size", 2);
        _effectsVBox.AddChild(_specialLabel);

        // 6. Action Prompt Row: [E] Keycap + Action Text
        _promptHBox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _promptHBox.AddThemeConstantOverride("separation", 6);
        mainVBox.AddChild(_promptHBox);

        _keyBadge = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        var keyStyle = new StyleBoxFlat
        {
            BgColor = new Color(1.0f, 0.85f, 0.20f, 1.0f), // Comic yellow keycap
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 2,
            BorderColor = new Color(0f, 0f, 0f, 1f),
            ContentMarginLeft = 5,
            ContentMarginRight = 5,
            ContentMarginTop = 1,
            ContentMarginBottom = 1
        };
        _keyBadge.AddThemeStyleboxOverride("panel", keyStyle);
        _promptHBox.AddChild(_keyBadge);

        _keyLabel = new Label
        {
            Text = "E",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _keyLabel.AddThemeFontSizeOverride("font_size", 11);
        _keyLabel.AddThemeColorOverride("font_color", Colors.Black);
        _keyBadge.AddChild(_keyLabel);

        _actionLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center
        };
        _actionLabel.AddThemeFontSizeOverride("font_size", 12);
        _actionLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.95f));
        _actionLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _actionLabel.AddThemeConstantOverride("outline_size", 2);
        _promptHBox.AddChild(_actionLabel);
    }

    private void SetBadgeColor(Color color)
    {
        if (_badgeStyle != null)
        {
            _badgeStyle.BgColor = color;
        }
    }

    public void ShowProduct(Product product, PlayerController player)
    {
        if (product == null || !GodotObject.IsInstanceValid(product))
        {
            HideBox();
            return;
        }

        _currentProduct = product;
        _currentInteractable = product;

        // 1. Title & Icon
        string name = (product.DisplayName != null && !string.IsNullOrEmpty(product.DisplayName.ToString()))
            ? product.DisplayName.ToString()
            : product.Name.ToString();
        _titleLabel.Text = name;

        if (product.Icon != null)
        {
            _iconRect.Texture = product.Icon;
            _iconRect.Visible = true;
        }
        else
        {
            _iconRect.Visible = false;
        }

        // 2. Price, Phase & Action Prompt
        var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Lobby;
        int basePrice = product.Price;
        int effectivePrice = player != null ? player.GetDiscountedPrice(basePrice) : basePrice;
        bool hasDiscount = player != null && player.CurrentPerk == PlayerPerk.BargainHunter && effectivePrice < basePrice;
        bool isScavenger = player != null && player.CurrentPerk == PlayerPerk.Scavenger;

        if (product.IsForSale)
        {
            if (phase == GamePhase.Shopping)
            {
                _priceBadge.Visible = true;
                _priceLabel.Text = hasDiscount ? $"${effectivePrice} (-25%)" : $"${effectivePrice}";
                SetBadgeColor(hasDiscount ? new Color(0.10f, 0.52f, 0.25f) : new Color(0.85f, 0.65f, 0.10f));
                _keyBadge.Visible = true;
                _actionLabel.Text = "Buy";
            }
            else if (phase == GamePhase.BattleRoyale)
            {
                if (isScavenger)
                {
                    _priceBadge.Visible = true;
                    _priceLabel.Text = "🎒 FREE";
                    SetBadgeColor(new Color(0.15f, 0.55f, 0.78f)); // Scavenger blue
                    _keyBadge.Visible = true;
                    _actionLabel.Text = "Scavenge Shelf";
                }
                else
                {
                    _priceBadge.Visible = true;
                    _priceLabel.Text = $"${effectivePrice}";
                    SetBadgeColor(new Color(0.85f, 0.65f, 0.10f));
                    _keyBadge.Visible = true;
                    _actionLabel.Text = "Buy";
                }
            }
            else if (phase == GamePhase.BattleTransition)
            {
                _priceBadge.Visible = true;
                _priceLabel.Text = "🔒 LOCKED";
                SetBadgeColor(new Color(0.70f, 0.18f, 0.18f));
                _keyBadge.Visible = false;
                _actionLabel.Text = "Store Lockdown";
            }
            else // Lobby
            {
                _priceBadge.Visible = true;
                _priceLabel.Text = $"${product.Price}";
                SetBadgeColor(new Color(0.35f, 0.38f, 0.45f));
                _keyBadge.Visible = false;
                _actionLabel.Text = "Store Closed (Warmup)";
            }
        }
        else
        {
            // Ground loot / dropped item
            _priceBadge.Visible = true;
            _priceLabel.Text = "FREE";
            SetBadgeColor(new Color(0.18f, 0.52f, 0.32f));
            _keyBadge.Visible = true;
            _actionLabel.Text = "Pick Up";
        }

        // 3. Effects: Damage, Durability, Heal, Explosive
        bool hasAnyEffect = false;

        // Damage calculation (including Power Arm perk bonus)
        int baseDamage = product.Damage;
        bool hasPowerArm = player != null && player.CurrentPerk == PlayerPerk.PowerArm;
        int effectiveDamage = hasPowerArm ? Mathf.RoundToInt(baseDamage * 1.25f) : baseDamage;

        if (baseDamage > 0)
        {
            _damageHBox.Visible = true;
            _damageLabel.Text = hasPowerArm
                ? $"💥 {effectiveDamage} DMG (+25%)"
                : $"💥 {effectiveDamage} DMG";

            if (product.MeleeDurability > 0)
            {
                _durabilityLabel.Visible = true;
                _durabilityLabel.Text = $"• 🔨 {product.CurrentDurability} Hits";
            }
            else
            {
                _durabilityLabel.Visible = false;
            }
            hasAnyEffect = true;
        }
        else
        {
            _damageHBox.Visible = false;
        }

        // Consumable Healing
        if (product.IsConsumable && product.HealAmount > 0)
        {
            _healLabel.Visible = true;
            _healLabel.Text = $"💚 +{product.HealAmount} HP Heal ([R] Consume)";
            hasAnyEffect = true;
        }
        else
        {
            _healLabel.Visible = false;
        }

        // Explosive blast hazard
        bool isExplosive = (product.DisplayName == "Propane Tank" || product.Name.ToString().Contains("PropaneTank"));
        if (isExplosive)
        {
            _specialLabel.Visible = true;
            _specialLabel.Text = "💣 EXPLOSIVE BLAST ON IMPACT";
            hasAnyEffect = true;
        }
        else if (product is PotatoGun pg)
        {
            _damageLabel.Text = hasPowerArm
                ? $"💥 {effectiveDamage} SPUD DMG (+25%)"
                : $"💥 {effectiveDamage} SPUD DMG";
            _durabilityLabel.Visible = true;
            _durabilityLabel.Text = "• 🔨 Indestructible PVC Club";

            _specialLabel.Visible = true;
            _specialLabel.Text = $"🥔 PNEUMATIC SPUD CANNON ({pg.CurrentAmmo}/{pg.MaxAmmo} AMMO)";
            hasAnyEffect = true;
        }
        else
        {
            _specialLabel.Visible = false;
        }

        _effectsVBox.Visible = hasAnyEffect;
        _divider.Visible = hasAnyEffect;

        ShowBox();
    }

    public void ShowReadyUp(ReadyUp readyUp)
    {
        if (readyUp == null || !GodotObject.IsInstanceValid(readyUp))
        {
            HideBox();
            return;
        }

        _currentProduct = null;
        _currentInteractable = readyUp;

        _titleLabel.Text = "Ready Station";
        _iconRect.Visible = false;

        _priceBadge.Visible = true;
        _priceLabel.Text = "START";
        SetBadgeColor(new Color(0.85f, 0.45f, 0.12f));

        _effectsVBox.Visible = false;
        _divider.Visible = false;

        _keyBadge.Visible = true;
        _actionLabel.Text = "Start Shopping Match";

        ShowBox();
    }

    public void ShowGeneric(string text)
    {
        _currentProduct = null;
        _currentInteractable = null;

        _titleLabel.Text = text;
        _iconRect.Visible = false;
        _priceBadge.Visible = false;
        _effectsVBox.Visible = false;
        _divider.Visible = false;

        _keyBadge.Visible = true;
        _actionLabel.Text = "Interact";

        ShowBox();
    }

    public void Refresh(PlayerController player)
    {
        if (_currentProduct != null && GodotObject.IsInstanceValid(_currentProduct))
        {
            ShowProduct(_currentProduct, player);
        }
    }

    private void ShowBox()
    {
        if (!Visible)
        {
            Visible = true;
            Modulate = new Color(1, 1, 1, 0);
            Scale = new Vector2(0.93f, 0.93f);
            PivotOffset = new Vector2(Size.X / 2f, 0);
        }

        _fadeTween?.Kill();
        _fadeTween = CreateTween().SetParallel(true);
        _fadeTween.TweenProperty(this, "modulate:a", 1.0f, 0.08f);
        _fadeTween.TweenProperty(this, "scale", Vector2.One, 0.08f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public void HideBox()
    {
        _currentProduct = null;
        _currentInteractable = null;

        if (!Visible) return;

        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 0.0f, 0.05f);
        _fadeTween.TweenCallback(Callable.From(() => Visible = false));
    }
}
