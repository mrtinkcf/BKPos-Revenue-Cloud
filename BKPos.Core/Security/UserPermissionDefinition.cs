namespace BKPos.Core.Security;

public sealed record UserPermissionDefinition(
    string Key,
    string Name,
    string GroupName,
    bool CashierDefault = false,
    bool ManagerDefault = false,
    bool InventoryDefault = false);
