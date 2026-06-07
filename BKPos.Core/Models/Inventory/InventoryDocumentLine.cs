namespace BKPos.Core.Models.Inventory;

public sealed class InventoryDocumentLine
{
    public string ProductExternalId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string UnitName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SalePrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public string Note { get; set; } = string.Empty;

    public int StockMode { get; set; } = InventoryStockModes.Unmanaged;

    public decimal MinStock { get; set; }
}
