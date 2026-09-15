using Godot;
using System.Collections.Generic;

public static class StylizationHelper
{
    private static Shader _inkOutlineShader;
    public static Shader InkOutlineShader
    {
        get
        {
            if (_inkOutlineShader == null && ResourceLoader.Exists("res://Shaders/ink_outline.gdshader"))
            {
                _inkOutlineShader = GD.Load<Shader>("res://Shaders/ink_outline.gdshader");
            }
            return _inkOutlineShader;
        }
    }

    private static readonly Dictionary<(float width, Color color), ShaderMaterial> _outlineMatCache = new();
    private static readonly Dictionary<(Material source, ShaderMaterial outline), StandardMaterial3D> _toonMatCache = new();

    public static void ClearCache()
    {
        _outlineMatCache.Clear();
        _toonMatCache.Clear();
    }

    public static ShaderMaterial CreateInkOutlineMaterial(float width = 0.0028f, Color? color = null)
    {
        if (InkOutlineShader == null) return null;
        Color c = color ?? new Color(0.06f, 0.06f, 0.09f, 1.0f);
        var key = (width, c);
        if (_outlineMatCache.TryGetValue(key, out ShaderMaterial cached) && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }

        var mat = new ShaderMaterial
        {
            Shader = InkOutlineShader
        };
        mat.SetShaderParameter("outline_width", width);
        mat.SetShaderParameter("outline_color", c);
        _outlineMatCache[key] = mat;
        return mat;
    }

    private static StandardMaterial3D GetOrCreateToonMaterial(StandardMaterial3D sourceMat, ShaderMaterial outlineMat)
    {
        if (sourceMat == null) return null;
        var key = (sourceMat, outlineMat);
        if (_toonMatCache.TryGetValue(key, out StandardMaterial3D cached) && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }

        var toon = (StandardMaterial3D)sourceMat.Duplicate();
        ConfigureToonMaterial(toon, outlineMat);
        _toonMatCache[key] = toon;
        return toon;
    }

    public static void ApplyToonStylization(Node root, float outlineWidth = 0.0028f, Color? outlineColor = null, bool addOutline = true)
    {
        if (root == null) return;

        List<MeshInstance3D> meshes = FindAllMeshInstances(root);
        ShaderMaterial outlineMat = addOutline ? CreateInkOutlineMaterial(outlineWidth, outlineColor) : null;

        foreach (MeshInstance3D mi in meshes)
        {
            // Skip hover outline meshes, billboards, and glowing rings
            string nodeName = mi.Name.ToString();
            if (nodeName.Contains("Outline", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (nodeName.Contains("Hover", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (nodeName.Contains("Ring", System.StringComparison.OrdinalIgnoreCase)) continue;

            if (mi.MaterialOverride is StandardMaterial3D overrideMat)
            {
                mi.MaterialOverride = GetOrCreateToonMaterial(overrideMat, outlineMat);
            }
            else
            {
                int surfaceCount = mi.Mesh != null ? mi.Mesh.GetSurfaceCount() : mi.GetSurfaceOverrideMaterialCount();
                if (surfaceCount == 0 && mi.Mesh != null) surfaceCount = 1;

                for (int i = 0; i < surfaceCount; i++)
                {
                    Material activeMat = mi.GetSurfaceOverrideMaterial(i) ?? mi.GetActiveMaterial(i);
                    if (activeMat is StandardMaterial3D stdMat)
                    {
                        mi.SetSurfaceOverrideMaterial(i, GetOrCreateToonMaterial(stdMat, outlineMat));
                    }
                    else if (activeMat == null && mi.Mesh != null)
                    {
                        var defaultMat = new StandardMaterial3D();
                        mi.SetSurfaceOverrideMaterial(i, GetOrCreateToonMaterial(defaultMat, outlineMat));
                    }
                }
            }
        }
    }

    private static void ConfigureToonMaterial(StandardMaterial3D mat, ShaderMaterial outlineMat)
    {
        mat.DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon;
        mat.SpecularMode = BaseMaterial3D.SpecularModeEnum.Toon;
        mat.Roughness = Mathf.Clamp(mat.Roughness, 0.18f, 0.45f);
        mat.RimEnabled = true;
        mat.Rim = 0.35f;
        mat.RimTint = 0.5f;

        if (outlineMat != null)
        {
            mat.NextPass = outlineMat;
        }
    }

    public static List<MeshInstance3D> FindAllMeshInstances(Node node)
    {
        var list = new List<MeshInstance3D>();
        CollectMeshesRecursive(node, list);
        return list;
    }

    private static void CollectMeshesRecursive(Node current, List<MeshInstance3D> list)
    {
        if (current == null) return;
        if (current is MeshInstance3D mi)
        {
            list.Add(mi);
        }
        foreach (Node child in current.GetChildren())
        {
            CollectMeshesRecursive(child, list);
        }
    }
}
