using BKPos.Core.Models;

namespace BKPos.Core.Discounts;

public sealed record DiscountableLine(decimal LineTotal, int ProductType);

public sealed record DiscountBreakdown(
    decimal Subtotal,
    decimal EligibleSubtotal,
    decimal DiscountAmount,
    decimal Total,
    decimal DiscountPercent,
    bool IsAutomaticCategoryDiscount,
    bool IsDiscountAllowed,
    string Status,
    string DiscountNote = "");

public static class DiscountCalculator
{
    public static DiscountBreakdown Calculate(
        IEnumerable<DiscountableLine> lines,
        ShopSettings? settings,
        decimal manualDiscountAmount = 0,
        DateTime? now = null)
    {
        settings ??= new ShopSettings();
        var lineList = lines.ToArray();
        var subtotal = RoundCurrency(lineList.Sum(line => Math.Max(0, line.LineTotal)));
        if (subtotal <= 0)
        {
            return new DiscountBreakdown(0, 0, 0, 0, 0, false, settings.AllowDiscount, "Đơn không có tiền hàng.");
        }

        if (!settings.AllowDiscount)
        {
            return new DiscountBreakdown(subtotal, 0, 0, subtotal, 0, false, false, "Chưa bật giảm giá.");
        }

        if (settings.CategoryDiscountEnabled)
        {
            return CalculateCategoryDiscount(lineList, settings, subtotal, now ?? DateTime.Now);
        }

        var maxManualDiscount = RoundCurrency(subtotal * ClampPercent(settings.MaxDiscountPercent) / 100m);
        var manualDiscount = Math.Clamp(RoundCurrency(manualDiscountAmount), 0m, maxManualDiscount);
        return new DiscountBreakdown(
            subtotal,
            subtotal,
            manualDiscount,
            Math.Max(0, subtotal - manualDiscount),
            subtotal <= 0 ? 0 : RoundPercent(manualDiscount * 100m / subtotal),
            false,
            true,
            manualDiscount > 0 ? "Áp dụng giảm giá thủ công." : "Không có giảm giá.");
    }

    public static bool IsCategoryDiscountInDateRange(ShopSettings settings, DateTime now)
    {
        var today = now.Date;
        if (settings.CategoryDiscountStartDate is { } start && today < start.Date)
        {
            return false;
        }

        if (settings.CategoryDiscountEndDate is { } end && today > end.Date)
        {
            return false;
        }

        return true;
    }

    private static DiscountBreakdown CalculateCategoryDiscount(
        IReadOnlyList<DiscountableLine> lines,
        ShopSettings settings,
        decimal subtotal,
        DateTime now)
    {
        if (!IsCategoryDiscountInDateRange(settings, now))
        {
            return new DiscountBreakdown(subtotal, 0, 0, subtotal, 0, true, true, "Giảm giá theo loại sản phẩm chưa trong thời gian hiệu lực.");
        }

        var percent = ClampPercent(Math.Min(settings.CategoryDiscountPercent, settings.MaxDiscountPercent));
        if (percent <= 0)
        {
            return new DiscountBreakdown(subtotal, 0, 0, subtotal, 0, true, true, "Phần trăm giảm giá đang bằng 0.");
        }

        var eligibleSubtotal = RoundCurrency(lines
            .Where(line => IsDiscountedProductType(settings, line.ProductType))
            .Sum(line => Math.Max(0, line.LineTotal)));
        if (eligibleSubtotal <= 0)
        {
            return new DiscountBreakdown(subtotal, 0, 0, subtotal, percent, true, true, "Không có món thuộc loại được giảm giá.");
        }

        var discount = Math.Min(subtotal, RoundCurrency(eligibleSubtotal * percent / 100m));
        var note = discount > 0 ? NormalizeNote(settings.CategoryDiscountNote) : string.Empty;
        return new DiscountBreakdown(
            subtotal,
            eligibleSubtotal,
            discount,
            Math.Max(0, subtotal - discount),
            percent,
            true,
            true,
            "Áp dụng giảm giá theo loại sản phẩm.",
            note);
    }

    private static bool IsDiscountedProductType(ShopSettings settings, int productType)
        => ProductTypes.Normalize(productType) switch
        {
            ProductTypes.Food => settings.CategoryDiscountFood,
            ProductTypes.Drink => settings.CategoryDiscountDrink,
            ProductTypes.Other => settings.CategoryDiscountOther,
            _ => false
        };

    private static decimal ClampPercent(decimal value) => Math.Clamp(value, 0m, 100m);

    private static decimal RoundPercent(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundCurrency(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static string NormalizeNote(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= 250 ? text : text[..250];
    }
}