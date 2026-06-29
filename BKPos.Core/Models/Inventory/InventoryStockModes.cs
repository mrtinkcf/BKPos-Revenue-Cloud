namespace BKPos.Core.Models.Inventory;

public static class InventoryStockModes
{
    public const int Unmanaged = 0;
    public const int WholeItem = 1;
    public const int Ingredient = 2;

    public static bool IsValid(int value) => value is Unmanaged or WholeItem or Ingredient;

    public static int Normalize(int value) => IsValid(value) ? value : Unmanaged;

    public static string GetName(int value) => Normalize(value) switch
    {
        WholeItem => "Hàng bán nguyên chiếc",
        Ingredient => "Nguyên liệu chế biến",
        _ => "Không quản lý kho"
    };
}
