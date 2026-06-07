namespace BKPos.Core.Models.Inventory;

public sealed class InventoryDocument
{
    public string ExternalId { get; set; } = string.Empty;

    public int Direction { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateTime DocumentDate { get; set; } = DateTime.Now;

    public string Note { get; set; } = string.Empty;

    public string CreatedByExternalId { get; set; } = string.Empty;

    public decimal TotalAmount => Lines.Sum(line => line.LineTotal);

    public List<InventoryDocumentLine> Lines { get; } = [];
}
