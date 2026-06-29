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

    public static string FormatQuantity(decimal value) =>
        value.ToString("0.###", DisplayCulture);

    public static decimal ParseQuantity(string? value)
    {
        var text = (value ?? string.Empty).Trim().Replace(" ", string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        var lastComma = text.LastIndexOf(',');
        var lastDot = text.LastIndexOf('.');
        var decimalIndex = Math.Max(lastComma, lastDot);
        if (decimalIndex >= 0)
        {
            var normalized = new System.Text.StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];
                if (char.IsDigit(character) || (i == 0 && character == '-'))
                {
                    normalized.Append(character);
                }
                else if (i == decimalIndex)
                {
                    normalized.Append('.');
                }
            }

            return decimal.TryParse(
                normalized.ToString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var normalizedValue)
                ? normalizedValue
                : 0m;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var plainValue)
            ? plainValue
            : 0m;
    }

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
