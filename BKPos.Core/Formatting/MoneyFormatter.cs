using System.Globalization;

namespace BKPos.Core.Formatting;

public static class MoneyFormatter
{
    public const string CurrencySuffix = "đ";

    public static readonly CultureInfo DisplayCulture = CreateDisplayCulture();

    public static string FormatNumber(decimal value) =>
        value.ToString("N0", DisplayCulture);

    public static string FormatCurrency(decimal value) =>
        FormatNumber(value) + CurrencySuffix;

    public static decimal Parse(string? value)
    {
        var text = (value ?? string.Empty)
            .Replace(CurrencySuffix, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("₫", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("VND", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        if (decimal.TryParse(text, NumberStyles.Number, DisplayCulture, out var displayValue))
        {
            return displayValue;
        }

        var normalized = text.Replace(".", string.Empty).Replace(",", string.Empty).Replace(" ", string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var plainValue)
            ? plainValue
            : 0m;
    }

    public static void ApplyDefaultCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = DisplayCulture;
        CultureInfo.DefaultThreadCurrentUICulture = DisplayCulture;
        CultureInfo.CurrentCulture = DisplayCulture;
        CultureInfo.CurrentUICulture = DisplayCulture;
    }

    private static CultureInfo CreateDisplayCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("vi-VN").Clone();
        culture.NumberFormat.NumberGroupSeparator = ".";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.CurrencyGroupSeparator = ".";
        culture.NumberFormat.CurrencyDecimalSeparator = ",";
        return CultureInfo.ReadOnly(culture);
    }
}
