using System.Collections.Generic;
using Godot;

public partial class Inventory : Node
{
    [Export] public int InventorySize = 5;
    [Export] public int MaxStackSize = 25;
    [Export] public InventoryBar InventoryBar;

    private List<Product>[] _slots;
    public int selectedItemIndex = 0;

    public override void _Ready()
    {
        _slots = new List<Product>[InventorySize];
        for (int i = 0; i < InventorySize; i++)
        {
            _slots[i] = new List<Product>();
        }
    }

    public Product GetItem(int index)
    {
        if (index < 0 || index >= InventorySize || _slots == null) return null;
        if (_slots[index].Count == 0) return null;
        return _slots[index][_slots[index].Count - 1];
    }

    public int GetSlotCount(int index)
    {
        if (index < 0 || index >= InventorySize || _slots == null) return 0;
        return _slots[index].Count;
    }

    public List<Product> GetSlotItems(int index)
    {
        if (index < 0 || index >= InventorySize || _slots == null) return new List<Product>();
        return _slots[index];
    }

    public bool CanAddItem(Product product)
    {
        if (product == null || _slots == null) return false;

        for (int i = 0; i < InventorySize; i++)
        {
            if (_slots[i].Count == 0) return true;
            if (_slots[i].Count < MaxStackSize && IsSameKind(_slots[i][0], product)) return true;
        }
        return false;
    }

    public int AddItem(Product product)
    {
        if (product == null || _slots == null) return -1;

        // 1. Try to add to currently selected slot if matching and has room
        if (_slots[selectedItemIndex].Count > 0 &&
            _slots[selectedItemIndex].Count < MaxStackSize &&
            IsSameKind(_slots[selectedItemIndex][0], product))
        {
            _slots[selectedItemIndex].Add(product);
            InventoryBar?.Refresh(this, selectedItemIndex);
            return selectedItemIndex;
        }

        // 2. Try to add to any matching slot that has room
        for (int i = 0; i < InventorySize; i++)
        {
            if (_slots[i].Count > 0 &&
                _slots[i].Count < MaxStackSize &&
                IsSameKind(_slots[i][0], product))
            {
                _slots[i].Add(product);
                InventoryBar?.Refresh(this, selectedItemIndex);
                return i;
            }
        }

        // 3. Try to add to currently selected slot if empty
        if (_slots[selectedItemIndex].Count == 0)
        {
            _slots[selectedItemIndex].Add(product);
            InventoryBar?.Refresh(this, selectedItemIndex);
            return selectedItemIndex;
        }

        // 4. Try to add to the first empty slot
        for (int i = 0; i < InventorySize; i++)
        {
            if (_slots[i].Count == 0)
            {
                _slots[i].Add(product);
                InventoryBar?.Refresh(this, selectedItemIndex);
                return i;
            }
        }

        return -1;
    }

    public void RemoveCurrentSelectedItem()
    {
        RemoveItemFromSlot(selectedItemIndex);
    }

    public Product RemoveItemFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= InventorySize || _slots == null || _slots[slotIndex].Count == 0) return null;
        int lastIdx = _slots[slotIndex].Count - 1;
        Product item = _slots[slotIndex][lastIdx];
        _slots[slotIndex].RemoveAt(lastIdx);
        InventoryBar?.Refresh(this, selectedItemIndex);
        return item;
    }

    public bool RemoveItem(Product product)
    {
        if (product == null || _slots == null) return false;

        for (int i = 0; i < InventorySize; i++)
        {
            if (_slots[i].Remove(product))
            {
                InventoryBar?.Refresh(this, selectedItemIndex);
                return true;
            }
        }
        return false;
    }

    public bool IsInventoryFull()
    {
        if (_slots == null) return false;
        for (int i = 0; i < InventorySize; i++)
        {
            if (_slots[i].Count < MaxStackSize) return false;
        }
        return true;
    }

    public int FreeSlots()
    {
        if (_slots == null) return InventorySize;
        int freeCount = 0;
        for (int i = 0; i < InventorySize; i++)
        {
            if (_slots[i].Count == 0) freeCount += 1;
        }
        return freeCount;
    }

    public int SelectNextItem()
    {
        int next = (selectedItemIndex + 1) % InventorySize;
        SetCurrentSelectedItem(next);
        return selectedItemIndex;
    }

    public int SelectPreviousItem()
    {
        int prev = (selectedItemIndex - 1 + InventorySize) % InventorySize;
        SetCurrentSelectedItem(prev);
        return selectedItemIndex;
    }

    public void SetCurrentSelectedItem(int index)
    {
        selectedItemIndex = index;
        InventoryBar?.Refresh(this, index);
    }

    public void DropLoot()
    {
        if (_slots == null) return;

        for (int i = 0; i < InventorySize; i++)
        {
            foreach (Product item in _slots[i])
            {
                if (item == null || !GodotObject.IsInstanceValid(item)) continue;
                if (item.GetParent() != GetTree().CurrentScene)
                {
                    item.Reparent(GetTree().CurrentScene, true);
                }
                item.Freeze = false;
                item.CollisionLayer = 1;
                item.CollisionMask = 3;
                item.Visible = true;
                item.IsForSale = false;
                item.WasBought = true;
                item.CanBePickedUp = true;
            }
            _slots[i].Clear();
        }
        InventoryBar?.Refresh(this, 0);
    }

    public static bool IsSameKind(Product a, Product b)
    {
        if (a == null || b == null) return false;
        if (!string.IsNullOrEmpty(a.SceneFilePath) && !string.IsNullOrEmpty(b.SceneFilePath))
        {
            return a.SceneFilePath == b.SceneFilePath;
        }
        if (a.DisplayName != null && b.DisplayName != null && !string.IsNullOrEmpty(a.DisplayName.ToString()))
        {
            return a.DisplayName == b.DisplayName;
        }
        return a.Name == b.Name;
    }
}
