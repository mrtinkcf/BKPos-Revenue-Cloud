using System.Globalization;

namespace BKPos.Revenue.App;

internal static class RevenueTime
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateTimeOffset ToStoreTime(DateTimeOffset value, string? timezone)
    {
        var zone = ResolveZone(timezone);
        return zone is null
            ? value.ToOffset(VietnamOffset)
            : TimeZoneInfo.ConvertTime(value, zone);
    }

    public static string FormatStore(DateTimeOffset? value, string? timezone, string format)
        => value is null ? "-" : ToStoreTime(value.Value, timezone).ToString(format, Vi);

    private static TimeZoneInfo? ResolveZone(string? timezone)
    {
        foreach (var id in CandidateIds(timezone))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateIds(string? timezone)
    {
        if (!string.IsNullOrWhiteSpace(timezone))
        {
            yield return timezone.Trim();
        }

        yield return "Asia/Ho_Chi_Minh";
        yield return "SE Asia Standard Time";
    }
}
