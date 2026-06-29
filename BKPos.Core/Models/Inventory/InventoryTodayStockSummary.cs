namespace BKPos.Core.Models.Inventory;

public sealed class InventoryTodayStockSummary
{
    public string ProductExternalId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string UnitName { get; set; } = string.Empty;

    public decimal OpeningStock { get; set; }

    public decimal ImportQuantity { get; set; }

    public decimal ManualExportQuantity { get; set; }

    public decimal SalesExportQuantity { get; set; }

    public decimal ClosingStock { get; set; }

    public decimal LastImportPrice { get; set; }

    public decimal MinStock { get; set; }

    public decimal TotalExportQuantity => ManualExportQuantity + SalesExportQuantity;

    public decimal ClosingStockValue => ClosingStock * LastImportPrice;
}
