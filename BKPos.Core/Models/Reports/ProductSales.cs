namespace BKPos.Core.Models.Reports;

public sealed class ProductSales
{
    public string ProductName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public decimal QtySold { get; set; }

    public decimal Revenue { get; set; }

    public string RevenueDisplay => BKPos.Core.Formatting.MoneyFormatter.FormatCurrency(Revenue);
}

