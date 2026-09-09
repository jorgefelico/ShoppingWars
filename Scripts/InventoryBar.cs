using Godot;
using System.Collections.Generic;

public partial class InventoryBar : CanvasLayer
{
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
        var panels = GetSlotPanels();
        for (int i = 0; i < panels.Count; i++)
        {
            PanelContainer panel = panels[i];
            StyleBoxFlat style = (panel.GetThemeStylebox("panel") as StyleBoxFlat).Duplicate() as StyleBoxFlat;
            if (currentSelectedItem == i)
            {
                style.BorderColor = Color.Color8(255, 223, 0);
            }
            else
            {
                style.BorderColor = Color.Color8(0, 0, 0);
            }
            panel.AddThemeStyleboxOverride("panel", style);

            Label label = panel.GetNodeOrNull<Label>("Label");
            TextureRect textureRect = panel.GetNodeOrNull<TextureRect>("TextureRect");
            Label countLabel = panel.GetNodeOrNull<Label>("CountLabel");

            if (countLabel == null)
            {
                countLabel = new Label
                {
                    Name = "CountLabel",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                countLabel.AddThemeFontSizeOverride("font_size", 13);
                countLabel.AddThemeColorOverride("font_color", Colors.White);
                countLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
                countLabel.AddThemeConstantOverride("outline_size", 4);
                panel.AddChild(countLabel);
            }

            Product product = inventory?.GetItem(i);
            int count = inventory != null ? inventory.GetSlotCount(i) : 0;

            if (product != null && count > 0)
            {
                if (product.Icon == null)
                {
                    if (label != null) label.Text = product.DisplayName;
                    if (textureRect != null) textureRect.Texture = null;
                }
                else
                {
                    if (label != null) label.Text = "";
                    if (textureRect != null) textureRect.Texture = product.Icon;
                }

                countLabel.Text = count > 1 ? count.ToString() : "";
            }
            else
            {
                if (label != null) label.Text = (i + 1).ToString();
                if (textureRect != null) textureRect.Texture = null;
                countLabel.Text = "";
            }
        }
    }

    public void Refresh(Product[] products, int currentSelectedItem)
    {
        var panels = GetSlotPanels();
        for (int i = 0; i < panels.Count; i++)
        {
            PanelContainer panel = panels[i];
            StyleBoxFlat style = (panel.GetThemeStylebox("panel") as StyleBoxFlat).Duplicate() as StyleBoxFlat;
            if (currentSelectedItem == i)
            {
                style.BorderColor = Color.Color8(255, 223, 0);
            }
            else
            {
                style.BorderColor = Color.Color8(0, 0, 0);
            }
            panel.AddThemeStyleboxOverride("panel", style);

            Label label = panel.GetNodeOrNull<Label>("Label");
            TextureRect textureRect = panel.GetNodeOrNull<TextureRect>("TextureRect");
            Label countLabel = panel.GetNodeOrNull<Label>("CountLabel");

            if (countLabel == null)
            {
                countLabel = new Label
                {
                    Name = "CountLabel",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                countLabel.AddThemeFontSizeOverride("font_size", 13);
                countLabel.AddThemeColorOverride("font_color", Colors.White);
                countLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
                countLabel.AddThemeConstantOverride("outline_size", 4);
                panel.AddChild(countLabel);
            }

            if (products != null && i < products.Length && products[i] != null)
            {
                if (products[i].Icon == null)
                {
                    if (label != null) label.Text = products[i].DisplayName;
                    if (textureRect != null) textureRect.Texture = null;
                }
                else
                {
                    if (label != null) label.Text = "";
                    if (textureRect != null) textureRect.Texture = products[i].Icon;
                }
                countLabel.Text = "";
            }
            else
            {
                if (label != null) label.Text = (i + 1).ToString();
                if (textureRect != null) textureRect.Texture = null;
                countLabel.Text = "";
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
