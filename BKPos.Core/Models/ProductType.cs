namespace BKPos.Core.Models;

public static class ProductTypes
{
    public const int Food = 0;
    public const int Drink = 1;
    public const int Other = 2;

    public static IReadOnlyList<ProductTypeOption> All { get; } =
    [
        new(Food, "Đồ ăn"),
        new(Drink, "Đồ uống"),
        new(Other, "Đồ khác")
    ];

    public static bool IsValid(int value) => value is Food or Drink or Other;

    public static int Normalize(int value) => IsValid(value) ? value : Drink;

    public static string GetName(int value)
        => All.FirstOrDefault(item => item.Value == Normalize(value))?.Name ?? "Đồ uống";
}

public sealed record ProductTypeOption(int Value, string Name);
