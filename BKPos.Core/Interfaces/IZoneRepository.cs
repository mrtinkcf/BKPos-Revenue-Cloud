using BKPos.Core.Models;

namespace BKPos.Core.Interfaces;

public interface IZoneRepository
{
    IReadOnlyList<Zone> GetAll();

    void AddZone(Zone zone);

    void UpdateZone(Zone zone);

    void DeleteZone(int zoneId);

    bool HasTables(int zoneId);

    void SaveZoneOrder(IReadOnlyList<string> zoneExternalIdsInOrder);
}
