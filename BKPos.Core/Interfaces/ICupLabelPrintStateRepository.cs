using BKPos.Core.Models;

namespace BKPos.Core.Interfaces;

public interface ICupLabelPrintStateRepository
{
    IReadOnlyDictionary<string, int> GetPrintedQuantities(string orderId);

    void RecordPrinted(IReadOnlyList<CupLabelPrintRecord> records);
}
