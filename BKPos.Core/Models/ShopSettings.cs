namespace BKPos.Core.Models;

public sealed class ShopSettings
{
    public string ShopName { get; set; } = "Quán Cà Phê";
    public string ShopAddress { get; set; } = string.Empty;
    public string ShopPhone { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string WifiPassword { get; set; } = string.Empty;

    // Bán hàng
    public bool AutoPrintAfterPayment { get; set; } = false;
    public bool PhoneOrderEnabled { get; set; } = false;
    public bool InventoryManagementEnabled { get; set; } = false;
    public bool AllowNegativeInventorySale { get; set; } = true;
    public decimal VatPercent { get; set; } = 0;
    public string DefaultGuestName { get; set; } = "Khách lẻ";

    // Khuyến mãi
    public bool AllowDiscount { get; set; } = true;
    public decimal DefaultDiscountPercent { get; set; } = 0;
    public decimal MaxDiscountPercent { get; set; } = 100;

    public bool CategoryDiscountEnabled { get; set; } = false;
    public decimal CategoryDiscountPercent { get; set; } = 0;
    public bool CategoryDiscountFood { get; set; } = false;
    public bool CategoryDiscountDrink { get; set; } = false;
    public bool CategoryDiscountOther { get; set; } = false;
    public DateTime? CategoryDiscountStartDate { get; set; }
    public DateTime? CategoryDiscountEndDate { get; set; }
    public string CategoryDiscountNote { get; set; } = string.Empty;
}
