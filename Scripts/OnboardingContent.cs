using Godot;

// Shared onboarding card content (title + rules/controls columns).
// Used by the main menu pre-match popup and the in-game [H] guide modal so the
// copy is defined exactly once. Callers wrap it in their own backdrop and add
// their own footer confirm button below the returned VBox.
public static class OnboardingContent
{
    public static StyleBoxFlat CreateCardStyle()
    {
        var cardStyle = UITheme.CreateComicCard(UITheme.CardDark, UITheme.FlyerYellow, 12, 4, 8);
        cardStyle.ContentMarginLeft = 28;
        cardStyle.ContentMarginRight = 28;
        cardStyle.ContentMarginTop = 22;
        cardStyle.ContentMarginBottom = 22;
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
        UITheme.FormatComicLabel(title, UITheme.TitleFont, 32, UITheme.FlyerYellow, UITheme.InkBlack, 4, UITheme.InkBlack, new Vector2I(3, 3));
        vbox.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = "SUPERMARKET BATTLE ROYALE — PLAYER GUIDE & CONTROLS";
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        UITheme.FormatComicLabel(subtitle, UITheme.BodyFont, 13, UITheme.SubtitleGray, UITheme.InkBlack, 2);
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
        UITheme.FormatComicLabel(leftTitle, UITheme.BodyFont, 16, UITheme.ElectricCyan, UITheme.InkBlack, 3);
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
            UITheme.FormatComicLabel(lbl, UITheme.BodyFont, 12, UITheme.PaperWhite, UITheme.InkBlack, 2);
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
        UITheme.FormatComicLabel(rightTitle, UITheme.BodyFont, 16, UITheme.FlyerYellow, UITheme.InkBlack, 3);
        rightCol.AddChild(rightTitle);

        (string key, string desc)[] controls = new (string, string)[]
        {
            ("[ W A S D ]", "Move Around"),
            ("[ SHIFT ]", "Sprint (Faster Movement)"),
            ("[ SPACE ]", "Jump"),
            ("[ E ]", "Interact / Buy / Scavenge"),
            ("[ LMB / Left Click ]", "Throw Weapon / Fire Potato Gun"),
            ("[ RMB / Right Click ]", "Melee Swing / PVC Bash"),
            ("[ R ]", "Eat / Drink / Reload Gun"),
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
            UITheme.FormatComicLabel(keyLbl, UITheme.BodyFont, 12, UITheme.FlyerYellow, UITheme.InkBlack, 2);
            row.AddChild(keyLbl);

            var descLbl = new Label();
            descLbl.Text = ctrl.desc;
            UITheme.FormatComicLabel(descLbl, UITheme.BodyFont, 12, UITheme.PaperWhite, UITheme.InkBlack, 2);
            row.AddChild(descLbl);

            rightCol.AddChild(row);
        }

        vbox.AddChild(new HSeparator());

        return vbox;
    }
}
