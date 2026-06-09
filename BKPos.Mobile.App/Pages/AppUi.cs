using System.Globalization;
using System.Text;
using BKPos.Core.Formatting;
using BKPos.Mobile.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace BKPos.Mobile.App.Pages;

internal static class AppUi
{
    public static readonly Color Navy = Color.FromArgb("#071A2F");
    public static readonly Color Navy2 = Color.FromArgb("#0B2442");
    public static readonly Color Blue = Color.FromArgb("#1D4ED8");
    public static readonly Color BlueSoft = Color.FromArgb("#DBEAFE");
    public static readonly Color Red = Color.FromArgb("#DC2626");
    public static readonly Color RedSoft = Color.FromArgb("#FEE2E2");
    public static readonly Color Background = Color.FromArgb("#EEF4FB");
    public static readonly Color Surface = Color.FromArgb("#FFFFFF");
    public static readonly Color SurfaceAlt = Color.FromArgb("#F8FAFC");
    public static readonly Color Ink = Color.FromArgb("#0F172A");
    public static readonly Color Muted = Color.FromArgb("#64748B");
    public static readonly Color Border = Color.FromArgb("#D8E2EF");
    public static readonly Color Success = Color.FromArgb("#15803D");
    public static readonly Color Warning = Color.FromArgb("#F59E0B");
    public static readonly Color Orange = Color.FromArgb("#F97316");
    public static readonly Color Danger = Red;
    public static readonly Color Coffee = Navy;
    public static readonly Color Accent = Blue;
    public static readonly Color Card = Surface;

    // ── Responsive scale ────────────────────────────────────────────────────
    private static double? _scale;
    public static double Scale => _scale ??= ComputeScale();

    private static double ComputeScale()
    {
        try
        {
            var info = DeviceDisplay.Current.MainDisplayInfo;
            // App is landscape-locked; shorter side = landscape height
            var shortSide = Math.Min(info.Width, info.Height) / info.Density;
            return Math.Clamp(shortSide / 360.0, 0.8, 1.25);
        }
        catch { return 1.0; }
    }

    /// <summary>Scale a size value by the device's responsive factor.</summary>
    public static double S(double size) => size * Scale;

    public static Label Title(string text) => new()
    {
        Text = text,
        FontSize = S(20),
        FontAttributes = FontAttributes.Bold,
        TextColor = Ink
    };

    public static Label Subtitle(string text) => new()
    {
        Text = text,
        FontSize = S(12),
        TextColor = Muted,
        LineBreakMode = LineBreakMode.WordWrap
    };

    public static Label SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = S(14),
        FontAttributes = FontAttributes.Bold,
        TextColor = Ink
    };

    public static Border CardView(View content, double padding = 14) => new()
    {
        BackgroundColor = Surface,
        Stroke = Border,
        StrokeThickness = 1,
        Padding = S(padding),
        StrokeShape = new RoundRectangle { CornerRadius = 18 },
        Content = content
    };

    public static Entry Entry(string placeholder, Keyboard? keyboard = null, bool password = false) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Text,
        IsPassword = password,
        BackgroundColor = SurfaceAlt,
        TextColor = Ink,
        PlaceholderColor = Muted,
        HeightRequest = S(50),
        FontSize = S(15),
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };

    public static Button PrimaryButton(string text) => Button(text, Blue, Colors.White);

    public static Button SecondaryButton(string text) => Button(text, BlueSoft, Blue);

    public static Button DangerButton(string text) => Button(text, Red, Colors.White);

    public static Button NavyButton(string text) => Button(text, Navy, Colors.White);

    public static Button GhostButton(string text) => Button(text, Colors.Transparent, Blue);

    public static Button IconButton(string icon, Color? color = null) => new()
    {
        Text = icon,
        BackgroundColor = Colors.Transparent,
        TextColor = color ?? Colors.White,
        FontSize = S(24),
        FontAttributes = FontAttributes.Bold,
        WidthRequest = S(48),
        HeightRequest = S(48),
        Padding = 0,
        CornerRadius = 24
    };

    private static Button Button(string text, Color background, Color textColor) => new()
    {
        Text = text,
        FontAttributes = FontAttributes.Bold,
        BackgroundColor = background,
        TextColor = textColor,
        CornerRadius = 16,
        HeightRequest = S(54),
        FontSize = S(15),
        Padding = new Thickness(S(14), 0)
    };

    public static Border Pill(string text, Color background, Color color) => new()
    {
        BackgroundColor = background,
        StrokeThickness = 0,
        Padding = new Thickness(S(10), S(6)),
        StrokeShape = new RoundRectangle { CornerRadius = 999 },
        Content = new Label
        {
            Text = text,
            TextColor = color,
            FontAttributes = FontAttributes.Bold,
            FontSize = S(12),
            HorizontalTextAlignment = TextAlignment.Center
        }
    };

    public static string Money(decimal value) => MoneyFormatter.FormatCurrency(value);

    public static bool ContainsSearch(string value, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return NormalizeSearch(value).Contains(NormalizeSearch(keyword), StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeSearch(string value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static async Task ShowErrorAsync(Page page, string message, string title = "Thông báo")
        => await page.DisplayAlert(title, message, "OK");

    public static string ToVietnameseError(Exception ex)
    {
        if (ex is ApiException api)
        {
            var detail = string.IsNullOrWhiteSpace(api.Detail) ? null : api.Detail;
            return api.Error switch
            {
                "missing_server_url" => "Chưa nhập IP máy chủ. Vào cài đặt để nhập IP trước.",
                "unauthorized" => "Sai tên đăng nhập hoặc mật khẩu.",
                "rate_limited" => "Đăng nhập sai quá nhiều lần. Vui lòng chờ một lúc rồi thử lại.",
                "license_invalid" => detail ?? "License không hợp lệ, đã hết hạn hoặc đã bị thu hồi.",
                "server_busy" => "Máy chủ đang bận. Vui lòng thử lại.",
                "order_locked" => "Đơn đang được xử lý bởi thiết bị khác. Vui lòng thử lại.",
                "table_occupied" => "Bàn đã có đơn đang mở.",
                "payment_total_mismatch" => "Số tiền thanh toán chưa khớp với tổng đơn.",
                "already_paid" => "Đơn này đã thanh toán.",
                "conflict" => detail ?? "Dữ liệu đang xung đột với trạng thái hiện tại. Vui lòng tải lại và thử lại.",
                "payments_required" => "Cần nhập ít nhất một phương thức thanh toán.",
                "idempotency_key_required" => "Thiếu mã chống thanh toán trùng.",
                "not_found" => "Không tìm thấy dữ liệu.",
                "bad_request" => "Dữ liệu gửi lên không hợp lệ.",
                _ when detail is not null => detail,
                _ => $"Lỗi API: {api.Error}"
            };
        }

        if (ex is TaskCanceledException or TimeoutException)
        {
            return "Kết nối máy chủ quá thời gian chờ. Kiểm tra BKPos Mobile Server đang chạy và điện thoại cùng mạng LAN.";
        }

        if (ex is HttpRequestException)
        {
            return "Không kết nối được máy chủ. Kiểm tra điện thoại cùng Wi-Fi/LAN, BKPos Mobile Server đang chạy, IP đúng và firewall mở cổng 5050.";
        }

        return $"Lỗi: {ex.Message}";
    }
}

internal sealed class TableCard
{
    public TableCard(TableDto source, bool isCurrent = false)
    {
        Source = source;
        Name = source.TableName;
        Status = source.HasOpenOrder ? "Đang phục vụ" : "Bàn trống";
        Total = source.HasOpenOrder ? AppUi.Money(source.Total) : "Sẵn sàng";
        Background = source.HasOpenOrder
            ? Color.FromArgb("#FFF7ED")
            : Color.FromArgb("#ECFDF5");
        BorderColor = isCurrent
            ? AppUi.Blue
            : source.HasOpenOrder
                ? Color.FromArgb("#FDBA74")
                : Color.FromArgb("#86EFAC");
        StatusColor = source.HasOpenOrder ? AppUi.Warning : AppUi.Success;
        BorderThickness = isCurrent ? 2 : 1;
    }

    public TableDto Source { get; }
    public string Name { get; }
    public string Status { get; }
    public string Total { get; }
    public Color Background { get; }
    public Color BorderColor { get; }
    public Color StatusColor { get; }
    public int BorderThickness { get; }
}

internal sealed class OrderLineCard
{
    public OrderLineCard(OrderLineDto source)
    {
        Source = source;
        ProductName = source.ProductName;
        Summary = $"{source.Quantity} x {AppUi.Money(source.UnitPrice)}";
        QuantityText = source.Quantity.ToString();
        PricePart = $"x {AppUi.Money(source.UnitPrice)}";
        LineTotal = AppUi.Money(source.LineTotal);
        Note = source.Note?.Trim() ?? string.Empty;
        NoteDisplay = string.IsNullOrWhiteSpace(Note) ? string.Empty : "Ghi chú: " + Note;
        HasKitchenPrint = source.HasKitchenPrint;
        KitchenPrintStatus = source.IsKitchenPrinted
            ? "Đã in bếp"
            : $"Đã in {Math.Max(0, source.KitchenPrintedQuantity)}/{Math.Max(0, source.Quantity)}";
        KitchenPrintStatusColor = source.IsKitchenPrinted ? AppUi.Success : AppUi.Warning;
    }

    public OrderLineDto Source { get; }
    public string ProductName { get; }
    public string Summary { get; }
    public string QuantityText { get; }
    public string PricePart { get; }
    public string LineTotal { get; }
    public string Note { get; }
    public string NoteDisplay { get; }
    public bool HasKitchenPrint { get; }
    public string KitchenPrintStatus { get; }
    public Color KitchenPrintStatusColor { get; }
}
