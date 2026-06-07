namespace BKPos.Core.Models.Inventory;

public sealed class InventoryProductConfig
{
    public string ProductExternalId { get; set; } = string.Empty;

    public int StockMode { get; set; }

    public decimal MinStock { get; set; }
}
