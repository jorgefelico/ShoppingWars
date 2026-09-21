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
    public float FillDensity = 0.80f;

    [Export(PropertyHint.Range, "0.2,1.2,0.05")]
    public float ItemSpacing = 0.42f;

    [Export] public int MinClusterSize = 2;
    [Export] public int MaxClusterSize = 3;

    [Export] public bool AllowEndcaps = true;

    [Export] public int MaxProductsPerShelfRow = 4;

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
        public int Weight = 50;
        public int MaxStoreCount = -1; // -1 for unlimited
    }

    public static int CurrentMatchSeed = 0;
    public static int TotalGlobalSpawned = 0;
    public static int MaxTotalStoreProducts = 450;
    private static readonly Dictionary<string, int> _globalSpawnCounts = new();
    private static readonly Queue<ProceduralShelfFiller> _generationQueue = new();
    private static bool _queueRunnerActive = false;

    public static void ResetGlobalSpawnCounts()
    {
        _globalSpawnCounts.Clear();
        TotalGlobalSpawned = 0;
        _generationQueue.Clear();
        _queueRunnerActive = false;
    }

    public static int GetGlobalSpawnCount(string productName)
    {
        return _globalSpawnCounts.TryGetValue(productName, out int c) ? c : 0;
    }

    public static void IncrementGlobalSpawnCount(string productName)
    {
        _globalSpawnCounts[productName] = GetGlobalSpawnCount(productName) + 1;
    }

    private static void EnqueueForGeneration(ProceduralShelfFiller filler)
    {
        _generationQueue.Enqueue(filler);
        if (!_queueRunnerActive)
        {
            _queueRunnerActive = true;
            Callable.From(ProcessGenerationQueue).CallDeferred();
        }
    }

    private static async void ProcessGenerationQueue()
    {
        const int BatchSize = 6;

        while (_generationQueue.Count > 0)
        {
            int processed = 0;
            SceneTree tree = null;

            while (_generationQueue.Count > 0 && processed < BatchSize)
            {
                var filler = _generationQueue.Dequeue();
                if (GodotObject.IsInstanceValid(filler) && filler.IsInsideTree())
                {
                    filler.GenerateStock();
                    tree ??= filler.GetTree();
                    processed++;
                }
            }

            if (_generationQueue.Count > 0 && tree != null && GodotObject.IsInstanceValid(tree))
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }

        _queueRunnerActive = false;
    }

    private static readonly List<ProductEntry> Catalog = new()
    {
        // Grocery
        new() { Name = "CerealBox", ScenePath = "res://Prefabs/Products/CerealBox.tscn", Height = 0.27f, Width = 0.19f, Depth = 0.06f, Spacing = 0.32f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.MixedMarket }, Weight = 60, MaxStoreCount = 18 },
        new() { Name = "ChipsBag", ScenePath = "res://Prefabs/Products/ChipsBag.tscn", Height = 0.26f, Width = 0.18f, Depth = 0.08f, Spacing = 0.35f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.MixedMarket }, Weight = 60, MaxStoreCount = 18 },
        new() { Name = "SodaCan", ScenePath = "res://Prefabs/Products/SodaCan.tscn", Height = 0.12f, Width = 0.07f, Depth = 0.07f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.Beverages, ShelfCategory.MixedMarket }, Weight = 80, MaxStoreCount = 24 },
        new() { Name = "MilkGallon", ScenePath = "res://Prefabs/Products/MilkGallon.tscn", Height = 0.26f, Width = 0.16f, Depth = 0.16f, Spacing = 0.38f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.Beverages, ShelfCategory.MixedMarket }, Weight = 45, MaxStoreCount = 14 },
        new() { Name = "FrozenPizza", ScenePath = "res://Prefabs/Products/FrozenPizza.tscn", Height = 0.06f, Width = 0.30f, Depth = 0.30f, Spacing = 0.45f, Categories = new[] { ShelfCategory.Grocery, ShelfCategory.Bakery, ShelfCategory.MixedMarket }, Weight = 40, MaxStoreCount = 14 },

        // Produce
        new() { Name = "Apple", ScenePath = "res://Prefabs/Products/Apple.tscn", Height = 0.14f, Width = 0.14f, Depth = 0.14f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket }, Weight = 75, MaxStoreCount = 20 },
        new() { Name = "Avocado", ScenePath = "res://Prefabs/Products/Avocado.tscn", Height = 0.12f, Width = 0.12f, Depth = 0.12f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket }, Weight = 55, MaxStoreCount = 16 },
        new() { Name = "Banana", ScenePath = "res://Prefabs/Products/Banana.tscn", Height = 0.20f, Width = 0.10f, Depth = 0.10f, Spacing = 0.26f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket }, Weight = 65, MaxStoreCount = 18 },
        new() { Name = "Lemon", ScenePath = "res://Prefabs/Products/Lemon.tscn", Height = 0.10f, Width = 0.10f, Depth = 0.10f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket }, Weight = 65, MaxStoreCount = 16 },
        new() { Name = "Onion", ScenePath = "res://Prefabs/Products/Onion.tscn", Height = 0.11f, Width = 0.11f, Depth = 0.11f, Spacing = 0.22f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket }, Weight = 60, MaxStoreCount = 16 },
        new() { Name = "Potato", ScenePath = "res://Prefabs/Products/Potato.tscn", Height = 0.16f, Width = 0.10f, Depth = 0.10f, Spacing = 0.25f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket }, Weight = 60, MaxStoreCount = 18 },
        new() { Name = "SweetPotato", ScenePath = "res://Prefabs/Products/SweetPotato.tscn", Height = 0.16f, Width = 0.10f, Depth = 0.10f, Spacing = 0.25f, Categories = new[] { ShelfCategory.Produce, ShelfCategory.MixedMarket }, Weight = 50, MaxStoreCount = 16 },
        new() { Name = "Watermelon", ScenePath = "res://Prefabs/Products/Watermelon.tscn", Height = 0.77f, Width = 0.58f, Depth = 0.58f, Spacing = 0.70f, Categories = new[] { ShelfCategory.Produce }, Weight = 18, MaxStoreCount = 10 },

        // Bakery
        new() { Name = "Baguette", ScenePath = "res://Prefabs/Products/Baguette.tscn", Height = 0.55f, Width = 0.10f, Depth = 0.10f, Spacing = 0.32f, Categories = new[] { ShelfCategory.Bakery }, Weight = 50, MaxStoreCount = 14 },
        new() { Name = "ChocolateCake", ScenePath = "res://Prefabs/Products/ChocolateCake.tscn", Height = 0.12f, Width = 0.24f, Depth = 0.24f, Spacing = 0.38f, Categories = new[] { ShelfCategory.Bakery, ShelfCategory.Grocery }, Weight = 35, MaxStoreCount = 12 },
        new() { Name = "Glizzy", ScenePath = "res://Prefabs/Products/Glizzy.tscn", Height = 1.15f, Width = 0.12f, Depth = 0.12f, Spacing = 0.35f, Categories = new[] { ShelfCategory.Bakery }, Weight = 45, MaxStoreCount = 12 },

        // Beverages
        new() { Name = "WineBottle", ScenePath = "res://Prefabs/Products/WineBottle.tscn", Height = 0.32f, Width = 0.08f, Depth = 0.08f, Spacing = 0.26f, Categories = new[] { ShelfCategory.Beverages, ShelfCategory.MixedMarket }, Weight = 30, MaxStoreCount = 12 },

        // Household & Cleaning
        new() { Name = "DetergentJug", ScenePath = "res://Prefabs/Products/DetergentJug.tscn", Height = 0.28f, Width = 0.18f, Depth = 0.12f, Spacing = 0.35f, Categories = new[] { ShelfCategory.HouseholdCleaning, ShelfCategory.MixedMarket }, Weight = 40, MaxStoreCount = 14 },
        new() { Name = "SprayPaint", ScenePath = "res://Prefabs/Products/SprayPaint.tscn", Height = 0.20f, Width = 0.07f, Depth = 0.07f, Spacing = 0.25f, Categories = new[] { ShelfCategory.HouseholdCleaning, ShelfCategory.HardwareTools }, Weight = 45, MaxStoreCount = 14 },
        new() { Name = "WetFloorSign", ScenePath = "res://Prefabs/Products/WetFloorSign.tscn", Height = 0.62f, Width = 0.35f, Depth = 0.30f, Spacing = 0.50f, Categories = new[] { ShelfCategory.HouseholdCleaning }, Weight = 25, MaxStoreCount = 8 },
        new() { Name = "RubberDuck", ScenePath = "res://Prefabs/Products/RubberDuck.tscn", Height = 0.10f, Width = 0.12f, Depth = 0.10f, Spacing = 0.24f, Categories = new[] { ShelfCategory.HouseholdCleaning, ShelfCategory.SportingToys }, Weight = 55, MaxStoreCount = 14 },

        // Kitchenware
        new() { Name = "CookingPot", ScenePath = "res://Prefabs/Products/CookingPot.tscn", Height = 0.18f, Width = 0.28f, Depth = 0.28f, Spacing = 0.42f, Categories = new[] { ShelfCategory.Kitchenware, ShelfCategory.MixedMarket }, Weight = 35, MaxStoreCount = 12 },
        new() { Name = "FryingPan", ScenePath = "res://Prefabs/Products/FryingPan.tscn", Height = 0.08f, Width = 0.40f, Depth = 0.27f, Spacing = 0.46f, Categories = new[] { ShelfCategory.Kitchenware }, Weight = 35, MaxStoreCount = 12 },
        new() { Name = "Toaster", ScenePath = "res://Prefabs/Products/Toaster.tscn", Height = 0.17f, Width = 0.24f, Depth = 0.14f, Spacing = 0.36f, Categories = new[] { ShelfCategory.Kitchenware, ShelfCategory.Electronics }, Weight = 30, MaxStoreCount = 12 },

        // Hardware & Tools - Contested Power Weapons!
        new() { Name = "PowerDrill", ScenePath = "res://Prefabs/Products/PowerDrill.tscn", Height = 0.28f, Width = 0.27f, Depth = 0.09f, Spacing = 0.38f, Categories = new[] { ShelfCategory.HardwareTools }, Weight = 14, MaxStoreCount = 10 },
        new() { Name = "Crowbar", ScenePath = "res://Prefabs/Products/Crowbar.tscn", Height = 0.60f, Width = 0.10f, Depth = 0.05f, Spacing = 0.30f, Categories = new[] { ShelfCategory.HardwareTools }, Weight = 14, MaxStoreCount = 10 },
        new() { Name = "PipeWrench", ScenePath = "res://Prefabs/Products/PipeWrench.tscn", Height = 0.48f, Width = 0.12f, Depth = 0.06f, Spacing = 0.30f, Categories = new[] { ShelfCategory.HardwareTools }, Weight = 16, MaxStoreCount = 10 },
        new() { Name = "Sledgehammer", ScenePath = "res://Prefabs/Products/Sledgehammer.tscn", Height = 0.85f, Width = 0.24f, Depth = 0.10f, Spacing = 0.45f, Categories = new[] { ShelfCategory.HardwareTools }, Weight = 6, MaxStoreCount = 6 },
        new() { Name = "PropaneTank", ScenePath = "res://Prefabs/Products/PropaneTank.tscn", Height = 0.48f, Width = 0.30f, Depth = 0.30f, Spacing = 0.45f, Categories = new[] { ShelfCategory.HardwareTools }, Weight = 5, MaxStoreCount = 6 },
        new() { Name = "FireExtinguisher", ScenePath = "res://Prefabs/Products/FireExtinguisher.tscn", Height = 0.44f, Width = 0.16f, Depth = 0.16f, Spacing = 0.38f, Categories = new[] { ShelfCategory.HardwareTools, ShelfCategory.HouseholdCleaning }, Weight = 18, MaxStoreCount = 10 },
        new() { Name = "PotatoGun", ScenePath = "res://Prefabs/Products/PotatoGun.tscn", Height = 0.35f, Width = 1.05f, Depth = 0.20f, Spacing = 1.10f, Categories = new[] { ShelfCategory.HardwareTools, ShelfCategory.SportingToys }, Weight = 8, MaxStoreCount = 4 },

        // Electronics
        new() { Name = "FlatScreenTV", ScenePath = "res://Prefabs/Products/FlatScreenTV.tscn", Height = 0.46f, Width = 0.68f, Depth = 0.18f, Spacing = 0.75f, Categories = new[] { ShelfCategory.Electronics }, Weight = 7, MaxStoreCount = 6 },
        new() { Name = "Boombox", ScenePath = "res://Prefabs/Products/Boombox.tscn", Height = 0.26f, Width = 0.45f, Depth = 0.14f, Spacing = 0.50f, Categories = new[] { ShelfCategory.Electronics }, Weight = 18, MaxStoreCount = 8 },
        new() { Name = "AlarmClock", ScenePath = "res://Prefabs/Products/AlarmClock.tscn", Height = 0.16f, Width = 0.16f, Depth = 0.08f, Spacing = 0.30f, Categories = new[] { ShelfCategory.Electronics, ShelfCategory.HouseholdCleaning }, Weight = 40, MaxStoreCount = 14 },

        // Pharmacy
        new() { Name = "PillBottle", ScenePath = "res://Prefabs/Products/PillBottle.tscn", Height = 0.09f, Width = 0.06f, Depth = 0.06f, Spacing = 0.18f, Categories = new[] { ShelfCategory.Pharmacy, ShelfCategory.MixedMarket }, Weight = 60, MaxStoreCount = 20 },

        // Sporting & Toys
        new() { Name = "BaseballBat", ScenePath = "res://Prefabs/Products/BaseballBat.tscn", Height = 0.80f, Width = 0.08f, Depth = 0.08f, Spacing = 0.32f, Categories = new[] { ShelfCategory.SportingToys }, Weight = 15, MaxStoreCount = 10 },
        new() { Name = "Football", ScenePath = "res://Prefabs/Products/Football.tscn", Height = 0.24f, Width = 0.24f, Depth = 0.24f, Spacing = 0.38f, Categories = new[] { ShelfCategory.SportingToys }, Weight = 40, MaxStoreCount = 12 },
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
                EnqueueForGeneration(this);
            }
        }
    }

    // --- Main Generation ---

    public void GenerateStock()
    {
        ClearStock();

        if (TotalGlobalSpawned >= MaxTotalStoreProducts) return;

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
                  ^ GetStableHashCode(target.Name)
                  ^ CurrentMatchSeed);
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
            if (TotalGlobalSpawned >= MaxTotalStoreProducts) break;
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

    private ProductEntry PickWeightedCandidate(List<ProductEntry> candidates, RandomNumberGenerator rng)
    {
        if (candidates.Count == 0) return null;
        if (TotalGlobalSpawned >= MaxTotalStoreProducts) return null;

        List<ProductEntry> available = new();
        int totalWeight = 0;

        foreach (var c in candidates)
        {
            if (c.MaxStoreCount >= 0 && GetGlobalSpawnCount(c.Name) >= c.MaxStoreCount)
            {
                continue; // Reached store-wide limit
            }
            available.Add(c);
            totalWeight += Mathf.Max(1, c.Weight);
        }

        if (available.Count == 0)
        {
            return null; // All candidates have reached their caps
        }

        int roll = rng.RandiRange(1, totalWeight);
        int current = 0;
        foreach (var c in available)
        {
            current += Mathf.Max(1, c.Weight);
            if (roll <= current)
            {
                return c;
            }
        }

        return available[0];
    }

    private int StockSurface(ShelfSurfaceInfo surface, Node3D container, ShelfCategory category, RandomNumberGenerator rng)
    {
        if (TotalGlobalSpawned >= MaxTotalStoreProducts) return 0;
        if (surface.MaxRun <= surface.MinRun || surface.SafeDepthMax <= surface.SafeDepthMin) return 0;

        // Add a shelf spawn chance so shelves have natural breathing room
        float shelfSpawnChance = (surface.IsTable || surface.IsEndcap) ? 0.90f : 0.65f;
        if (rng.Randf() > shelfSpawnChance)
        {
            return 0;
        }

        List<ProductEntry> candidates = GetCandidatesForSurface(surface, category);
        if (candidates.Count == 0) return 0;

        int spawnedCount = 0;
        float baseFacingAngle = Mathf.Atan2(surface.FacingNormal.X, surface.FacingNormal.Z);

        int depthRows = 1;
        if (surface.IsTable && (surface.SafeDepthMax - surface.SafeDepthMin) >= 1.2f)
        {
            depthRows = (surface.SafeDepthMax - surface.SafeDepthMin) >= 1.8f ? 3 : 2;
        }

        int maxPerShelf = surface.IsTable ? 6 : (surface.IsEndcap ? 4 : MaxProductsPerShelfRow);
        float surfaceLength = surface.MaxRun - surface.MinRun;

        for (int r = 0; r < depthRows; r++)
        {
            if (TotalGlobalSpawned >= MaxTotalStoreProducts) break;

            float targetDepth = surface.SafeDepthCenter;
            if (depthRows > 1)
            {
                float t = (float)r / (depthRows - 1);
                targetDepth = Mathf.Lerp(surface.SafeDepthMin, surface.SafeDepthMax, t);
            }

            int clusterCount = 1;
            if (surfaceLength > 10.0f)
            {
                clusterCount = rng.RandiRange(1, 2);
            }

            for (int c = 0; c < clusterCount && spawnedCount < maxPerShelf && TotalGlobalSpawned < MaxTotalStoreProducts; c++)
            {
                ProductEntry prod = PickWeightedCandidate(candidates, rng);
                if (prod == null) break;

                float spacing = prod.Spacing > 0 ? prod.Spacing : ItemSpacing;
                int clusterSize = rng.RandiRange(MinClusterSize, MaxClusterSize);

                float clusterWidth = clusterSize * spacing;
                float startRun;
                if (clusterCount > 1)
                {
                    float halfLen = surfaceLength * 0.5f;
                    float segMin = surface.MinRun + (c * halfLen) + 0.5f;
                    float segMax = segMin + halfLen - clusterWidth - 0.5f;
                    startRun = (segMax > segMin) ? rng.RandfRange(segMin, segMax) : segMin;
                }
                else
                {
                    float maxStart = surface.MaxRun - clusterWidth - 0.5f;
                    startRun = (maxStart > surface.MinRun + 0.5f) ? rng.RandfRange(surface.MinRun + 0.5f, maxStart) : surface.MinRun;
                }

                float currentRun = startRun;
                for (int i = 0; i < clusterSize && spawnedCount < maxPerShelf && TotalGlobalSpawned < MaxTotalStoreProducts; i++)
                {
                    float halfW = prod.Width * 0.5f;
                    float slotRun = currentRun + halfW;

                    if (slotRun + halfW > surface.MaxRun) break;

                    if (rng.Randf() <= FillDensity)
                    {
                        Vector3 localPos = ComputePosition(surface, slotRun, targetDepth, prod.Depth, rng);
                        float rotJitter = rng.RandfRange(-0.08f, 0.08f);

                        SpawnProduct(prod, localPos, baseFacingAngle + rotJitter, container, surface.Name, spawnedCount);
                        IncrementGlobalSpawnCount(prod.Name);
                        TotalGlobalSpawned++;
                        spawnedCount++;
                    }

                    currentRun += spacing;
                }
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
            float minX = surface.SafeDepthMin + halfD;
            float maxX = surface.SafeDepthMax - halfD;
            pos.X = minX > maxX ? surface.SafeDepthCenter : Mathf.Clamp(safeX, minX, maxX);
        }
        else
        {
            // Shelf runs along X (endcaps & tables). Depth is along Z.
            pos.X = runPos + jitterRun;

            // Clamp Z within safe depth bounds
            float safeZ = depthPos + jitterDepth;
            float halfD = itemDepth * 0.5f;
            float minZ = surface.SafeDepthMin + halfD;
            float maxZ = surface.SafeDepthMax - halfD;
            pos.Z = minZ > maxZ ? surface.SafeDepthCenter : Mathf.Clamp(safeZ, minZ, maxZ);
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
        float surfaceDepth = surface.SafeDepthMax - surface.SafeDepthMin;
        float surfaceRun = surface.MaxRun - surface.MinRun;

        foreach (var prod in Catalog)
        {
            // Clearance check: product height must not exceed shelf vertical clearance
            if (prod.Height > surface.ClearanceY + 0.04f) continue;

            // Fit check: item width must fit within the shelf run bounds
            if (prod.Width > surfaceRun) continue;

            // Fit check: item depth must fit within the shelf safe depth bounds
            if (surfaceDepth > 0f && prod.Depth > surfaceDepth) continue;

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
                if (prod.Height <= surface.ClearanceY + 0.04f &&
                    prod.Width <= surfaceRun &&
                    (surfaceDepth <= 0f || prod.Depth <= surfaceDepth))
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
                if (level == 2 || level == 3)
                {
                    surfaces.Add(CreateGondolaSideShelf(meshInst, level, Vector3.Left));
                }
            }
            else if (name.StartsWith("Shelf_R_"))
            {
                int level = ParseLevel(name);
                if (level == 2 || level == 3)
                {
                    surfaces.Add(CreateGondolaSideShelf(meshInst, level, Vector3.Right));
                }
            }
            else if (name.StartsWith("Endcap_N_Shelf"))
            {
                int level = ParseLevel(name);
                if (level == 2 || level == 3)
                {
                    surfaces.Add(CreateEndcapShelf(meshInst, level, Vector3.Back));
                }
            }
            else if (name.StartsWith("Endcap_S_Shelf"))
            {
                int level = ParseLevel(name);
                if (level == 2 || level == 3)
                {
                    surfaces.Add(CreateEndcapShelf(meshInst, level, Vector3.Forward));
                }
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
            bool runAlongX = size.X >= size.Z;
            float len = runAlongX ? size.X : size.Z;
            float dep = runAlongX ? size.Z : size.X;

            float runPadding = Math.Min(0.35f, len * 0.25f);
            float depthPadding = Math.Min(0.25f, dep * 0.25f);

            float runCenter = runAlongX ? bestTop.Position.X : bestTop.Position.Z;
            float minRun = runCenter - (len * 0.5f) + runPadding;
            float maxRun = runCenter + (len * 0.5f) - runPadding;

            float depthCenter = runAlongX ? bestTop.Position.Z : bestTop.Position.X;
            float depthMin = depthCenter - (dep * 0.5f) + depthPadding;
            float depthMax = depthCenter + (dep * 0.5f) - depthPadding;

            surfaces.Add(new ShelfSurfaceInfo
            {
                Name = bestTop.Name,
                SurfaceCenter = bestTop.Position,
                MinRun = minRun,
                MaxRun = maxRun,
                SurfaceTopY = topY,
                ClearanceY = 1.6f,
                FacingNormal = runAlongX ? Vector3.Back : Vector3.Right,
                SafeDepthCenter = depthCenter,
                SafeDepthMin = depthMin,
                SafeDepthMax = depthMax,
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

    public static uint GetStableHashCode(string str)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in str)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
}
