namespace BKPos.Core.Models;

public sealed class OrderLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D").ToUpper();

    public string OrderId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string UserExternalId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int ProductType { get; set; } = ProductTypes.Drink;

    public decimal Quantity { get; set; }

    public string? Note { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
