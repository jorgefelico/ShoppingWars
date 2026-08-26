using Godot;

public partial class Inventory : Node
{
    [Export] int InventorySize = 5;
    private Product[] InventoryItems;
    private int selectedItemIndex = 0;

    public override void _Ready()
    {
        InventoryItems = new Product[InventorySize];
    }

    public Product getItem(int index)
    {
        return InventoryItems[index];
    }

    public void AddItem(Product product)
    {
        for (int i = 0; i < InventorySize; i++)
        {
            if (InventoryItems[i] == null)
            {
                InventoryItems[i] = product;
                selectedItemIndex = i;
                break;
            }
        }
    }

    public void RemoveCurrentSelectedItem()
    {
        InventoryItems[selectedItemIndex] = null;
    }

    public bool IsInventoryFull()
    {
        bool isFull = true;

        foreach (Product item in InventoryItems)
        {
            if (item == null) isFull = false;
        }
        return isFull;
    }

    public int FreeSlots()
    {
        int freeCount = 0;
        for (int i = 0; i < InventoryItems.Length; i++)
        {
            if (InventoryItems[i] == null) freeCount += 1;
        }
        return freeCount;
    }

    public Product NextItem()
    {
        if (selectedItemIndex < InventorySize)
        {
            selectedItemIndex += 1;
            return InventoryItems[selectedItemIndex];
        }
        else
        {
            selectedItemIndex = 0;
            return InventoryItems[selectedItemIndex];
        }
    }

    public Product PreviousItem()
    {
        if (selectedItemIndex > 0)
        {
            selectedItemIndex -= 1;
            return InventoryItems[selectedItemIndex];
        }
        else
        {
            selectedItemIndex = InventorySize - 1;
            return InventoryItems[selectedItemIndex];
        }
    }

    public void SetCurrentSelectedItem(int index)
    {
        selectedItemIndex = index;
    }
}
