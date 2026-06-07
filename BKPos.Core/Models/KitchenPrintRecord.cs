namespace BKPos.Core.Models;

public sealed class KitchenPrintRecord
{
    public string OrderId { get; set; } = string.Empty;

    public string OrderLineId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string UserExternalId { get; set; } = string.Empty;

    public string? Note { get; set; }
}
