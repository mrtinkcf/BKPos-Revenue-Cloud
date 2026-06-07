namespace BKPos.Core.Models.Reports;

public sealed class OverviewStats
{
    public decimal TodayRevenue { get; set; }

    public int TodayOrders { get; set; }

    public decimal MonthRevenue { get; set; }

    public int MonthOrders { get; set; }

    public string? TopProductToday { get; set; }

    public decimal AvgOrderValue { get; set; }
}
