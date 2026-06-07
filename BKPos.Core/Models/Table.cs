using BKPos.Core.Enums;

namespace BKPos.Core.Models;

public sealed class Table
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ZoneId { get; set; }

    public TableStatus Status { get; set; } = TableStatus.Empty;
}
