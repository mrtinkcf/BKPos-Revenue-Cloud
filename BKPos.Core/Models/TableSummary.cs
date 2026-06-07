namespace BKPos.Core.Models;

public sealed class TableSummary
{
    public int TableId { get; set; }

    public string TableExternalId { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public int ZoneId { get; set; }

    public string ZoneExternalId { get; set; } = string.Empty;

    public string? OrderId { get; set; }

    public DateTime? OccupiedAt { get; set; }

    public decimal Total { get; set; }
}
