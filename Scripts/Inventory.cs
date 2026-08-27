using Godot;

public partial class Inventory : Node
{
    [Export] int InventorySize = 5;
    [Export] InventoryBar InventoryBar;

    private Product[] InventoryItems;
    public int selectedItemIndex = 0;

    public override void _Ready()
    {
        InventoryItems = new Product[InventorySize];
    }

    public Product GetItem(int index)
    {
        return InventoryItems[index];
    }

    public void AddItem(Product product)
    {
        for (int i = 0; i < InventoryItems.Length; i++)
        {
            if (InventoryItems[i] == null)
            {
                InventoryItems[i] = product;
                selectedItemIndex = i;
                break;
            }
        }
        InventoryBar.Refresh(InventoryItems, selectedItemIndex);
    }

    public void RemoveCurrentSelectedItem()
    {
        InventoryItems[selectedItemIndex] = null;
        InventoryBar.Refresh(InventoryItems, selectedItemIndex);
    }

    public bool IsInventoryFull()
    {
        return FreeSlots() == 0;
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

    public int SelectNextItem()
    {
        if (selectedItemIndex < InventoryItems.Length - 1)
        {
            SetCurrentSelectedItem(selectedItemIndex + 1);
            return selectedItemIndex;
        }
        else
        {
            SetCurrentSelectedItem(0);
            return selectedItemIndex;
        }
    }

    public int SelectPreviousItem()
    {
        if (selectedItemIndex > 0)
        {
            SetCurrentSelectedItem(selectedItemIndex - 1);
            return selectedItemIndex;
        }
        else
        {
            SetCurrentSelectedItem(InventoryItems.Length - 1);
            return selectedItemIndex;
        }
    }

    public void SetCurrentSelectedItem(int index)
    {
        selectedItemIndex = index;
        InventoryBar.Refresh(InventoryItems, index);
    }

}
