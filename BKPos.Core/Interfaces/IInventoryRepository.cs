using BKPos.Core.Models.Inventory;

namespace BKPos.Core.Interfaces;

public interface IInventoryRepository
{
    IReadOnlyList<InventoryStockItem> GetStockItems();

    IReadOnlyList<InventoryDocumentSummary> GetDocuments(int take = 100);

    InventoryDocument? GetDocument(string externalId);

    string CreateDocument(InventoryDocument document);

    void UpdateDocument(InventoryDocument document);

    void DeleteDocument(string externalId);

    void SaveProductConfig(InventoryProductConfig config);
}
