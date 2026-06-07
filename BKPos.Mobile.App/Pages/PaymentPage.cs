using System.Globalization;
using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Pages;

public sealed class PaymentPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly OrderDto _order;
    private readonly Func<bool, Task> _completed;
    private readonly Entry _cash = AppUi.Entry("Tiền mặt", Keyboard.Numeric);
    private readonly Entry _transfer = AppUi.Entry("Chuyển khoản", Keyboard.Numeric);
    private readonly Entry _card = AppUi.Entry("Thẻ", Keyboard.Numeric);
    private readonly Entry _discount = AppUi.Entry("Giảm giá", Keyboard.Numeric);
    private readonly Label _summary = new() { FontSize = AppUi.S(18), FontAttributes = FontAttributes.Bold, TextColor = AppUi.Coffee };
    private readonly Label _status = AppUi.Subtitle("Nhập số tiền theo từng phương thức.");
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Accent };
    private string _idempotencyKey = Guid.NewGuid().ToString("D");

    public PaymentPage(ApiClient api, OrderDto order, Func<bool, Task> completed)
    {
        _api = api;
        _order = order;
        _completed = completed;
        Title = "Thanh toán";
        BackgroundColor = AppUi.Background;
        Content = AppKeyboardHost.Wrap(BuildContent());
        UpdateSummary();
    }

    private View BuildContent()
    {
        _cash.TextChanged += (_, _) => UpdateSummary();
        _transfer.TextChanged += (_, _) => UpdateSummary();
        _card.TextChanged += (_, _) => UpdateSummary();
        _discount.TextChanged += (_, _) => UpdateSummary();

        var pay = AppUi.PrimaryButton("Xác nhận thanh toán");
        pay.Clicked += async (_, _) => await PayAsync();

        var close = AppUi.SecondaryButton("Đóng");
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(AppUi.S(20), AppUi.S(16)),
                Spacing = AppUi.S(14),
                Children =
                {
                    AppUi.Title("Thanh toán"),
                    AppUi.Subtitle($"Đơn {_order.OrderId} | Tổng {AppUi.Money(_order.Total)}"),
                    AppUi.CardView(new VerticalStackLayout
                    {
                        Spacing = 14,
                        Children =
                        {
                            _cash,
                            _transfer,
                            _card,
                            _discount,
                            _summary,
                            new Label { Text = $"Mã chống trùng: {_idempotencyKey}", FontSize = 12, TextColor = AppUi.Muted },
                            new HorizontalStackLayout { Spacing = 8, Children = { _busy, _status } },
                            pay,
                            close
                        }
                    })
                }
            }
        };
    }

    private async Task PayAsync()
    {
        var payments = new List<PaymentLineDto>();
        AddPayment(payments, "cash", _cash.Text);
        AddPayment(payments, "transfer", _transfer.Text);
        AddPayment(payments, "card", _card.Text);

        if (payments.Count == 0)
        {
            _status.Text = "Cần nhập ít nhất một phương thức thanh toán.";
            return;
        }

        _busy.IsRunning = true;
        try
        {
            var result = await _api.PayOrderAsync(_order.OrderId, payments, ParseMoney(_discount.Text), _idempotencyKey);
            _status.Text = $"Đã thanh toán {AppUi.Money(result.PaidAmount)}. Tiền thừa {AppUi.Money(result.Change)}.";
            _idempotencyKey = Guid.NewGuid().ToString("D");
            await Navigation.PopModalAsync();
            await _completed(true);
        }
        catch (Exception ex)
        {
            _status.Text = AppUi.ToVietnameseError(ex);
        }
        finally
        {
            _busy.IsRunning = false;
        }
    }

    private void UpdateSummary()
    {
        var discount = ParseMoney(_discount.Text);
        var paid = ParseMoney(_cash.Text) + ParseMoney(_transfer.Text) + ParseMoney(_card.Text);
        var due = Math.Max(0, _order.Total - discount);
        var delta = paid - due;
        _summary.Text = delta >= 0
            ? $"Tiền thừa: {AppUi.Money(delta)}"
            : $"Còn thiếu: {AppUi.Money(Math.Abs(delta))}";
        _summary.TextColor = delta >= 0 ? AppUi.Success : AppUi.Danger;
    }

    private static void AddPayment(List<PaymentLineDto> lines, string method, string? text)
    {
        var amount = ParseMoney(text);
        if (amount > 0)
        {
            lines.Add(new PaymentLineDto(method, amount));
        }
    }

    private static decimal ParseMoney(string? text)
    {
        return BKPos.Core.Formatting.MoneyFormatter.Parse(text);
    }
}
