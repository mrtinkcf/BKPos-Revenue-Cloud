namespace BKPos.Core.Models;

public sealed class Zone
{
    public int Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
