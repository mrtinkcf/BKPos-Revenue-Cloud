namespace BKPos.Core.Models;

public sealed class Product
{
    public int Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string CategoryExternalId { get; set; } = string.Empty;

    public string CreatedByExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int UnitId { get; set; }

    public string UnitExternalId { get; set; } = string.Empty;

    public string UnitName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string PriceDisplay => BKPos.Core.Formatting.MoneyFormatter.FormatCurrency(Price);

    public decimal Price2 { get; set; }

    public decimal Price3 { get; set; }

    public decimal Price4 { get; set; }

    public decimal ImportPrice { get; set; }

    public int ProductType { get; set; } = ProductTypes.Drink;

    public string ProductTypeName => ProductTypes.GetName(ProductType);

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public bool CategoryInventoryEnabled { get; set; }

    public bool CategoryOrderVisible { get; set; } = true;

    public int CategoryDefaultStockMode { get; set; }

    public string CategoryTypeName => ProductCategoryTypes.GetName(CategoryInventoryEnabled, CategoryOrderVisible);
}
