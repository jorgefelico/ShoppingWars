using Godot;

public partial class InventoryBar : CanvasLayer
{
    public void Refresh(Inventory inventory, int currentSelectedItem)
    {
        Godot.Collections.Array<Node> panels = FindChildren("*", "PanelContainer");
        for (int i = 0; i < panels.Count; i++)
        {
            PanelContainer panel = (PanelContainer)panels[i];
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
        Godot.Collections.Array<Node> panels = FindChildren("*", "PanelContainer");
        for (int i = 0; i < panels.Count; i++)
        {
            PanelContainer panel = (PanelContainer)panels[i];
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
}
