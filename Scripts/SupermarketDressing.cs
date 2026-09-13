using Godot;
using System.Collections.Generic;

public partial class SupermarketDressing : Node
{
    public override void _Ready()
    {
        // Defer dressing to ensure all parent scene nodes are fully loaded
        Callable.From(ApplyStoreDressing).CallDeferred();
    }

    private void ApplyStoreDressing()
    {
        SetupDepartmentBanners();
        SetupAisleSigns();
        SetupShelfSaleStickers();
    }

    private void SetupDepartmentBanners()
    {
        var bannersRoot = GetTree().CurrentScene?.FindChild("Lighting_And_Banners", true, false);
        if (bannersRoot == null) return;

        var deptConfigs = new Dictionary<string, (string Title, Color Color)>
        {
            { "Banner_Produce", ("🥬 FRESH PRODUCE", new Color(0.12f, 0.62f, 0.22f)) },
            { "Banner_DeliBakery", ("🥖 BAKERY & DELI", new Color(0.85f, 0.55f, 0.12f)) },
            { "Banner_Electronics", ("⚡ TECH & GADGETS", new Color(0.06f, 0.58f, 0.95f)) },
            { "Banner_Pharmacy", ("💊 HEALTH & PHARMACY", new Color(0.08f, 0.65f, 0.62f)) },
            { "Banner_Grocery", ("🛒 PANTRY & SNACKS", new Color(0.92f, 0.18f, 0.24f)) },
            { "Banner_Apparel", ("👕 APPAREL & GEAR", new Color(0.55f, 0.25f, 0.85f)) },
            { "Banner_HomeLiving", ("🏠 HOME & HARDWARE", new Color(0.95f, 0.42f, 0.08f)) },
            { "Banner_Checkout", ("💳 EXPRESS CHECKOUT", new Color(0.08f, 0.38f, 0.85f)) }
        };

        foreach (Node child in bannersRoot.GetChildren())
        {
            if (child is not MeshInstance3D meshInstance) continue;
            string nodeName = meshInstance.Name.ToString();

            if (!deptConfigs.TryGetValue(nodeName, out var config)) continue;

            // Apply vibrant toon-shaded department color to the banner board
            var mat = new StandardMaterial3D
            {
                DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Toon,
                AlbedoColor = config.Color,
                Roughness = 0.35f,
                Metallic = 0.15f,
                RimEnabled = true,
                Rim = 0.35f,
                RimTint = 0.4f
            };
            meshInstance.MaterialOverride = mat;

            // Add bold double-sided 3D text (Front & Back)
            AddBannerLabel(meshInstance, config.Title, new Vector3(0, 0, 0.09f), Vector3.Zero);
            AddBannerLabel(meshInstance, config.Title, new Vector3(0, 0, -0.09f), new Vector3(0, 180, 0));
        }
    }

    private static void AddBannerLabel(Node3D parent, string text, Vector3 localPos, Vector3 localRotDegrees)
    {
        var label = new Label3D
        {
            Text = text,
            FontSize = 54,
            OutlineSize = 14,
            OutlineModulate = Colors.Black,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            DoubleSided = true,
            Position = localPos,
            RotationDegrees = localRotDegrees,
            PixelSize = 0.0055f,
            RenderPriority = 3
        };
        parent.AddChild(label);
    }

    private void SetupAisleSigns()
    {
        var currentScene = GetTree().CurrentScene;
        if (currentScene == null) return;

        // Find all aisle sign meshes across the supermarket
        var aisleSigns = new List<MeshInstance3D>();
        FindMeshesByName(currentScene, "AisleSign", aisleSigns);

        int aisleIndex = 1;
        foreach (var sign in aisleSigns)
        {
            int displayNum = (aisleIndex + 1) / 2; // Pairs of signs per aisle
            aisleIndex++;

            // Create front and back Aisle Number badges
            AddAisleNumberLabel(sign, $"AISLE {displayNum}", new Vector3(0, 0.1f, 0.06f), Vector3.Zero);
            AddAisleNumberLabel(sign, $"AISLE {displayNum}", new Vector3(0, 0.1f, -0.06f), new Vector3(0, 180, 0));
        }
    }

    private static void AddAisleNumberLabel(Node3D parent, string text, Vector3 localPos, Vector3 localRotDegrees)
    {
        var label = new Label3D
        {
            Text = text,
            FontSize = 38,
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Modulate = new Color(1.0f, 0.92f, 0.35f), // Bright yellow on navy blue sign
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            DoubleSided = true,
            Position = localPos,
            RotationDegrees = localRotDegrees,
            PixelSize = 0.005f,
            RenderPriority = 4
        };
        parent.AddChild(label);
    }

    private void SetupShelfSaleStickers()
    {
        var currentScene = GetTree().CurrentScene;
        if (currentScene == null) return;

        var aisles = new List<Node3D>();
        FindNodesByNamePrefix(currentScene, "GondolaAisle", aisles);

        string[] salePhrases = { "SALE!", "HOT BUY", "50% OFF", "VALUE", "$2.99", "$4.99", "CLEARANCE" };
        Color[] badgeColors = {
            new Color(0.95f, 0.15f, 0.15f), // Crimson red
            new Color(1.0f, 0.85f, 0.05f),  // Caution yellow
            new Color(0.15f, 0.75f, 0.25f)  // Green savings
        };

        var rand = new RandomNumberGenerator();
        rand.Seed = 1337;

        foreach (var aisle in aisles)
        {
            // Place 2-4 sale stickers on random shelf tiers per aisle
            int stickerCount = rand.RandiRange(2, 4);
            for (int s = 0; s < stickerCount; s++)
            {
                float zPos = rand.RandfRange(-5.0f, 5.0f);
                float yPos = rand.RandfRange(0.8f, 2.0f);
                bool leftSide = rand.RandiRange(0, 1) == 0;
                float xPos = leftSide ? -0.73f : 0.73f;
                float rotY = leftSide ? -90.0f : 90.0f;

                string phrase = salePhrases[rand.RandiRange(0, salePhrases.Length - 1)];
                Color badgeColor = badgeColors[rand.RandiRange(0, badgeColors.Length - 1)];

                var sticker = new Label3D
                {
                    Text = phrase,
                    FontSize = 26,
                    OutlineSize = 8,
                    OutlineModulate = Colors.Black,
                    Modulate = badgeColor,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                    DoubleSided = true,
                    Position = new Vector3(xPos, yPos, zPos),
                    RotationDegrees = new Vector3(0, rotY, rand.RandfRange(-6.0f, 6.0f)),
                    PixelSize = 0.004f,
                    RenderPriority = 3
                };
                aisle.AddChild(sticker);
            }
        }
    }

    private static void FindMeshesByName(Node root, string nameSubstr, List<MeshInstance3D> results)
    {
        if (root == null) return;
        if (root is MeshInstance3D mi && mi.Name.ToString().Contains(nameSubstr, System.StringComparison.OrdinalIgnoreCase))
        {
            results.Add(mi);
        }
        foreach (Node child in root.GetChildren())
        {
            FindMeshesByName(child, nameSubstr, results);
        }
    }

    private static void FindNodesByNamePrefix(Node root, string prefix, List<Node3D> results)
    {
        if (root == null) return;
        if (root is Node3D n3d && n3d.Name.ToString().StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
        {
            results.Add(n3d);
        }
        foreach (Node child in root.GetChildren())
        {
            FindNodesByNamePrefix(child, prefix, results);
        }
    }
}
