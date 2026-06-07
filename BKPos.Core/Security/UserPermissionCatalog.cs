namespace BKPos.Core.Security;

public static class UserPermissionCatalog
{
    public static IReadOnlyList<UserPermissionDefinition> All { get; } =
    [
        new(UserPermissionKeys.SalesAccess, "Vào màn hình bán hàng", "Bán hàng", CashierDefault: true, ManagerDefault: true),
        new(UserPermissionKeys.SalesRemoveLine, "Xóa món trong bàn", "Bán hàng", ManagerDefault: true),
        new(UserPermissionKeys.SalesDiscount, "Giảm giá", "Bán hàng", ManagerDefault: true),
        new(UserPermissionKeys.SalesPayment, "Thanh toán", "Bán hàng", CashierDefault: true, ManagerDefault: true),
        new(UserPermissionKeys.InvoiceReprint, "In lại hóa đơn", "Hóa đơn", CashierDefault: true, ManagerDefault: true),
        new(UserPermissionKeys.InvoiceEditPaid, "Sửa hóa đơn đã thanh toán", "Hóa đơn", ManagerDefault: true),
        new(UserPermissionKeys.InvoiceDeletePaid, "Hủy/xóa hóa đơn đã thanh toán", "Hóa đơn", ManagerDefault: true),
        new(UserPermissionKeys.ManagementCatalog, "Quản lý danh mục mặt hàng", "Quản lý", ManagerDefault: true),
        new(UserPermissionKeys.ManagementTableZone, "Quản lý bàn và khu vực", "Quản lý", ManagerDefault: true),
        new(UserPermissionKeys.UserManage, "Quản lý nhân viên và phân quyền", "Quản lý", ManagerDefault: true),
        new(UserPermissionKeys.InventoryAccess, "Vào màn hình kho", "Kho", ManagerDefault: true, InventoryDefault: true),
        new(UserPermissionKeys.InventoryDocCreate, "Tạo phiếu nhập/xuất kho", "Kho", ManagerDefault: true, InventoryDefault: true),
        new(UserPermissionKeys.InventoryDocEdit, "Sửa phiếu kho", "Kho", ManagerDefault: true, InventoryDefault: true),
        new(UserPermissionKeys.InventoryDocDelete, "Xóa phiếu kho", "Kho", ManagerDefault: true),
        new(UserPermissionKeys.ReportsAccess, "Xem thống kê/báo cáo", "Báo cáo", ManagerDefault: true),
        new(UserPermissionKeys.SettingsAccess, "Cài đặt hệ thống", "Cài đặt", ManagerDefault: true)
    ];

    public static IReadOnlySet<string> AllKeys { get; } =
        All.Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> CashierDefaults { get; } =
        All.Where(item => item.CashierDefault).Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> ManagerDefaults { get; } =
        All.Where(item => item.ManagerDefault).Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> InventoryDefaults { get; } =
        All.Where(item => item.InventoryDefault).Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
