namespace BKPos.Licensing.Models;

public sealed class LicenseInfo
{
    public int Schema { get; set; } = 1;
    public string Product { get; set; } = "BKPos";
    public string LicenseId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string Edition { get; set; } = "Pro";
    public List<string> Features { get; set; } = [];
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
