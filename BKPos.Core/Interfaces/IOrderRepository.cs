using BKPos.Core.Enums;
using BKPos.Core.Models;

namespace BKPos.Core.Interfaces;

public interface IOrderRepository
{
    Order? GetOpenOrder(string tableExternalId);

    string CreateOrder(Order order);

    void AddLine(OrderLine line);

    void UpdateLineQuantity(string lineId, decimal newQty);

    void UpdateLineNote(string lineId, string? note);

    void RemoveLine(string lineId);

    IReadOnlyList<OrderLine> GetLines(string orderId);

    void UpdateOrderTotal(string orderId, decimal total);

    void DeleteOrder(string orderId);

    void CompleteOrder(string orderId, decimal finalTotal, PaymentMethod method, decimal discountAmount);

    void RecordIncome(string description, decimal amount, int userId);
}
