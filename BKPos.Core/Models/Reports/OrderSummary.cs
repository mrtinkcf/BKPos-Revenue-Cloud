namespace BKPos.Core.Models.Reports;

public sealed class OrderSummary
{
    public string OrderId { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public int ItemCount { get; set; }

    public decimal Total { get; set; }

    public string TotalDisplay => BKPos.Core.Formatting.MoneyFormatter.FormatCurrency(Total);

    public string TimeDisplay => CreatedAt.ToString("HH:mm dd/MM");
}

