using Godot;
using System.Collections.Generic;

public partial class InventoryBar : CanvasLayer
{
    private int _lastSelectedSlot = -1;
    private PanelContainer _trayPanel;
    private bool _isInitialized = false;

    public override void _Ready()
    {
        InitializeTray();
        Refresh((Inventory)null, 0);
    }

    private void InitializeTray()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        var hBox = GetNodeOrNull<HBoxContainer>("HBoxContainer");
        if (hBox != null)
        {
            // Position bottom-center, elevated slightly above screen edge
            hBox.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            hBox.AnchorLeft = 0.5f;
            hBox.AnchorRight = 0.5f;
            hBox.AnchorTop = 1.0f;
            hBox.AnchorBottom = 1.0f;
            hBox.GrowHorizontal = Control.GrowDirection.Both;
            hBox.GrowVertical = Control.GrowDirection.Begin;
            hBox.OffsetTop = -66;
            hBox.OffsetBottom = -18;
            hBox.Alignment = BoxContainer.AlignmentMode.Center;
            hBox.AddThemeConstantOverride("separation", 8);
        }
    }

    public List<PanelContainer> GetSlotPanels()
    {
        var list = new List<PanelContainer>();
        var hBox = GetNodeOrNull<HBoxContainer>("HBoxContainer");
        if (hBox != null)
        {
            foreach (Node child in hBox.GetChildren())
            {
                if (child is PanelContainer pc && !pc.IsQueuedForDeletion())
                {
                    list.Add(pc);
                }
            }
        }
        return list;
    }

    public void Refresh(Inventory inventory, int currentSelectedItem)
    {
        InitializeTray();
        var panels = GetSlotPanels();

        for (int i = 0; i < panels.Count; i++)
        {
            PanelContainer panel = panels[i];
            panel.CustomMinimumSize = new Vector2(48, 48);
            panel.PivotOffset = new Vector2(24, 24);

            bool isSelected = currentSelectedItem == i;

            // Comic Style for Slot
            StyleBoxFlat style;
            if (isSelected)
            {
                style = new StyleBoxFlat
                {
                    BgColor = new Color(0.24f, 0.22f, 0.32f, 0.98f),
                    BorderColor = UITheme.FlyerYellow,
                    BorderWidthLeft = 3,
                    BorderWidthTop = 3,
                    BorderWidthRight = 3,
                    BorderWidthBottom = 4,
                    CornerRadiusTopLeft = 8,
                    CornerRadiusTopRight = 8,
                    CornerRadiusBottomLeft = 8,
                    CornerRadiusBottomRight = 8,
                    ShadowColor = new Color(1.0f, 0.84f, 0.0f, 0.45f), // Golden aura
                    ShadowSize = 6,
                    ShadowOffset = new Vector2(0, 2)
                };

                if (_lastSelectedSlot != currentSelectedItem)
                {
                    Tween tween = CreateTween();
                    tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    tween.TweenProperty(panel, "scale", new Vector2(1.18f, 1.18f), 0.08f);
                    tween.TweenProperty(panel, "scale", new Vector2(1.08f, 1.08f), 0.12f);
                }
                else
                {
                    panel.Scale = new Vector2(1.08f, 1.08f);
                }
            }
            else
            {
                style = new StyleBoxFlat
                {
                    BgColor = new Color(0.12f, 0.13f, 0.18f, 0.92f),
                    BorderColor = UITheme.InkBlack,
                    BorderWidthLeft = 2,
                    BorderWidthTop = 2,
                    BorderWidthRight = 2,
                    BorderWidthBottom = 4, // 3D bevel
                    CornerRadiusTopLeft = 7,
                    CornerRadiusTopRight = 7,
                    CornerRadiusBottomLeft = 7,
                    CornerRadiusBottomRight = 7,
                    ShadowColor = new Color(0f, 0f, 0f, 0.5f),
                    ShadowSize = 3,
                    ShadowOffset = new Vector2(2, 2)
                };
                panel.Scale = Vector2.One;
            }
            panel.AddThemeStyleboxOverride("panel", style);

            Label label = panel.GetNodeOrNull<Label>("Label");
            TextureRect textureRect = panel.GetNodeOrNull<TextureRect>("TextureRect");
            Label countLabel = panel.GetNodeOrNull<Label>("CountLabel");

            // Format slot hotkey sticker (Top-Left)
            if (label != null)
            {
                label.HorizontalAlignment = HorizontalAlignment.Left;
                label.VerticalAlignment = VerticalAlignment.Top;
                label.Text = $"{i + 1}";
                UITheme.FormatComicLabel(label, UITheme.BodyFont, 11, isSelected ? UITheme.FlyerYellow : new Color(0.8f, 0.8f, 0.85f), UITheme.InkBlack, 2);
            }

            // Format count badge (Bottom-Right)
            if (countLabel == null)
            {
                countLabel = new Label
                {
                    Name = "CountLabel",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                panel.AddChild(countLabel);
            }
            UITheme.FormatComicLabel(countLabel, UITheme.BodyFont, 13, Colors.White, UITheme.InkBlack, 3);

            // Item Icon setup
            if (textureRect != null)
            {
                textureRect.CustomMinimumSize = new Vector2(36, 36);
                textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            }

            Product product = inventory?.GetItem(i);
            int count = inventory != null ? inventory.GetSlotCount(i) : 0;

            if (product != null && count > 0)
            {
                if (product.Icon == null)
                {
                    if (label != null) label.Text = $"{i + 1}\n{product.DisplayName}";
                    if (textureRect != null) textureRect.Texture = null;
                }
                else
                {
                    if (textureRect != null) textureRect.Texture = product.Icon;
                }

                if (product is PotatoGun pg)
                {
                    countLabel.Text = pg.IsReloading ? "🔄 RELOAD" : $"🥔 {pg.CurrentAmmo}";
                    countLabel.Modulate = pg.CurrentAmmo > 0 ? UITheme.FlyerYellow : UITheme.ActionRed;
                }
                else
                {
                    countLabel.Text = count > 1 ? $"x{count}" : "";
                    countLabel.Modulate = Colors.White;
                }
            }
            else
            {
                if (textureRect != null) textureRect.Texture = null;
                countLabel.Text = "";
            }
        }

        _lastSelectedSlot = currentSelectedItem;
    }

    public void Refresh(Product[] products, int currentSelectedItem)
    {
        InitializeTray();
        var panels = GetSlotPanels();

        for (int i = 0; i < panels.Count; i++)
        {
            PanelContainer panel = panels[i];
            panel.CustomMinimumSize = new Vector2(48, 48);
            panel.PivotOffset = new Vector2(24, 24);

            bool isSelected = currentSelectedItem == i;
            var style = new StyleBoxFlat
            {
                BgColor = isSelected ? new Color(0.24f, 0.22f, 0.32f, 0.98f) : new Color(0.12f, 0.13f, 0.18f, 0.92f),
                BorderColor = isSelected ? UITheme.FlyerYellow : UITheme.InkBlack,
                BorderWidthLeft = isSelected ? 3 : 2,
                BorderWidthTop = isSelected ? 3 : 2,
                BorderWidthRight = isSelected ? 3 : 2,
                BorderWidthBottom = isSelected ? 4 : 4,
                CornerRadiusTopLeft = 7,
                CornerRadiusTopRight = 7,
                CornerRadiusBottomLeft = 7,
                CornerRadiusBottomRight = 7,
                ShadowColor = isSelected ? new Color(1.0f, 0.84f, 0f, 0.45f) : new Color(0f, 0f, 0f, 0.5f),
                ShadowSize = isSelected ? 6 : 3,
                ShadowOffset = new Vector2(0, 2)
            };
            panel.AddThemeStyleboxOverride("panel", style);

            Label label = panel.GetNodeOrNull<Label>("Label");
            TextureRect textureRect = panel.GetNodeOrNull<TextureRect>("TextureRect");
            Label countLabel = panel.GetNodeOrNull<Label>("CountLabel");

            if (label != null)
            {
                label.Text = $"{i + 1}";
                UITheme.FormatComicLabel(label, UITheme.BodyFont, 11, isSelected ? UITheme.FlyerYellow : new Color(0.8f, 0.8f, 0.85f), UITheme.InkBlack, 2);
            }

            if (countLabel != null)
            {
                countLabel.Text = "";
            }

            if (textureRect != null)
            {
                textureRect.CustomMinimumSize = new Vector2(36, 36);
                textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            }

            if (products != null && i < products.Length && products[i] != null)
            {
                if (products[i].Icon == null)
                {
                    if (label != null) label.Text = $"{i + 1}\n{products[i].DisplayName}";
                    if (textureRect != null) textureRect.Texture = null;
                }
                else
                {
                    if (textureRect != null) textureRect.Texture = products[i].Icon;
                }
            }
            else
            {
                if (textureRect != null) textureRect.Texture = null;
            }
        }
    }

    public void EnsureSlots(int count)
    {
        var hBox = GetNodeOrNull<HBoxContainer>("HBoxContainer");
        if (hBox == null) return;

        var panels = GetSlotPanels();
        while (panels.Count < count && panels.Count > 0)
        {
            int newIndex = panels.Count;
            PanelContainer template = panels[panels.Count - 1];
            if (template == null) break;

            PanelContainer newPanel = template.Duplicate() as PanelContainer;
            newPanel.Name = $"Slot{newIndex + 1}";
            Label label = newPanel.GetNodeOrNull<Label>("Label");
            if (label != null) label.Text = (newIndex + 1).ToString();
            TextureRect tr = newPanel.GetNodeOrNull<TextureRect>("TextureRect");
            if (tr != null) tr.Texture = null;
            Label countLbl = newPanel.GetNodeOrNull<Label>("CountLabel");
            if (countLbl != null) countLbl.Text = "";
            hBox.AddChild(newPanel);
            panels.Add(newPanel);
        }

        while (panels.Count > count && panels.Count > 0)
        {
            PanelContainer extra = panels[panels.Count - 1];
            panels.RemoveAt(panels.Count - 1);
            hBox.RemoveChild(extra);
            extra.QueueFree();
        }
    }
}
