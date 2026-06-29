using BKPos.Core.Formatting;

namespace BKPos.Core.Models.Inventory;

public sealed class InventoryRecipeLine
{
    public string RecipeId { get; set; } = string.Empty;

    public string DishProductExternalId { get; set; } = string.Empty;

    public string DishProductName { get; set; } = string.Empty;

    public string IngredientProductExternalId { get; set; } = string.Empty;

    public string IngredientProductName { get; set; } = string.Empty;

    public string IngredientUnitName { get; set; } = string.Empty;

    public decimal QuantityPerUnit { get; set; }

    public decimal IngredientLastImportPrice { get; set; }

    public decimal LineCost => QuantityPerUnit * IngredientLastImportPrice;

    public string QuantityPerUnitDisplay => MoneyFormatter.FormatQuantity(QuantityPerUnit);

    public string IngredientLastImportPriceDisplay => MoneyFormatter.FormatCurrency(IngredientLastImportPrice);

    public string LineCostDisplay => MoneyFormatter.FormatCurrency(LineCost);
}
