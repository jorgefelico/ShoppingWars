using Godot;
using System;
using System.Collections.Generic;

public enum ShelfCategory
{
    Auto,
    Grocery,
    Produce,
    Bakery,
    Beverages,
    HouseholdCleaning,
    Kitchenware,
    HardwareTools,
    Electronics,
    Pharmacy,
    SportingToys,
    MixedMarket,
    Custom
}

[Tool]
public partial class ProceduralShelfFiller : Node3D
{
    // --- Inspector Settings ---

    [ExportGroup("Stocking Settings")]
    [Export] public ShelfCategory Category = ShelfCategory.Auto;

    [Export(PropertyHint.Range, "0.1,1.0,0.05")]
    public float FillDensity = 0.85f;

    [Export(PropertyHint.Range, "0.2,1.2,0.05")]
    public float ItemSpacing = 0.42f;

    [Export] public int MinClusterSize = 2;
    [Export] public int MaxClusterSize = 5;

    [Export] public bool AllowEndcaps = true;

    [Export] public int MaxProductsPerShelfRow = 22;

    [Export] public int CustomSeed = 0;

    [ExportGroup("Custom Products (When Category = Custom)")]
    [Export] public Godot.Collections.Array<PackedScene> CustomProducts = new();

    [ExportGroup("Generation Triggers")]
    [Export] public bool GenerateOnReady = true;
    [Export] public bool AlwaysRegenerateAtRuntime = false;

    [Export]
    public bool TriggerGenerate
    {
        get => false;
        set
        {
            if (value) GenerateStock();
        }
    }

    [Export]
    public bool TriggerClear
    {
        get => false;
        set
        {
            if (value) ClearStock();
        }
    }

    // --- Product Entry Definition ---

    public class ProductEntry
    {
        public string Name;
        public string ScenePath;
        public float Height;
        public float Width;   // Dimension along shelf run
        public float Depth;   // Dimension along shelf depth
        public float Spacing;
        public ShelfCategory[] Categories;
    }

    private static readonly List<ProductEntry> Catalog = new()
    {
        // Grocery
        new() { Name = "CerealBox", ScenePath = "res://Prefabs/Products/CerealBox.tscn", Height = 0.27f, Width = 0.19f, Depth = 0.06f, Spacing = 0.32f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.MixedMarket } },
        new() { Name = "ChipsBag", ScenePath = "res://Prefabs/Products/ChipsBag.tscn", Height = 0.26f, Width = 0.18f, Depth = 0.08f, Spacing = 0.35f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.MixedMarket } },
        new() { Name = "SodaCan", ScenePath = "res://Prefabs/Products/SodaCan.tscn", Height = 0.12f, Width = 0.07f, Depth = 0.07f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.Beverages, ShelfCategory.MixedMarket } },
        new() { Name = "MilkGallon", ScenePath = "res://Prefabs/Products/MilkGallon.tscn", Height = 0.26f, Width = 0.16f, Depth = 0.16f, Spacing = 0.38f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.Beverages, ShelfCategory.MixedMarket } },
        new() { Name = "FrozenPizza", ScenePath = "res://Prefabs/Products/FrozenPizza.tscn", Height = 0.06f, Width = 0.30f, Depth = 0.30f, Spacing = 0.45f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.Bakery, ShelfCategory.MixedMarket } },

        // Produce
        new() { Name = "Apple", ScenePath = "res://Prefabs/Products/Apple.tscn", Height = 0.14f, Width = 0.14f, Depth = 0.14f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket } },
        new() { Name = "Avocado", ScenePath = "res://Prefabs/Products/Avocado.tscn", Height = 0.12f, Width = 0.12f, Depth = 0.12f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket } },
        new() { Name = "Banana", ScenePath = "res://Prefabs/Products/Banana.tscn", Height = 0.20f, Width = 0.10f, Depth = 0.10f, Spacing = 0.26f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket } },
        new() { Name = "Lemon", ScenePath = "res://Prefabs/Products/Lemon.tscn", Height = 0.10f, Width = 0.10f, Depth = 0.10f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket } },
        new() { Name = "Onion", ScenePath = "res://Prefabs/Products/Onion.tscn", Height = 0.11f, Width = 0.11f, Depth = 0.11f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket } },
        new() { Name = "SweetPotato", ScenePath = "res://Prefabs/Products/SweetPotato.tscn", Height = 0.16f, Width = 0.10f, Depth = 0.10f, Spacing = 0.25f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket } },
        new() { Name = "Watermelon", ScenePath = "res://Prefabs/Products/Watermelon.tscn", Height = 0.77f, Width = 0.58f, Depth = 0.58f, Spacing = 0.70f, Categories = new[] { ShelfCategory.Produce } },

        // Bakery
        new() { Name = "Baguette", ScenePath = "res://Prefabs/Products/Baguette.tscn", Height = 0.55f, Width = 0.10f, Depth = 0.10f, Spacing = 0.32f, Categories = new[] { ShelfCategory.Bakery } },
        new() { Name = "ChocolateCake", ScenePath = "res://Prefabs/Products/ChocolateCake.tscn", Height = 0.12f, Width = 0.24f, Depth = 0.24f, Spacing = 0.38f, Categories = new[] { ShelfCategory.Bakery, ShelfCategory.Grocery } },
        new() { Name = "Glizzy", ScenePath = "res://Prefabs/Products/Glizzy.tscn", Height = 1.15f, Width = 0.12f, Depth = 0.12f, Spacing = 0.35f, Categories = new[] { ShelfCategory.Bakery } },

        // Beverages
        new() { Name = "WineBottle", ScenePath = "res://Prefabs/Products/WineBottle.tscn", Height = 0.32f, Width = 0.08f, Depth = 0.08f, Spacing = 0.26f, Categories = new[] { ShelfCategory.Beverages, ShelfCategory.MixedMarket } },

        // Household & Cleaning
        new() { Name = "DetergentJug", ScenePath = "res://Prefabs/Products/DetergentJug.tscn", Height = 0.28f, Width = 0.18f, Depth = 0.12f, Spacing = 0.35f, Categories = new[] { ShelfCategory.HouseholdCleaning, ShelfCategory.MixedMarket } },
        new() { Name = "SprayPaint", ScenePath = "res://Prefabs/Products/SprayPaint.tscn", Height = 0.20f, Width = 0.07f, Depth = 0.07f, Spacing = 0.25f, Categories = new[] { ShelfCategory.HouseholdCleaning, ShelfCategory.HardwareTools } },
        new() { Name = "WetFloorSign", ScenePath = "res://Prefabs/Products/WetFloorSign.tscn", Height = 0.62f, Width = 0.35f, Depth = 0.30f, Spacing = 0.50f, Categories = new[] { ShelfCategory.HouseholdCleaning } },
        new() { Name = "RubberDuck", ScenePath = "res://Prefabs/Products/RubberDuck.tscn", Height = 0.10f, Width = 0.12f, Depth = 0.10f, Spacing = 0.24f, Categories = new[] { ShelfCategory.HouseholdCleaning, ShelfCategory.SportingToys } },

        // Kitchenware
        new() { Name = "CookingPot", ScenePath = "res://Prefabs/Products/CookingPot.tscn", Height = 0.18f, Width = 0.28f, Depth = 0.28f, Spacing = 0.42f, Categories = new[] { ShelfCategory.Kitchenware, ShelfCategory.MixedMarket } },
        new() { Name = "FryingPan", ScenePath = "res://Prefabs/Products/FryingPan.tscn", Height = 0.08f, Width = 0.40f, Depth = 0.27f, Spacing = 0.46f, Categories = new[] { ShelfCategory.Kitchenware } },
        new() { Name = "Toaster", ScenePath = "res://Prefabs/Products/Toaster.tscn", Height = 0.17f, Width = 0.24f, Depth = 0.14f, Spacing = 0.36f, Categories = new[] { ShelfCategory.Kitchenware, ShelfCategory.Electronics } },

        // Hardware & Tools
        new() { Name = "PowerDrill", ScenePath = "res://Prefabs/Products/PowerDrill.tscn", Height = 0.28f, Width = 0.27f, Depth = 0.09f, Spacing = 0.38f, Categories = new[] { ShelfCategory.HardwareTools } },
        new() { Name = "Crowbar", ScenePath = "res://Prefabs/Products/Crowbar.tscn", Height = 0.60f, Width = 0.10f, Depth = 0.05f, Spacing = 0.30f, Categories = new[] { ShelfCategory.HardwareTools } },
        new() { Name = "PipeWrench", ScenePath = "res://Prefabs/Products/PipeWrench.tscn", Height = 0.48f, Width = 0.12f, Depth = 0.06f, Spacing = 0.30f, Categories = new[] { ShelfCategory.HardwareTools } },
        new() { Name = "Sledgehammer", ScenePath = "res://Prefabs/Products/Sledgehammer.tscn", Height = 0.85f, Width = 0.24f, Depth = 0.10f, Spacing = 0.45f, Categories = new[] { ShelfCategory.HardwareTools } },
        new() { Name = "PropaneTank", ScenePath = "res://Prefabs/Products/PropaneTank.tscn", Height = 0.48f, Width = 0.30f, Depth = 0.30f, Spacing = 0.45f, Categories = new[] { ShelfCategory.HardwareTools } },
        new() { Name = "FireExtinguisher", ScenePath = "res://Prefabs/Products/FireExtinguisher.tscn", Height = 0.44f, Width = 0.16f, Depth = 0.16f, Spacing = 0.38f, Categories = new[] { ShelfCategory.HardwareTools, ShelfCategory.HouseholdCleaning } },

        // Electronics
        new() { Name = "FlatScreenTV", ScenePath = "res://Prefabs/Products/FlatScreenTV.tscn", Height = 0.46f, Width = 0.68f, Depth = 0.18f, Spacing = 0.75f, Categories = new[] { ShelfCategory.Electronics } },
        new() { Name = "Boombox", ScenePath = "res://Prefabs/Products/Boombox.tscn", Height = 0.26f, Width = 0.45f, Depth = 0.14f, Spacing = 0.50f, Categories = new[] { ShelfCategory.Electronics } },
        new() { Name = "AlarmClock", ScenePath = "res://Prefabs/Products/AlarmClock.tscn", Height = 0.16f, Width = 0.16f, Depth = 0.08f, Spacing = 0.30f, Categories = new[] { ShelfCategory.Electronics, ShelfCategory.HouseholdCleaning } },

        // Pharmacy
        new() { Name = "PillBottle", ScenePath = "res://Prefabs/Products/PillBottle.tscn", Height = 0.09f, Width = 0.06f, Depth = 0.06f, Spacing = 0.18f, Categories = new[] { ShelfCategory.Pharmacy, ShelfCategory.MixedMarket } },

        // Sporting & Toys
        new() { Name = "BaseballBat", ScenePath = "res://Prefabs/Products/BaseballBat.tscn", Height = 0.80f, Width = 0.08f, Depth = 0.08f, Spacing = 0.32f, Categories = new[] { ShelfCategory.SportingToys } },
        new() { Name = "Football", ScenePath = "res://Prefabs/Products/Football.tscn", Height = 0.24f, Width = 0.24f, Depth = 0.24f, Spacing = 0.38f, Categories = new[] { ShelfCategory.SportingToys } },
    };

    private static readonly Dictionary<string, PackedScene> LoadedScenes = new();

    public struct ShelfSurfaceInfo
    {
        public string Name;
        public Vector3 SurfaceCenter;
        public float MinRun;       // Safe min position along length (with padding)
        public float MaxRun;       // Safe max position along length (with padding)
        public float SurfaceTopY;
        public float ClearanceY;   // Vertical space to next shelf
        public Vector3 FacingNormal;// Outward facing direction
        public float SafeDepthCenter; // Safe center coordinate along depth axis
        public float SafeDepthMin;    // Inner safe boundary
        public float SafeDepthMax;    // Outer safe boundary
        public bool IsEndcap;
        public bool IsTable;
        public int Level;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;

        if (GenerateOnReady)
        {
            Node existingContainer = GetNodeOrNull("StockContainer");
            if (existingContainer == null || AlwaysRegenerateAtRuntime || existingContainer.GetChildCount() == 0)
            {
                GenerateStock();
            }
        }
    }

    // --- Main Generation ---

    public void GenerateStock()
    {
        ClearStock();

        Node3D target = GetParent() as Node3D ?? this;
        List<ShelfSurfaceInfo> surfaces = DiscoverShelfSurfaces(target);
        if (surfaces.Count == 0)
        {
            GD.Print($"[ShelfFiller] No shelf surfaces found on '{target.Name}'.");
            return;
        }

        ShelfCategory activeCategory = ResolveCategory();

        int seed = CustomSeed != 0 
            ? CustomSeed 
            : (int)((int)Mathf.Round(target.GlobalPosition.X * 100) * 73856093 
                  ^ (int)Mathf.Round(target.GlobalPosition.Z * 100) * 19349663 
                  ^ (uint)target.Name.GetHashCode());
        RandomNumberGenerator rng = new();
        rng.Seed = (ulong)Math.Abs(seed);

        Node3D container = new() { Name = "StockContainer" };
        AddChild(container);
        if (Engine.IsEditorHint() && GetTree()?.EditedSceneRoot != null)
        {
            container.Owner = GetTree().EditedSceneRoot;
        }

        int totalSpawned = 0;

        foreach (var surface in surfaces)
        {
            if (surface.IsEndcap && !AllowEndcaps) continue;

            totalSpawned += StockSurface(surface, container, activeCategory, rng);
        }

        GD.Print($"[ShelfFiller] '{target.Name}' stocked with {totalSpawned} products (Theme: {activeCategory}).");
    }

    public void ClearStock()
    {
        Node existing = GetNodeOrNull("StockContainer");
        if (existing != null)
        {
            existing.QueueFree();
            RemoveChild(existing);
        }
    }

    // --- Stocking Logic ---

    private int StockSurface(ShelfSurfaceInfo surface, Node3D container, ShelfCategory category, RandomNumberGenerator rng)
    {
        List<ProductEntry> candidates = GetCandidatesForSurface(surface, category);
        if (candidates.Count == 0) return 0;

        int spawnedCount = 0;
        float baseFacingAngle = Mathf.Atan2(surface.FacingNormal.X, surface.FacingNormal.Z);

        // For wide tables (produce island, electronics display table), stock 2 or 3 rows across depth
        int depthRows = 1;
        if (surface.IsTable && (surface.SafeDepthMax - surface.SafeDepthMin) >= 1.2f)
        {
            depthRows = (surface.SafeDepthMax - surface.SafeDepthMin) >= 1.8f ? 3 : 2;
        }

        for (int r = 0; r < depthRows; r++)
        {
            float targetDepth = surface.SafeDepthCenter;
            if (depthRows > 1)
            {
                float t = (float)r / (depthRows - 1); // 0 to 1
                targetDepth = Mathf.Lerp(surface.SafeDepthMin, surface.SafeDepthMax, t);
            }

            float currentRun = surface.MinRun;

            while (currentRun < surface.MaxRun && spawnedCount < MaxProductsPerShelfRow)
            {
                ProductEntry prod = candidates[rng.RandiRange(0, candidates.Count - 1)];
                float spacing = prod.Spacing > 0 ? prod.Spacing : ItemSpacing;
                int clusterSize = rng.RandiRange(MinClusterSize, MaxClusterSize);

                for (int i = 0; i < clusterSize && spawnedCount < MaxProductsPerShelfRow; i++)
                {
                    float halfW = prod.Width * 0.5f;
                    float slotRun = currentRun + halfW;

                    // STRICT BOUNDARY CHECK: Never exceed shelf length bounds!
                    if (slotRun + halfW > surface.MaxRun)
                    {
                        currentRun = surface.MaxRun;
                        break;
                    }

                    if (rng.Randf() <= FillDensity)
                    {
                        Vector3 localPos = ComputePosition(surface, slotRun, targetDepth, prod.Depth, rng);
                        float rotJitter = rng.RandfRange(-0.08f, 0.08f); // ~4.5 deg natural jitter

                        SpawnProduct(prod, localPos, baseFacingAngle + rotJitter, container, surface.Name, spawnedCount);
                        spawnedCount++;
                    }

                    currentRun += spacing;
                }

                // Section break
                currentRun += spacing * 0.35f;
            }
        }

        return spawnedCount;
    }

    private Vector3 ComputePosition(ShelfSurfaceInfo surface, float runPos, float depthPos, float itemDepth, RandomNumberGenerator rng)
    {
        float jitterRun = rng.RandfRange(-0.01f, 0.01f);
        float jitterDepth = rng.RandfRange(-0.01f, 0.01f);

        Vector3 pos = Vector3.Zero;
        pos.Y = surface.SurfaceTopY + 0.005f;

        if (surface.FacingNormal.Abs().X > 0.5f)
        {
            // Shelf runs along Z (side shelves). Depth is along X.
            pos.Z = runPos + jitterRun;

            // Clamp X within safe depth bounds
            float safeX = depthPos + jitterDepth;
            float halfD = itemDepth * 0.5f;
            safeX = Mathf.Clamp(safeX, surface.SafeDepthMin + halfD, surface.SafeDepthMax - halfD);
            pos.X = safeX;
        }
        else
        {
            // Shelf runs along X (endcaps & tables). Depth is along Z.
            pos.X = runPos + jitterRun;

            // Clamp Z within safe depth bounds
            float safeZ = depthPos + jitterDepth;
            float halfD = itemDepth * 0.5f;
            safeZ = Mathf.Clamp(safeZ, surface.SafeDepthMin + halfD, surface.SafeDepthMax - halfD);
            pos.Z = safeZ;
        }

        return pos;
    }

    private void SpawnProduct(ProductEntry entry, Vector3 localPos, float rotY, Node3D container, string shelfId, int index)
    {
        PackedScene scene = GetPackedScene(entry.ScenePath);
        if (scene == null) return;

        Node instance = scene.Instantiate();
        if (instance is not Node3D node3D)
        {
            instance.QueueFree();
            return;
        }

        node3D.Name = $"{shelfId}_{entry.Name}_{index}";
        node3D.Position = localPos;
        node3D.Rotation = new Vector3(0, rotY, 0);

        container.AddChild(node3D);

        if (Engine.IsEditorHint() && GetTree()?.EditedSceneRoot != null)
        {
            node3D.Owner = GetTree().EditedSceneRoot;
        }
    }

    private PackedScene GetPackedScene(string path)
    {
        if (LoadedScenes.TryGetValue(path, out PackedScene cached))
        {
            return cached;
        }

        if (ResourceLoader.Exists(path))
        {
            PackedScene scene = GD.Load<PackedScene>(path);
            if (scene != null)
            {
                LoadedScenes[path] = scene;
                return scene;
            }
        }

        return null;
    }

    // --- Category Resolution ---

    private ShelfCategory ResolveCategory()
    {
        if (Category != ShelfCategory.Auto) return Category;

        string hierarchy = GetPath().ToString();
        Node p = GetParent();
        while (p != null)
        {
            hierarchy += "/" + p.Name;
            p = p.GetParent();
        }

        if (hierarchy.Contains("Pharmacy") || hierarchy.Contains("HealthBeauty") || hierarchy.Contains("HBAisle"))
        {
            return ShelfCategory.Pharmacy;
        }

        if (hierarchy.Contains("Packaged_Grocery") || hierarchy.Contains("GroceryAisle"))
        {
            string selfName = (GetParent() != null ? GetParent().Name : Name).ToString();
            if (selfName.Contains("1") || selfName.Contains("2")) return ShelfCategory.Grocery;
            if (selfName.Contains("3") || selfName.Contains("4")) return ShelfCategory.Beverages;
            if (selfName.Contains("5") || selfName.Contains("6")) return ShelfCategory.Bakery;
            if (selfName.Contains("7") || selfName.Contains("8")) return ShelfCategory.Produce;
            return ShelfCategory.Grocery;
        }

        if (hierarchy.Contains("Produce"))
        {
            return ShelfCategory.Produce;
        }

        if (hierarchy.Contains("Electronics"))
        {
            return ShelfCategory.Electronics;
        }

        if (hierarchy.Contains("CenterStore") || hierarchy.Contains("GMAisle"))
        {
            string selfName = (GetParent() != null ? GetParent().Name : Name).ToString();
            if (selfName.Contains("11_") || selfName.Contains("12_")) return ShelfCategory.Beverages;
            if (selfName.Contains("9_") || selfName.Contains("10_")) return ShelfCategory.SportingToys;
            if (selfName.Contains("7_") || selfName.Contains("8_")) return ShelfCategory.Electronics;
            if (selfName.Contains("5_") || selfName.Contains("6_")) return ShelfCategory.Kitchenware;
            if (selfName.Contains("3_") || selfName.Contains("4_")) return ShelfCategory.HouseholdCleaning;
            if (selfName.Contains("1_") || selfName.Contains("2_")) return ShelfCategory.HardwareTools;
            return ShelfCategory.HardwareTools;
        }

        return ShelfCategory.MixedMarket;
    }

    private List<ProductEntry> GetCandidatesForSurface(ShelfSurfaceInfo surface, ShelfCategory category)
    {
        List<ProductEntry> matches = new();

        foreach (var prod in Catalog)
        {
            // Clearance check: product height must not exceed shelf vertical clearance
            if (prod.Height > surface.ClearanceY + 0.04f) continue;

            // Fit check: item width must fit within the shelf run bounds
            if (prod.Width > (surface.MaxRun - surface.MinRun)) continue;

            bool catMatch = false;
            if (category == ShelfCategory.MixedMarket)
            {
                catMatch = true;
            }
            else
            {
                foreach (var c in prod.Categories)
                {
                    if (c == category)
                    {
                        catMatch = true;
                        break;
                    }
                }
            }

            if (catMatch)
            {
                matches.Add(prod);
            }
        }

        if (matches.Count == 0)
        {
            foreach (var prod in Catalog)
            {
                if (prod.Height <= surface.ClearanceY + 0.04f && prod.Width <= (surface.MaxRun - surface.MinRun))
                {
                    matches.Add(prod);
                }
            }
        }

        return matches;
    }

    // --- Shelf Surface Discovery ---

    private List<ShelfSurfaceInfo> DiscoverShelfSurfaces(Node3D root)
    {
        List<ShelfSurfaceInfo> surfaces = new();

        foreach (Node child in root.GetChildren())
        {
            if (child is not MeshInstance3D meshInst) continue;
            string name = meshInst.Name.ToString();

            if (name.StartsWith("Shelf_L_"))
            {
                int level = ParseLevel(name);
                surfaces.Add(CreateGondolaSideShelf(meshInst, level, Vector3.Left));
            }
            else if (name.StartsWith("Shelf_R_"))
            {
                int level = ParseLevel(name);
                surfaces.Add(CreateGondolaSideShelf(meshInst, level, Vector3.Right));
            }
            else if (name.StartsWith("Endcap_N_Shelf"))
            {
                int level = ParseLevel(name);
                surfaces.Add(CreateEndcapShelf(meshInst, level, Vector3.Back));
            }
            else if (name.StartsWith("Endcap_S_Shelf"))
            {
                int level = ParseLevel(name);
                surfaces.Add(CreateEndcapShelf(meshInst, level, Vector3.Forward));
            }
        }

        if (surfaces.Count > 0)
        {
            surfaces.Sort((a, b) => a.SurfaceTopY.CompareTo(b.SurfaceTopY));
            ComputeClearances(surfaces);
            return surfaces;
        }

        // Table / Counter top surface discovery
        MeshInstance3D bestTop = null;
        float maxTopY = float.MinValue;
        foreach (Node child in root.GetChildren())
        {
            if (child is not MeshInstance3D meshInst) continue;
            string lower = meshInst.Name.ToString().ToLower();
            if (lower.Contains("tier") || lower.Contains("top") || lower.Contains("table") || lower.Contains("base") || lower.Contains("bunker") || lower.Contains("shelf"))
            {
                Vector3 size = meshInst.Mesh is BoxMesh bm ? bm.Size : meshInst.GetAabb().Size * meshInst.Scale;
                float topY = meshInst.Position.Y + (size.Y * 0.5f);
                if (topY > maxTopY && size.X >= 0.4f && size.Z >= 0.4f)
                {
                    maxTopY = topY;
                    bestTop = meshInst;
                }
            }
        }

        if (bestTop != null)
        {
            Vector3 size = bestTop.Mesh is BoxMesh bm ? bm.Size : bestTop.GetAabb().Size * bestTop.Scale;
            float topY = bestTop.Position.Y + (size.Y * 0.5f);
            float len = Math.Max(size.X, size.Z);
            float dep = Math.Min(size.X, size.Z);

            surfaces.Add(new ShelfSurfaceInfo
            {
                Name = bestTop.Name,
                SurfaceCenter = bestTop.Position,
                MinRun = bestTop.Position.X - (len * 0.5f) + 0.35f,
                MaxRun = bestTop.Position.X + (len * 0.5f) - 0.35f,
                SurfaceTopY = topY,
                ClearanceY = 1.6f,
                FacingNormal = size.X >= size.Z ? Vector3.Back : Vector3.Right,
                SafeDepthCenter = bestTop.Position.Z,
                SafeDepthMin = bestTop.Position.Z - (dep * 0.5f) + 0.25f,
                SafeDepthMax = bestTop.Position.Z + (dep * 0.5f) - 0.25f,
                IsEndcap = false,
                IsTable = true,
                Level = 1
            });
        }

        return surfaces;
    }

    private static int ParseLevel(string name)
    {
        if (name.EndsWith("1")) return 1;
        if (name.EndsWith("2")) return 2;
        if (name.EndsWith("3")) return 3;
        if (name.EndsWith("4")) return 4;
        return 1;
    }

    private ShelfSurfaceInfo CreateGondolaSideShelf(MeshInstance3D mesh, int level, Vector3 facing)
    {
        Vector3 size = mesh.Mesh is BoxMesh bm ? bm.Size : mesh.GetAabb().Size;
        float topY = mesh.Position.Y + (size.Y * 0.5f);

        // Safe shelf run along Z (leave 0.70m padding from each end to prevent any overhang)
        float halfLen = size.Z * 0.5f;
        float minZ = -halfLen + 0.70f;
        float maxZ = halfLen - 0.70f;

        // Shelf depth bounds along X
        // Shelf is 0.65m wide. Center is at -0.40 (Left) or +0.40 (Right).
        // Center wall is at X = 0 (surface at +/-0.06).
        // Outer shelf front edge is at -0.725 (Left) or +0.725 (Right).
        float safeCenter = facing.X < 0 ? -0.38f : +0.38f;
        float safeMin = facing.X < 0 ? -0.68f : +0.08f;
        float safeMax = facing.X < 0 ? -0.08f : +0.68f;

        return new ShelfSurfaceInfo
        {
            Name = mesh.Name,
            SurfaceCenter = mesh.Position,
            MinRun = minZ,
            MaxRun = maxZ,
            SurfaceTopY = topY,
            ClearanceY = 0.56f,
            FacingNormal = facing,
            SafeDepthCenter = safeCenter,
            SafeDepthMin = safeMin,
            SafeDepthMax = safeMax,
            IsEndcap = false,
            IsTable = false,
            Level = level
        };
    }

    private ShelfSurfaceInfo CreateEndcapShelf(MeshInstance3D mesh, int level, Vector3 facing)
    {
        Vector3 size = mesh.Mesh is BoxMesh bm ? bm.Size : mesh.GetAabb().Size;
        float topY = mesh.Position.Y + (size.Y * 0.5f);

        // Endcap shelf is 1.6m wide across X (from -0.8 to +0.8).
        // Safe run along X: from -0.55 to +0.55 (0.25m padding from each side)
        float minX = -0.55f;
        float maxX = 0.55f;

        // Endcap depth along Z: center is at +/-12.35, depth is 0.50m (from 12.10 to 12.60)
        float safeCenterZ = mesh.Position.Z;
        float safeMinZ = facing.Z > 0 ? safeCenterZ - 0.18f : safeCenterZ - 0.18f;
        float safeMaxZ = facing.Z > 0 ? safeCenterZ + 0.18f : safeCenterZ + 0.18f;

        return new ShelfSurfaceInfo
        {
            Name = mesh.Name,
            SurfaceCenter = mesh.Position,
            MinRun = minX,
            MaxRun = maxX,
            SurfaceTopY = topY,
            ClearanceY = 0.56f,
            FacingNormal = facing,
            SafeDepthCenter = safeCenterZ,
            SafeDepthMin = safeMinZ,
            SafeDepthMax = safeMaxZ,
            IsEndcap = true,
            IsTable = false,
            Level = level
        };
    }

    private static void ComputeClearances(List<ShelfSurfaceInfo> surfaces)
    {
        for (int i = 0; i < surfaces.Count; i++)
        {
            var s = surfaces[i];
            float nextY = float.MaxValue;

            for (int j = 0; j < surfaces.Count; j++)
            {
                if (i == j) continue;
                var other = surfaces[j];
                if (other.FacingNormal == s.FacingNormal && other.IsEndcap == s.IsEndcap)
                {
                    if (other.SurfaceTopY > s.SurfaceTopY && other.SurfaceTopY < nextY)
                    {
                        nextY = other.SurfaceTopY;
                    }
                }
            }

            if (nextY < float.MaxValue)
            {
                s.ClearanceY = nextY - s.SurfaceTopY - 0.04f;
            }
            else
            {
                // Top shelf has open ceiling clearance
                s.ClearanceY = 1.4f;
            }

            surfaces[i] = s;
        }
    }
}
