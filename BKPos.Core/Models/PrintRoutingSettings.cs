namespace BKPos.Core.Models;

public sealed class PrintRoutingSettings
{
    public PrintRouteTarget Bill { get; set; } = new();

    public PrintRouteTarget CupLabel { get; set; } = new();

    public PrintRouteTarget KitchenTicket { get; set; } = new();

    public PrintRouteTarget GetRoute(PrintTemplateType type) => type switch
    {
        PrintTemplateType.CupLabel => CupLabel,
        PrintTemplateType.KitchenTicket => KitchenTicket,
        _ => Bill
    };
}

public sealed class PrintRouteTarget
{
    public bool IsEnabled { get; set; } = true;

    public string? PrinterId { get; set; }

    public string? TemplateId { get; set; }

    public bool PrintFood { get; set; } = true;

    public bool PrintDrink { get; set; } = true;

    public bool PrintOther { get; set; } = true;

    public bool AllowsProductType(int productType) => ProductTypes.Normalize(productType) switch
    {
        ProductTypes.Food => PrintFood,
        ProductTypes.Drink => PrintDrink,
        ProductTypes.Other => PrintOther,
        _ => true
    };
}

public sealed class ResolvedPrintTarget
{
    public required PrinterProfile Printer { get; init; }

    public required PrintTemplate Template { get; init; }
}
