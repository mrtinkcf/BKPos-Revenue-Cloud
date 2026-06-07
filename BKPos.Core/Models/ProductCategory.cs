namespace BKPos.Core.Models;

public sealed class ProductCategory
{
    public int Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string CreatedByExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool InventoryEnabled { get; set; }

    public bool OrderVisible { get; set; } = true;

    public int DefaultStockMode { get; set; }

    public string CategoryTypeName => ProductCategoryTypes.GetName(InventoryEnabled, OrderVisible);
}
