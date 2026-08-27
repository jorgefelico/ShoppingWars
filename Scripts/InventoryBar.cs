using Godot;

public partial class InventoryBar : CanvasLayer
{
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
