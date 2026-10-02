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
        SetupDepartmentAccentLighting();
        SetupAisleSigns();
        SetupShelfSaleStickers();
        SetupShelfEdgePriceRails();
        SetupFloorDemarcationAndDecals();
        SetupFreezerCoolerLighting();
        SetupStorefrontEntranceAndSunlight();
    }

    private void SetupDepartmentBanners()
    {
        var bannersRoot = GetTree().CurrentScene?.FindChild("Lighting_And_Banners", true, false);
        if (bannersRoot == null) return;

        var deptConfigs = new Dictionary<string, (string Title, Color Color, Color TextColor)>
        {
            { "Banner_Produce", ("🥬 FRESH PRODUCE", new Color(0.14f, 0.65f, 0.25f), new Color(1.0f, 1.0f, 1.0f)) },
            { "Banner_DeliBakery", ("🥖 BAKERY & DELI", new Color(0.92f, 0.58f, 0.15f), new Color(1.0f, 1.0f, 1.0f)) },
            { "Banner_Electronics", ("⚡ TECH & GADGETS", new Color(0.08f, 0.62f, 0.98f), new Color(1.0f, 1.0f, 1.0f)) },
            { "Banner_Pharmacy", ("💊 HEALTH & PHARMACY", new Color(0.10f, 0.72f, 0.68f), new Color(1.0f, 1.0f, 1.0f)) },
            { "Banner_Grocery", ("🛒 PANTRY & SNACKS", new Color(0.95f, 0.22f, 0.26f), new Color(1.0f, 1.0f, 1.0f)) },
            { "Banner_Apparel", ("👕 APPAREL & GEAR", new Color(0.60f, 0.30f, 0.90f), new Color(1.0f, 1.0f, 1.0f)) },
            { "Banner_HomeLiving", ("🏠 HOME & HARDWARE", new Color(0.98f, 0.46f, 0.12f), new Color(1.0f, 1.0f, 1.0f)) },
            { "Banner_Checkout", ("💳 EXPRESS CHECKOUT", new Color(0.12f, 0.45f, 0.92f), new Color(1.0f, 1.0f, 1.0f)) }
        };

        foreach (Node child in bannersRoot.GetChildren())
        {
            if (child is not MeshInstance3D meshInstance) continue;
            string nodeName = meshInstance.Name.ToString();

            if (!deptConfigs.TryGetValue(nodeName, out var config)) continue;

            // If banner already has child labels authored in scene/prefab, do not re-add
            if (meshInstance.FindChild("Label_Front", false, false) != null) continue;

            // Apply vibrant stylized PBR material to the banner board
            var mat = new StandardMaterial3D
            {
                AlbedoColor = config.Color,
                Roughness = 0.25f,
                Metallic = 0.1f,
                ClearcoatEnabled = true,
                Clearcoat = 0.45f,
                ClearcoatRoughness = 0.15f,
                RimEnabled = true,
                Rim = 0.4f,
                RimTint = 0.35f
            };
            meshInstance.MaterialOverride = mat;

            // Add bold double-sided 3D text (Front & Back)
            AddBannerLabel(meshInstance, config.Title, config.TextColor, new Vector3(0, 0, 0.09f), Vector3.Zero);
            AddBannerLabel(meshInstance, config.Title, config.TextColor, new Vector3(0, 0, -0.09f), new Vector3(0, 180, 0));
        }
    }

    private void SetupDepartmentAccentLighting()
    {
        var bannersRoot = GetTree().CurrentScene?.FindChild("Lighting_And_Banners", true, false);
        if (bannersRoot == null) return;

        var lightConfigs = new Dictionary<string, (Color LightColor, float Energy, float Range, float Attenuation)>
        {
            { "Banner_Produce", (new Color(1.0f, 0.88f, 0.65f), 2.2f, 18.0f, 1.1f) },
            { "Banner_DeliBakery", (new Color(1.0f, 0.82f, 0.55f), 2.2f, 18.0f, 1.1f) },
            { "Banner_Electronics", (new Color(0.25f, 0.72f, 1.0f), 2.5f, 20.0f, 1.0f) },
            { "Banner_Pharmacy", (new Color(0.35f, 0.95f, 0.85f), 2.0f, 18.0f, 1.1f) },
            { "Banner_Grocery", (new Color(1.0f, 0.92f, 0.82f), 2.0f, 18.0f, 1.1f) },
            { "Banner_Apparel", (new Color(0.85f, 0.68f, 1.0f), 2.0f, 18.0f, 1.1f) },
            { "Banner_HomeLiving", (new Color(1.0f, 0.74f, 0.52f), 2.0f, 18.0f, 1.1f) },
            { "Banner_Checkout", (new Color(0.96f, 0.98f, 1.0f), 2.4f, 16.0f, 1.0f) }
        };

        foreach (Node child in bannersRoot.GetChildren())
        {
            if (child is not MeshInstance3D banner) continue;
            string nodeName = banner.Name.ToString();

            if (!lightConfigs.TryGetValue(nodeName, out var cfg)) continue;

            // If banner already has an accent light authored in scene, do not add duplicate
            if (banner.FindChild($"{nodeName}_AccentLight", false, false) != null) continue;

            // Add downward accent spotlight illuminating the department zone
            var spot = new SpotLight3D
            {
                Name = $"{nodeName}_AccentLight",
                LightColor = cfg.LightColor,
                LightEnergy = cfg.Energy,
                SpotRange = cfg.Range,
                SpotAngle = 55.0f,
                SpotAttenuation = cfg.Attenuation,
                ShadowEnabled = false,
                Position = new Vector3(0, -1.2f, 0),
                RotationDegrees = new Vector3(-90, 0, 0)
            };
            spot.AddToGroup("StoreLights");
            banner.AddChild(spot);
        }
    }

    private static void AddBannerLabel(Node3D parent, string text, Color textColor, Vector3 localPos, Vector3 localRotDegrees)
    {
        var label = new Label3D
        {
            Text = text,
            FontSize = 54,
            OutlineSize = 12,
            OutlineModulate = new Color(0.05f, 0.05f, 0.08f, 0.95f),
            Modulate = textColor,
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

        var aisleSigns = new List<MeshInstance3D>();
        FindMeshesByName(currentScene, "AisleSign", aisleSigns);

        int aisleIndex = 1;
        foreach (var sign in aisleSigns)
        {
            int displayNum = (aisleIndex + 1) / 2;
            aisleIndex++;

            var front = sign.GetNodeOrNull<Label3D>("Label_Front");
            var back = sign.GetNodeOrNull<Label3D>("Label_Back");
            if (front != null && back != null)
            {
                front.Text = $"AISLE {displayNum}";
                back.Text = $"AISLE {displayNum}";
                continue;
            }

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
            OutlineSize = 8,
            OutlineModulate = new Color(0.05f, 0.05f, 0.08f, 0.95f),
            Modulate = new Color(1.0f, 0.92f, 0.35f),
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
            new Color(0.95f, 0.18f, 0.18f),
            new Color(1.0f, 0.85f, 0.10f),
            new Color(0.18f, 0.78f, 0.28f)
        };

        var rand = new RandomNumberGenerator();
        rand.Seed = 1337;

        foreach (var aisle in aisles)
        {
            int stickerCount = rand.RandiRange(3, 5);
            for (int s = 0; s < stickerCount; s++)
            {
                float zPos = rand.RandfRange(-5.5f, 5.5f);
                float yPos = rand.RandfRange(0.45f, 2.25f);
                bool leftSide = rand.RandiRange(0, 1) == 0;
                float xPos = leftSide ? -0.74f : 0.74f;
                float rotY = leftSide ? -90.0f : 90.0f;

                string phrase = salePhrases[rand.RandiRange(0, salePhrases.Length - 1)];
                Color badgeColor = badgeColors[rand.RandiRange(0, badgeColors.Length - 1)];

                var sticker = new Label3D
                {
                    Text = phrase,
                    FontSize = 26,
                    OutlineSize = 6,
                    OutlineModulate = new Color(0.08f, 0.08f, 0.1f),
                    Modulate = badgeColor,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                    DoubleSided = true,
                    Position = new Vector3(xPos, yPos, zPos),
                    RotationDegrees = new Vector3(0, rotY, rand.RandfRange(-5.0f, 5.0f)),
                    PixelSize = 0.004f,
                    RenderPriority = 3
                };
                aisle.AddChild(sticker);
            }
        }
    }

    private void SetupShelfEdgePriceRails()
    {
        var currentScene = GetTree().CurrentScene;
        if (currentScene == null) return;

        var aisles = new List<Node3D>();
        FindNodesByNamePrefix(currentScene, "GondolaAisle", aisles);

        // Retail shelf tier heights: 0.40m, 1.00m, 1.60m, 2.20m
        float[] tierHeights = { 0.40f, 1.00f, 1.60f, 2.20f };
        Color[] railColors = {
            new Color(0.85f, 0.15f, 0.18f), // Red promo rail
            new Color(0.12f, 0.45f, 0.85f), // Blue standard rail
            new Color(0.15f, 0.65f, 0.25f)  // Green fresh rail
        };

        var rand = new RandomNumberGenerator();
        rand.Seed = 4242;

        foreach (var aisle in aisles)
        {
            float length = aisle.Name.ToString().Contains("24m") ? 23.6f : 13.6f;
            Color railColor = railColors[rand.RandiRange(0, railColors.Length - 1)];

            var railMat = new StandardMaterial3D
            {
                AlbedoColor = railColor,
                Roughness = 0.3f,
                ClearcoatEnabled = true,
                Clearcoat = 0.35f,
                ClearcoatRoughness = 0.1f
            };

            foreach (float y in tierHeights)
            {
                // Left side front edge (X = -0.73m)
                var railL = CreateShelfRailMesh(length, railMat);
                railL.Position = new Vector3(-0.73f, y - 0.01f, 0);
                aisle.AddChild(railL);

                // Right side front edge (X = +0.73m)
                var railR = CreateShelfRailMesh(length, railMat);
                railR.Position = new Vector3(0.73f, y - 0.01f, 0);
                aisle.AddChild(railR);
            }
        }
    }

    private static MeshInstance3D CreateShelfRailMesh(float length, StandardMaterial3D mat)
    {
        var box = new BoxMesh
        {
            Size = new Vector3(0.035f, 0.045f, length),
            Material = mat
        };

        return new MeshInstance3D
        {
            Mesh = box,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
    }

    private void SetupFloorDemarcationAndDecals()
    {
        var currentScene = GetTree().CurrentScene;
        if (currentScene == null) return;

        // 1. Checkout Lane floor waiting stripes
        var checkoutLanes = new List<Node3D>();
        FindNodesByNamePrefix(currentScene, "CheckoutLane", checkoutLanes);

        var yellowStripeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.96f, 0.78f, 0.12f),
            Roughness = 0.35f,
            ClearcoatEnabled = true,
            Clearcoat = 0.4f
        };

        foreach (var lane in checkoutLanes)
        {
            // Floor queue indicator in front of conveyor (Z ~ -2.8m, Y = 0.01m above terrazzo)
            var stripeMesh = new BoxMesh
            {
                Size = new Vector3(1.2f, 0.005f, 0.15f),
                Material = yellowStripeMat
            };
            var stripe = new MeshInstance3D
            {
                Mesh = stripeMesh,
                Position = new Vector3(0, 0.01f, -2.8f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            lane.AddChild(stripe);

            var queueLabel = new Label3D
            {
                Text = "PLEASE WAIT HERE",
                FontSize = 22,
                OutlineSize = 6,
                OutlineModulate = new Color(0.08f, 0.08f, 0.1f),
                Modulate = new Color(1.0f, 0.95f, 0.3f),
                Position = new Vector3(0, 0.015f, -3.1f),
                RotationDegrees = new Vector3(-90, 0, 0),
                PixelSize = 0.0035f,
                RenderPriority = 2
            };
            lane.AddChild(queueLabel);
        }

        // 2. Warehouse loading door caution striping
        var warehouseDoors = new List<Node3D>();
        FindNodesByNamePrefix(currentScene, "WarehouseDoors", warehouseDoors);

        foreach (var door in warehouseDoors)
        {
            var hazardMesh = new BoxMesh
            {
                Size = new Vector3(4.0f, 0.005f, 0.4f),
                Material = yellowStripeMat
            };
            var hazardStrip = new MeshInstance3D
            {
                Mesh = hazardMesh,
                Position = new Vector3(0, 0.01f, 0.8f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            door.AddChild(hazardStrip);

            var doorLabel = new Label3D
            {
                Text = "CAUTION - EMPLOYEES ONLY",
                FontSize = 28,
                OutlineSize = 8,
                OutlineModulate = Colors.Black,
                Modulate = new Color(1.0f, 0.85f, 0.15f),
                Position = new Vector3(0, 2.2f, 0.15f),
                PixelSize = 0.0045f,
                RenderPriority = 2
            };
            door.AddChild(doorLabel);
        }
    }

    private void SetupFreezerCoolerLighting()
    {
        var currentScene = GetTree().CurrentScene;
        if (currentScene == null) return;

        var freezers = new List<Node3D>();
        FindNodesByNamePrefix(currentScene, "FreezerBunker", freezers);
        FindNodesByNamePrefix(currentScene, "PerimeterWallCooler", freezers);

        foreach (var freezer in freezers)
        {
            var interiorLight = new OmniLight3D
            {
                Name = "RefrigerationGlow",
                LightColor = new Color(0.72f, 0.92f, 1.0f),
                LightEnergy = 1.4f,
                OmniRange = 3.2f,
                OmniAttenuation = 1.2f,
                ShadowEnabled = false,
                Position = new Vector3(0, 0.65f, 0)
            };
            interiorLight.AddToGroup("StoreLights");
            freezer.AddChild(interiorLight);
        }
    }

    private void SetupStorefrontEntranceAndSunlight()
    {
        var currentScene = GetTree().CurrentScene;
        if (currentScene == null) return;

        // If StorefrontEntrance is already authored in the scene, skip procedural creation
        if (currentScene.FindChild("StorefrontEntrance", true, false) != null)
        {
            return;
        }

        var entranceRoot = new Node3D { Name = "StorefrontEntrance" };
        currentScene.AddChild(entranceRoot);

        var glassMat = GD.Load<Material>("res://Materials/refrigerator_glass.tres");
        var frameMat = GD.Load<Material>("res://Materials/shelf_dark.tres");
        var chromeMat = GD.Load<Material>("res://Materials/clothing_rack_metal.tres");

        float facadeZ = -69.4f;

        // 1. Massive Glass Curtain Wall Facade (26m wide x 6.0m high)
        var glassMesh = new BoxMesh
        {
            Size = new Vector3(26.0f, 6.0f, 0.08f),
            Material = glassMat
        };
        var glassPane = new MeshInstance3D
        {
            Mesh = glassMesh,
            Position = new Vector3(0, 3.1f, facadeZ),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        entranceRoot.AddChild(glassPane);

        // Architectural Aluminum Mullions (Vertical framing every 3.25m)
        for (int i = -4; i <= 4; i++)
        {
            float xPos = i * 3.25f;
            var mullionMesh = new BoxMesh
            {
                Size = new Vector3(0.12f, 6.2f, 0.16f),
                Material = frameMat
            };
            var mullion = new MeshInstance3D
            {
                Mesh = mullionMesh,
                Position = new Vector3(xPos, 3.1f, facadeZ),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On
            };
            entranceRoot.AddChild(mullion);
        }

        // Horizontal Transom Frame Rails
        float[] transomHeights = { 0.1f, 2.8f, 6.1f };
        foreach (float y in transomHeights)
        {
            var transomMesh = new BoxMesh
            {
                Size = new Vector3(26.4f, 0.14f, 0.16f),
                Material = frameMat
            };
            var transom = new MeshInstance3D
            {
                Mesh = transomMesh,
                Position = new Vector3(0, y, facadeZ),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On
            };
            entranceRoot.AddChild(transom);
        }

        // 2. Central Automatic Sliding Doors (2 pairs, at X = -3.25m and X = +3.25m)
        float[] doorCenters = { -3.25f, 3.25f };
        foreach (float cx in doorCenters)
        {
            // Motion sensor header box
            var sensorMesh = new BoxMesh
            {
                Size = new Vector3(2.4f, 0.22f, 0.22f),
                Material = frameMat
            };
            var sensor = new MeshInstance3D
            {
                Mesh = sensorMesh,
                Position = new Vector3(cx, 2.75f, facadeZ + 0.12f)
            };
            entranceRoot.AddChild(sensor);

            // Red LED motion sensor indicator dot
            var sensorDotMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.15f, 0.15f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.2f, 0.2f),
                EmissionEnergyMultiplier = 2.5f
            };
            var dotMesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.06f), Material = sensorDotMat };
            var dot = new MeshInstance3D
            {
                Mesh = dotMesh,
                Position = new Vector3(cx, 2.75f, facadeZ + 0.24f)
            };
            entranceRoot.AddChild(dot);

            // Door caution decal
            var doorDecal = new Label3D
            {
                Text = "⚡ AUTOMATIC DOOR ⚡\nCAUTION: STAND CLEAR",
                FontSize = 20,
                OutlineSize = 6,
                OutlineModulate = Colors.Black,
                Modulate = new Color(1.0f, 0.90f, 0.2f),
                Position = new Vector3(cx, 1.45f, facadeZ + 0.06f),
                PixelSize = 0.0035f,
                RenderPriority = 3
            };
            entranceRoot.AddChild(doorDecal);
        }

        // 3. Welcome Marquee Signboard Above Entrance
        var portalBoardMesh = new BoxMesh
        {
            Size = new Vector3(14.0f, 1.4f, 0.25f),
            Material = frameMat
        };
        var portalBoard = new MeshInstance3D
        {
            Mesh = portalBoardMesh,
            Position = new Vector3(0, 7.0f, facadeZ)
        };
        entranceRoot.AddChild(portalBoard);

        var welcomeLabel = new Label3D
        {
            Text = "🛒 WELCOME TO SUPERMART #404 🛒\nHOME OF THE MANAGER'S SPECIAL",
            FontSize = 38,
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Modulate = new Color(1.0f, 0.95f, 0.85f),
            Position = new Vector3(0, 7.0f, facadeZ + 0.14f),
            PixelSize = 0.0055f,
            RenderPriority = 4
        };
        entranceRoot.AddChild(welcomeLabel);

        // 4. Chrome Guard Rails / Cart Corral Flanking
        float[] railX = { -13.5f, 13.5f };
        foreach (float rx in railX)
        {
            var guardMesh = new CylinderMesh
            {
                TopRadius = 0.05f,
                BottomRadius = 0.05f,
                Height = 1.1f,
                Material = chromeMat
            };
            var guard = new MeshInstance3D
            {
                Mesh = guardMesh,
                Position = new Vector3(rx, 0.55f, facadeZ + 1.2f)
            };
            entranceRoot.AddChild(guard);
        }

        // 5. Cinematic Sunlight Shaft (Volumetric God Rays Stream)
        var sunbeam = new SpotLight3D
        {
            Name = "EntranceSunlightShaft",
            LightColor = new Color(1.0f, 0.92f, 0.78f), // Warm golden morning sunlight
            LightEnergy = 4.8f,
            LightVolumetricFogEnergy = 3.6f, // Dramatic visible god rays cutting through aisle mist
            SpotRange = 55.0f,
            SpotAngle = 65.0f,
            SpotAttenuation = 1.05f,
            ShadowEnabled = true,
            ShadowBias = 0.06f,
            ShadowNormalBias = 1.0f,
            ShadowBlur = 1.5f,
            Position = new Vector3(0, 8.5f, -76.0f),
            RotationDegrees = new Vector3(-28.0f, 180.0f, 0) // Angled down and into the store
        };
        entranceRoot.AddChild(sunbeam);
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
