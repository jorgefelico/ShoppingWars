using Godot;

public static class Utils
{
    public static MeshInstance3D FindMeshInstance(Node node)
    {
        if (node is MeshInstance3D mi)
            return mi;
        foreach (Node child in node.GetChildren())
        {
            MeshInstance3D found = FindMeshInstance(child);
            if (found != null)
                return found;
        }
        return null;
    }

     public static Label3D CreateHoverLabel(string Text)
    {
        Label3D HoverLabel = new Label3D
        {
            Visible = false,
            Text = Text,

            // Font clarity settings
            FontSize = 128,
            PixelSize = 0.0007f,
            Modulate = Colors.Black,

            // Mipmapping prevents shimmer/crawling at varying camera distances
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,

            // Alpha & Rendering flags
            // Disabled alpha cut avoids hard/aliased scissor edges while high FontSize keeps it solid
            AlphaCut = Label3D.AlphaCutMode.Disabled,
            Shaded = false,          // True darkens pure black or distorts contrast with lighting
            NoDepthTest = true,
            DoubleSided = false,
            OutlineSize = 0,

            // Transform & Sorting
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            SortingOffset = 1.0f    // Ensures it renders clearly in front of objects without Z-fighting
        };

       return HoverLabel;
    }
}