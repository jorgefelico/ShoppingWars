using Godot;

public partial class InventoryBar : CanvasLayer
{
	public void Refresh(Product[] products, int currentSelectedItem)
	{
		Godot.Collections.Array<Node> panels = FindChildren("*", "Panel");
		for (int i = 0; i < panels.Count; i++)
		{
			Panel panel = (Panel)panels[i];
			StyleBoxFlat style = (panel.GetThemeStylebox("panel") as StyleBoxFlat).Duplicate() as StyleBoxFlat;
			if (currentSelectedItem == i)
			{
				style.BorderColor = Color.Color8(255, 223, 0);
				style.BorderWidthTop = 3;
				style.BorderWidthRight = 3;
				style.BorderWidthBottom = 3;
				style.BorderWidthLeft = 3;
			}
			else
			{
				style.BorderColor = Color.Color8(0, 0, 0);
				style.BorderWidthTop = 1;
				style.BorderWidthRight = 1;
				style.BorderWidthBottom = 1;
				style.BorderWidthLeft = 1;
			}
			panel.AddThemeStyleboxOverride("panel", style);

			Label label = (Label)panels[i].FindChild("Label");
			TextureRect textureRect = (TextureRect)panels[i].FindChild("TextureRect");
			if (label == null || textureRect == null) continue;

			if (products[i] != null)
			{
				if (products[i].Icon == null)
				{
					label.Text = products[i].DisplayName;
					textureRect.Texture = null;
				}
				else
				{
					label.Text = "";
					textureRect.Texture = products[i].Icon;
				}
			}
			else
			{
				label.Text = (i + 1).ToString();
				textureRect.Texture = null;
			}
		}
	}
}
