namespace BKPos.Core.Models.Reports;

public sealed class InvoiceLine
{
    public string Id { get; set; } = string.Empty;

    public string OrderId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public string? Note { get; set; }

    public int ProductType { get; set; } = BKPos.Core.Models.ProductTypes.Drink;

    public string UnitPriceDisplay => BKPos.Core.Formatting.MoneyFormatter.FormatCurrency(UnitPrice);

    public string LineTotalDisplay => BKPos.Core.Formatting.MoneyFormatter.FormatCurrency(LineTotal);
}

