namespace BKPos.Core.Models;

public sealed class ProductUnit
{
    public int Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string CreatedByExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool AllowDecimalQuantity { get; set; }

    public static bool IsKnownDecimalUnitName(string? name)
    {
        var normalized = (name ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "kg" or "g" or "gram" or "gr" or "l" or "lit" or "liter" or "litre" or "ml";
    }
}
