namespace BKPos.Core.Models.Inventory;

public sealed class InventoryDocumentSummary
{
    public string ExternalId { get; set; } = string.Empty;

    public int Direction { get; set; }

    public string DirectionName => InventoryDirections.GetName(Direction);

    public string DocumentNo { get; set; } = string.Empty;

    public DateTime DocumentDate { get; set; }

    public string Note { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
}
