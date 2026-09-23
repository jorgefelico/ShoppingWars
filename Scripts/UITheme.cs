using Godot;
using System;

public static class UITheme
{
    // Fonts
    private static Font _titleFont;
    private static Font _bodyFont;

    public static Font TitleFont
    {
        get
        {
            if (_titleFont == null)
            {
                try
                {
                    _titleFont = GD.Load<Font>("res://Fonts/Bangers-Regular.ttf");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[UITheme] Failed to load Bangers font: {ex.Message}");
                }
            }
            return _titleFont;
        }
    }

    public static Font BodyFont
    {
        get
        {
            if (_bodyFont == null)
            {
                try
                {
                    _bodyFont = GD.Load<Font>("res://Fonts/LilitaOne-Regular.ttf");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[UITheme] Failed to load LilitaOne font: {ex.Message}");
                }
            }
            return _bodyFont;
        }
    }

    // Comic & Retail Color Palette
    public static readonly Color InkBlack = Color.FromHtml("#111118");
    public static readonly Color InkBorder = Color.FromHtml("#181824");
    public static readonly Color PaperWhite = Color.FromHtml("#FAF7EE");
    public static readonly Color PaperCream = Color.FromHtml("#F4EFE2");
    public static readonly Color CardDark = Color.FromHtml("#151622");
    public static readonly Color CardHeader = Color.FromHtml("#1E2032");
    public static readonly Color FlyerYellow = Color.FromHtml("#FFD600");
    public static readonly Color ActionRed = Color.FromHtml("#FF2A4D");
    public static readonly Color CrimsonDark = Color.FromHtml("#8B0E23");
    public static readonly Color FreshGreen = Color.FromHtml("#1FC754");
    public static readonly Color EmeraldDark = Color.FromHtml("#0C6B28");
    public static readonly Color ElectricCyan = Color.FromHtml("#00D2FF");
    public static readonly Color CyanDark = Color.FromHtml("#006A8A");
    public static readonly Color ClearanceOrange = Color.FromHtml("#FF7A00");
    public static readonly Color GoldCash = Color.FromHtml("#FFDF00");
    public static readonly Color SubtitleGray = Color.FromHtml("#8E92A8");

    // Audio SFX players
    private static AudioStream _clickSfx;
    private static AudioStream _hoverSfx;
    private static AudioStream _scanSfx;
    private static AudioStream _buySfx;
    private static AudioStreamPlayer _sfxPlayer;

    private static void EnsureAudioInitialized()
    {
        if (_clickSfx == null)
        {
            try
            {
                _clickSfx = GD.Load<AudioStream>("res://Sounds/ui_click.wav");
                _hoverSfx = GD.Load<AudioStream>("res://Sounds/ui_hover.wav");
                _scanSfx = GD.Load<AudioStream>("res://Sounds/ui_scan.wav");
                _buySfx = GD.Load<AudioStream>("res://Sounds/ui_buy.wav");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[UITheme] Failed to load UI sounds: {ex.Message}");
            }
        }
    }

    private static AudioStreamPlayer GetAudioPlayer()
    {
        if (!GodotObject.IsInstanceValid(_sfxPlayer))
        {
            _sfxPlayer = new AudioStreamPlayer();
            _sfxPlayer.Name = "UI_AudioPlayer";
            _sfxPlayer.Bus = "SFX";
            
            // Add to scene tree root so it persists across scene transitions
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null && tree.Root != null)
            {
                tree.Root.CallDeferred(Node.MethodName.AddChild, _sfxPlayer);
            }
        }
        return _sfxPlayer;
    }

    public static void PlayClick()
    {
        EnsureAudioInitialized();
        var player = GetAudioPlayer();
        if (player != null && _clickSfx != null)
        {
            player.Stream = _clickSfx;
            player.VolumeDb = -4.0f;
            player.Play();
        }
    }

    public static void PlayHover()
    {
        EnsureAudioInitialized();
        var player = GetAudioPlayer();
        if (player != null && _hoverSfx != null)
        {
            player.Stream = _hoverSfx;
            player.VolumeDb = -10.0f;
            player.Play();
        }
    }

    public static void PlayScan()
    {
        EnsureAudioInitialized();
        var player = GetAudioPlayer();
        if (player != null && _scanSfx != null)
        {
            player.Stream = _scanSfx;
            player.VolumeDb = -5.0f;
            player.Play();
        }
    }

    public static void PlayCashRegister()
    {
        EnsureAudioInitialized();
        var player = GetAudioPlayer();
        if (player != null && _buySfx != null)
        {
            player.Stream = _buySfx;
            player.VolumeDb = -3.0f;
            player.Play();
        }
    }

    // StyleBox Generators
    public static StyleBoxFlat CreateComicCard(Color bgColor, Color? borderColor = null, int cornerRadius = 10, int borderWidth = 3, int shadowOffset = 5)
    {
        Color border = borderColor ?? InkBlack;
        var style = new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth + 1,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ShadowColor = new Color(0.04f, 0.04f, 0.07f, 0.85f),
            ShadowSize = shadowOffset,
            ShadowOffset = new Vector2(shadowOffset, shadowOffset)
        };
        return style;
    }

    public static StyleBoxFlat CreatePriceBadgeStyle(Color bgColor, int cornerRadius = 8)
    {
        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = InkBlack,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
            ShadowColor = new Color(0f, 0f, 0f, 0.6f),
            ShadowSize = 2,
            ShadowOffset = new Vector2(2, 2)
        };
    }

    public static StyleBoxFlat CreateReceiptStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PaperCream,
            BorderColor = InkBlack,
            BorderWidthLeft = 3,
            BorderWidthTop = 4,
            BorderWidthRight = 3,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 18,
            ContentMarginBottom = 18,
            ShadowColor = new Color(0.05f, 0.05f, 0.08f, 0.85f),
            ShadowSize = 6,
            ShadowOffset = new Vector2(6, 7)
        };
    }

    // Tactile 3D Arcade Button Styling
    public static void ApplyArcadeButton(
        Button button, 
        Color baseColor, 
        Color? bottomLipColor = null, 
        int fontSize = 16, 
        int cornerRadius = 8,
        bool isPlaySound = true)
    {
        if (button == null) return;

        Color darkLip = bottomLipColor ?? baseColor.Darkened(0.35f);
        Color hoverColor = baseColor.Lightened(0.18f);
        Color pressedColor = baseColor.Darkened(0.20f);

        // Normal style: 3D extruded bottom lip
        var normalStyle = new StyleBoxFlat
        {
            BgColor = baseColor,
            BorderColor = InkBlack,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 5, // 3D Extrusion lip!
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ShadowColor = new Color(0f, 0f, 0f, 0.55f),
            ShadowSize = 3,
            ShadowOffset = new Vector2(2, 3)
        };

        // Hover style: Pop and highlight
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = hoverColor,
            BorderColor = InkBlack,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 6,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ShadowColor = new Color(0f, 0f, 0f, 0.75f),
            ShadowSize = 4,
            ShadowOffset = new Vector2(3, 4)
        };

        // Pressed style: Physically depressed into the shadow!
        var pressedStyle = new StyleBoxFlat
        {
            BgColor = pressedColor,
            BorderColor = InkBlack,
            BorderWidthLeft = 2,
            BorderWidthTop = 4, // Pushed down
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ShadowColor = new Color(0f, 0f, 0f, 0.4f),
            ShadowSize = 1,
            ShadowOffset = new Vector2(1, 1)
        };

        // Focus style: Golden comic ink halo
        var focusStyle = (StyleBoxFlat)normalStyle.Duplicate();
        focusStyle.BorderColor = FlyerYellow;
        focusStyle.BorderWidthLeft = 3;
        focusStyle.BorderWidthTop = 3;
        focusStyle.BorderWidthRight = 3;
        focusStyle.BorderWidthBottom = 6;

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeStyleboxOverride("focus", focusStyle);

        // Font
        if (BodyFont != null)
        {
            button.AddThemeFontOverride("font", BodyFont);
        }
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_outline_color", InkBlack);
        button.AddThemeConstantOverride("outline_size", 4);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", FlyerYellow);

        // Interaction Juice (Punch bounce and sound)
        button.PivotOffset = button.CustomMinimumSize / 2f;

        button.MouseEntered += () =>
        {
            if (isPlaySound) PlayHover();
            button.PivotOffset = button.Size / 2f;
            Tween tw = button.CreateTween();
            tw.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(button, "scale", new Vector2(1.035f, 1.035f), 0.12f);
        };

        button.MouseExited += () =>
        {
            button.PivotOffset = button.Size / 2f;
            Tween tw = button.CreateTween();
            tw.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(button, "scale", Vector2.One, 0.12f);
        };

        button.Pressed += () =>
        {
            if (isPlaySound) PlayClick();
            button.PivotOffset = button.Size / 2f;
            Tween tw = button.CreateTween();
            tw.TweenProperty(button, "scale", new Vector2(0.96f, 0.96f), 0.05f);
            tw.TweenProperty(button, "scale", Vector2.One, 0.1f);
        };
    }

    // Comic Label Formatter
    public static void FormatComicLabel(
        Label label, 
        Font font, 
        int fontSize, 
        Color fontColor, 
        Color? outlineColor = null, 
        int outlineSize = 4,
        Color? shadowColor = null,
        Vector2I? shadowOffset = null)
    {
        if (label == null) return;
        if (font != null) label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", fontColor);

        Color outColor = outlineColor ?? InkBlack;
        label.AddThemeColorOverride("font_outline_color", outColor);
        label.AddThemeConstantOverride("outline_size", outlineSize);

        if (shadowColor.HasValue)
        {
            label.AddThemeColorOverride("font_shadow_color", shadowColor.Value);
            var offset = shadowOffset ?? new Vector2I(2, 2);
            label.AddThemeConstantOverride("shadow_offset_x", offset.X);
            label.AddThemeConstantOverride("shadow_offset_y", offset.Y);
        }
    }
}
