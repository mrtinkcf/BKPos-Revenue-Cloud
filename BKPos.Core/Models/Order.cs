namespace BKPos.Core.Models;

public sealed class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D").ToUpper();

    public int TableId { get; set; }

    public string TableExternalId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? Note { get; set; }

    public int UserId { get; set; }

    public string UserExternalId { get; set; } = string.Empty;
}
