namespace BKPos.Core.Models.Inventory;

public static class InventoryDirections
{
    public const int Export = 20;
    public const int Import = 30;

    public static string GetName(int value) => value switch
    {
        Import => "Nhập kho",
        Export => "Xuất kho",
        _ => "Không rõ"
    };
}
