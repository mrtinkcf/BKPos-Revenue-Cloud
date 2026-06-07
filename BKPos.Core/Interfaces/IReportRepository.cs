using BKPos.Core.Models;
using BKPos.Core.Models.Reports;

namespace BKPos.Core.Interfaces;

public interface IReportRepository
{
    OverviewStats GetOverview(DateTime today);

    IReadOnlyList<DailyRevenue> GetDailyRevenue(DateTime from, DateTime to);

    IReadOnlyList<ProductSales> GetTopProducts(DateTime from, DateTime to, int top = 20);

    IReadOnlyList<OrderSummary> GetOrderHistory(DateTime from, DateTime to, string? search = null);

    IReadOnlyList<OrderLine> GetOrderLines(string orderId);

    IReadOnlyList<InvoiceSummary> GetInvoices(DateTime? from, DateTime? to);

    IReadOnlyList<InvoiceLine> GetInvoiceLines(string orderId);

    void CancelInvoice(string orderId);
}
