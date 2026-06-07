namespace BKPos.Core.Models.Reports;

public sealed class InvoiceSummary
{
    public string OrderId { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public decimal Discount { get; set; }

    public decimal Total { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public DateTime BusinessDate { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public string DiscountDisplay => BKPos.Core.Formatting.MoneyFormatter.FormatCurrency(Discount);

    public string TotalDisplay => BKPos.Core.Formatting.MoneyFormatter.FormatCurrency(Total);

    public string StartTimeDisplay => StartTime.ToString("HH:mm");

    public string EndTimeDisplay => EndTime?.ToString("HH:mm") ?? string.Empty;

    public string DateDisplay => BusinessDate.ToString("dd/MM/yyyy");
}

