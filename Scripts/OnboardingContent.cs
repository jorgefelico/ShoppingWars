using Godot;

// Shared onboarding card content (title + rules/controls columns).
// Used by the main menu pre-match popup and the in-game [H] guide modal so the
// copy is defined exactly once. Callers wrap it in their own backdrop and add
// their own footer confirm button below the returned VBox.
public static class OnboardingContent
{
    public static StyleBoxFlat CreateCardStyle()
    {
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
        return cardStyle;
    }

    public static VBoxContainer BuildCardBody()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);

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
            "🤖 10. HAZARDS: Dodge Groombas, laser cameras, the runaway Cartsferatu cart & store events!"
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
            ("[ H ]", "Open / Close this Guide Anytime"),
            ("[ ESC ]", "Settings / Match Menu")
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

        return vbox;
    }
}
