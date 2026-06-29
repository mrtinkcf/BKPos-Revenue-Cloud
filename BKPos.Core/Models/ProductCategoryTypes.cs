using BKPos.Core.Models.Inventory;

namespace BKPos.Core.Models;

public static class ProductCategoryTypes
{
    public const int Sales = 0;
    public const int SalesAndInventory = 1;
    public const int InventoryOnly = 2;

    public static string GetName(bool inventoryEnabled, bool orderVisible)
    {
        if (inventoryEnabled && !orderVisible)
        {
            return "Quản lý kho, không order";
        }

        if (inventoryEnabled)
        {
            return "Bán hàng + quản lý kho";
        }

        return "Bán hàng";
    }

    public static int GetType(bool inventoryEnabled, bool orderVisible)
    {
        if (inventoryEnabled && !orderVisible)
        {
            return InventoryOnly;
        }

        return inventoryEnabled ? SalesAndInventory : Sales;
    }

    public static void Apply(ProductCategory category, int type)
    {
        switch (type)
        {
            case SalesAndInventory:
                category.InventoryEnabled = true;
                category.OrderVisible = true;
                category.DefaultStockMode = InventoryStockModes.WholeItem;
                break;
            case InventoryOnly:
                category.InventoryEnabled = true;
                category.OrderVisible = false;
                category.DefaultStockMode = InventoryStockModes.Ingredient;
                break;
            default:
                category.InventoryEnabled = false;
                category.OrderVisible = true;
                category.DefaultStockMode = InventoryStockModes.Unmanaged;
                break;
        }
    }
}
