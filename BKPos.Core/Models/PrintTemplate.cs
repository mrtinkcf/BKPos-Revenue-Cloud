namespace BKPos.Core.Models;

public sealed class PrintTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D").ToUpperInvariant();

    public string Name { get; set; } = string.Empty;

    public PrintTemplateType Type { get; set; }

    public int PaperWidthMm { get; set; } = 80;

    public decimal ContentInsetLeftMm { get; set; }

    public decimal ContentInsetRightMm { get; set; }

    public List<TemplateSection> Sections { get; set; } = [];

    public bool IsDefault { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public enum PrintTemplateType
{
    Bill,
    CupLabel,
    KitchenTicket
}

public sealed class TemplateSection
{
    public string Type { get; set; } = string.Empty;

    public string? Text { get; set; }

    public string Align { get; set; } = "left";

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public string FontFamily { get; set; } = string.Empty;

    public int FontSize { get; set; } = 1;

    public string? DividerChar { get; set; }

    public bool ShowPrice { get; set; } = true;

    public bool ShowQty { get; set; } = true;

    public bool ShowUnit { get; set; }

    public bool ShowNote { get; set; } = true;

    public bool ShowTableHeader { get; set; } = true;

    public bool ShowTableGrid { get; set; }

    public string? Data { get; set; }

    public int Size { get; set; } = 6;

    public string? ImagePath { get; set; }

    public int SpacerLines { get; set; } = 1;
}
