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
}