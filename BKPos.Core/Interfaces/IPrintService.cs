using BKPos.Core.Models;

namespace BKPos.Core.Interfaces;

public interface IPrintService
{
    Task PrintBillAsync(PrinterProfile printer, PrintTemplate template, PrintContext context);

    Task PrintKitchenTicketAsync(PrinterProfile printer, PrintTemplate template, PrintContext context);

    Task PrintCupLabelAsync(PrinterProfile printer, PrintTemplate template, PrintContext context, int quantity = 1);

    Task<bool> TestPrintAsync(PrinterProfile printer);

    System.Drawing.Bitmap RenderPreview(PrintTemplate template, PrintContext context);
}
