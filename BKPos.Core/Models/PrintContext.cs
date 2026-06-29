namespace BKPos.Core.Models;

public sealed class PrintContext
{
    public string ShopName { get; set; } = string.Empty;

    public string ShopAddress { get; set; } = string.Empty;

    public string ShopPhone { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string ZoneName { get; set; } = string.Empty;

    public string OrderId { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    public string Cashier { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public string DiscountNote { get; set; } = string.Empty;

    public List<PrintLineItem> Items { get; set; } = [];

    public string TaxCode      { get; set; } = string.Empty;
    public string WifiPassword { get; set; } = string.Empty;
    public string LogoPath     { get; set; } = string.Empty;

    // Thông tin khách hàng (tuỳ chọn — dùng cho bill giao hàng hoặc phiếu tính tiền)
    public string CustomerName    { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public string CustomerPhone   { get; set; } = string.Empty;

    // Dùng cho tem ly: ví dụ tem thứ 1 trong tổng 5 tem sẽ hiển thị 1/5.
    public int ItemIndex { get; set; }
    public int ItemCount { get; set; }
    public string ItemSequence => ItemIndex > 0 && ItemCount > 0 ? $"{ItemIndex}/{ItemCount}" : string.Empty;
}

public sealed class PrintLineItem
{
    public string OrderLineId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string UnitName { get; set; } = string.Empty;

    public decimal Qty { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public int ProductType { get; set; } = ProductTypes.Drink;

    public string Note { get; set; } = string.Empty;
}
