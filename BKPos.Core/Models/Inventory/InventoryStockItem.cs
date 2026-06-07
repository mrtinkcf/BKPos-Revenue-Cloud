using BKPos.Core.Formatting;

namespace BKPos.Core.Models.Inventory;

public sealed class InventoryStockItem
{
    public string ProductExternalId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string UnitName { get; set; } = string.Empty;

    public decimal CurrentStock { get; set; }

    public decimal LastImportPrice { get; set; }

    public decimal SalePrice { get; set; }

    public bool IsManaged { get; set; }

    public bool HasImportMovement { get; set; }

    public bool IsInventoryCategory { get; set; }

    public bool IsOrderVisible { get; set; } = true;

    public int CategoryDefaultStockMode { get; set; } = InventoryStockModes.Unmanaged;

    public int StockMode { get; set; } = InventoryStockModes.Unmanaged;

    public string StockModeName => InventoryStockModes.GetName(StockMode);

    public decimal MinStock { get; set; }

    public decimal EstimatedValue => CurrentStock * LastImportPrice;

    public bool IsOutOfStock => CurrentStock <= 0;

    public bool IsLowStock => MinStock > 0 && CurrentStock > 0 && CurrentStock <= MinStock;

    public string CurrentStockDisplay => CurrentStock.ToString("N2", MoneyFormatter.DisplayCulture);

    public string LastImportPriceDisplay => MoneyFormatter.FormatCurrency(LastImportPrice);

    public string SalePriceDisplay => MoneyFormatter.FormatCurrency(SalePrice);

    public string EstimatedValueDisplay => MoneyFormatter.FormatCurrency(EstimatedValue);
}
