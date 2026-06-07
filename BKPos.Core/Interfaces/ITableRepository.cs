using BKPos.Core.Models;

namespace BKPos.Core.Interfaces;

public interface ITableRepository
{
    IReadOnlyList<Table> GetByZone(int zoneId);

    IReadOnlyList<TableSummary> GetSummariesByZone(string? zoneExternalId);

    void AddTable(Table table);

    void UpdateTable(Table table);

    void DeleteTable(int tableId);

    bool HasOpenOrder(int tableId);
}
