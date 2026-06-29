namespace BKPos.Core.Models.Inventory;

public sealed class InventoryDailyMovement
{
    public DateTime Date { get; set; }

    public decimal ImportAmount { get; set; }

    public decimal ManualExportAmount { get; set; }

    public decimal SalesExportAmount { get; set; }

    public decimal ExportAmount { get; set; }
}
