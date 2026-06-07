namespace BKPos.Core.Models.Reports;

public sealed class DailyRevenue
{
    public DateTime Date { get; set; }

    public int OrderCount { get; set; }

    public decimal Revenue { get; set; }

    public string DateDisplay => Date.ToString("dd/MM/yyyy");

    public string RevenueDisplay => BKPos.Core.Formatting.MoneyFormatter.FormatCurrency(Revenue);
}

