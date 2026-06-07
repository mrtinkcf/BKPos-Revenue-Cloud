using BKPos.Core.Models;

namespace BKPos.Core.Interfaces;

public interface IKitchenPrintStateRepository
{
    IReadOnlyDictionary<string, int> GetPrintedQuantities(string orderId);

    void RecordPrinted(IReadOnlyList<KitchenPrintRecord> records);
}
