using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace BKPos.Revenue.App;

public sealed class DashboardPage : ContentPage
{
    private readonly RevenueApiClient _api;
    private readonly RevenueSessionStore _session;
    private readonly IServiceProvider _services;
    private readonly Picker _storePicker = new() { Title = "Chọn cửa hàng" };
    private readonly DatePicker _datePicker = new() { Date = DateTime.Today };
    private readonly DatePicker _fromPicker = new() { Date = DateTime.Today.AddDays(-6) };
    private readonly DatePicker _toPicker = new() { Date = DateTime.Today };
    private readonly Label _offlineBanner = new() { TextColor = Colors.White, BackgroundColor = AppColors.Red, Padding = new Thickness(10, 6), IsVisible = false };
    private readonly Label _syncStatus = new() { TextColor = AppColors.Muted, FontSize = 12 };
    private readonly Label _revenue = new() { FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Blue };
    private readonly Label _invoiceCount = new() { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy };
    private readonly Label _avg = new() { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy };
    private readonly Label _cancelled = new() { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Red };
    private readonly Label _rangeRevenue = new() { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Blue };
    private readonly Label _rangeInvoices = new() { FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy };
    private readonly Label _rangePayments = new() { FontSize = 13, TextColor = AppColors.Muted };
    private readonly VerticalStackLayout _paymentPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _dailyPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _topPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _invoicePanel = new() { Spacing = 8 };
    private readonly RevenueLineDrawable _lineDrawable = new();
    private readonly PaymentPieDrawable _pieDrawable = new();
    private readonly GraphicsView _lineChart;
    private readonly GraphicsView _pieChart;
    private readonly Button _refreshButton = new() { Text = "Làm mới", BackgroundColor = AppColors.Blue, TextColor = Colors.White, CornerRadius = 10 };
    private readonly Button _logoutButton = new() { Text = "Đăng xuất", BackgroundColor = AppColors.Red, TextColor = Colors.White, CornerRadius = 10 };
    private readonly Button _rangeButton = new() { Text = "Xem khoảng ngày", BackgroundColor = AppColors.Blue, TextColor = Colors.White, CornerRadius = 10 };
    private List<StoreDto> _stores = [];

    public DashboardPage(RevenueApiClient api, RevenueSessionStore session, IServiceProvider services)
    {
        _api = api;
        _session = session;
        _services = services;
        _lineChart = new GraphicsView { Drawable = _lineDrawable, HeightRequest = 150 };
        _pieChart = new GraphicsView { Drawable = _pieDrawable, HeightRequest = 150 };
        Title = "Doanh thu";
        BackgroundColor = AppColors.Surface;
        Build();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void Build()
    {
        _refreshButton.Clicked += async (_, _) => await LoadAsync();
        _logoutButton.Clicked += async (_, _) => await LogoutAsync();
        _rangeButton.Clicked += async (_, _) => await LoadRangeAsync();
        _datePicker.DateSelected += async (_, _) =>
        {
            var date = _datePicker.Date ?? DateTime.Today;
            _fromPicker.Date = date.AddDays(-6);
            _toPicker.Date = date;
            await LoadReportsAsync();
        };
        _storePicker.SelectedIndexChanged += async (_, _) =>
        {
            if (_storePicker.SelectedIndex >= 0 && _storePicker.SelectedIndex < _stores.Count)
            {
                _session.StoreId = _stores[_storePicker.SelectedIndex].StoreId;
                await LoadReportsAsync();
            }
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                Header(),
                Body()
            }
        };
    }

    private View Header()
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 14),
            BackgroundColor = AppColors.Navy,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        grid.Add(new Label { Text = "BKPos Revenue", TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold }, 0, 0);
        grid.Add(_refreshButton, 1, 0);
        grid.Add(_logoutButton, 2, 0);
        grid.Add(_syncStatus, 0, 1);
        Grid.SetColumnSpan(_syncStatus, 3);
        Grid.SetRow(grid, 0);
        return grid;
    }

    private View Body()
    {
        var content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(14),
                Spacing = 12,
                Children =
                {
                    _offlineBanner,
                    FilterCard(),
                    SummaryGrid(),
                    Section("Báo cáo khoảng ngày", RangeCard()),
                    Section("Doanh thu 7 ngày / đường tháng", new VerticalStackLayout { Spacing = 10, Children = { _lineChart, _dailyPanel } }),
                    Section("Phương thức thanh toán", new VerticalStackLayout { Spacing = 10, Children = { _pieChart, _paymentPanel } }),
                    Section("Top món bán chạy", _topPanel),
                    Section("Hóa đơn gần nhất", _invoicePanel)
                }
            }
        };
        Grid.SetRow(content, 1);
        return content;
    }

    private View FilterCard()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        grid.Add(_storePicker, 0, 0);
        grid.Add(_datePicker, 1, 0);
        return Card(grid);
    }

    private View RangeCard()
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        grid.Add(_fromPicker, 0, 0);
        grid.Add(_toPicker, 1, 0);
        grid.Add(_rangeButton, 0, 1);
        Grid.SetColumnSpan(_rangeButton, 2);
        var rangeSummary = new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label { Text = "Doanh thu khoảng ngày", TextColor = AppColors.Muted, FontSize = 12 },
                _rangeRevenue,
                _rangeInvoices,
                _rangePayments
            }
        };
        grid.Add(rangeSummary, 0, 2);
        Grid.SetColumnSpan(rangeSummary, 2);
        return grid;
    }

    private View SummaryGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        grid.Add(SummaryCard("Doanh thu", _revenue), 0, 0);
        grid.Add(SummaryCard("Số hóa đơn", _invoiceCount), 1, 0);
        grid.Add(SummaryCard("Trung bình/đơn", _avg), 0, 1);
        grid.Add(SummaryCard("Đơn hủy", _cancelled), 1, 1);
        return grid;
    }

    private static View SummaryCard(string title, Label value)
        => Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = title, TextColor = AppColors.Muted, FontSize = 12 },
                value
            }
        });

    private static View Section(string title, View content)
        => Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = title, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy },
                content
            }
        });

    private static Border Card(View content)
        => new()
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = AppColors.Card,
            Padding = new Thickness(14),
            Content = content
        };

    private async Task LoadAsync()
    {
        try
        {
            _refreshButton.IsEnabled = false;
            _api.ResetCacheStatus();
            UpdateOfflineBanner();

            var stores = await _api.StoresAsync();
            _stores = stores.Stores.Where(s => s.Enabled).ToList();
            _storePicker.ItemsSource = _stores.Select(s => s.Name).ToList();
            if (_stores.Count == 0)
            {
                throw new InvalidOperationException("Tenant chưa có cửa hàng Revenue Cloud đang bật.");
            }

            var selected = Math.Max(0, _stores.FindIndex(s => s.StoreId == _session.StoreId));
            _storePicker.SelectedIndex = selected;
            _session.StoreId = _stores[selected].StoreId;
            await LoadReportsAsync();
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            await DisplayAlertAsync("Không tải được dữ liệu", ex.Message, "OK");
        }
        finally
        {
            _refreshButton.IsEnabled = true;
        }
    }

    private async Task LoadReportsAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId))
        {
            return;
        }

        var date = _datePicker.Date ?? DateTime.Today;
        var today = await _api.TodayAsync(date, _session.StoreId);
        var month = await _api.MonthAsync(date, _session.StoreId);
        var top = await _api.TopProductsAsync(date.AddDays(-6), date, _session.StoreId);
        var invoices = await _api.InvoicesAsync(date.AddDays(-6), date, _session.StoreId);

        _syncStatus.Text = today.LastSyncAt is null
            ? "Chưa có lần sync cloud"
            : $"Sync gần nhất: {today.LastSyncAt:dd/MM/yyyy HH:mm}";
        _revenue.Text = RevenueApiClient.Money(today.Summary.Revenue);
        _invoiceCount.Text = today.Summary.InvoiceCount.ToString();
        _avg.Text = RevenueApiClient.Money(today.Summary.AverageInvoiceValue);
        _cancelled.Text = today.Summary.CancelledInvoiceCount.ToString();
        RenderDaily(today.Revenue7Days.Count > 0 ? today.Revenue7Days : month.Daily, month.Daily);
        RenderPayment(today.PaymentBreakdown);
        RenderTop(top.Items);
        RenderInvoices(invoices.Items);
        await LoadRangeAsync();
    }

    private async Task LoadRangeAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId))
        {
            return;
        }

        var from = _fromPicker.Date ?? DateTime.Today.AddDays(-6);
        var to = _toPicker.Date ?? DateTime.Today;
        if (from.Date > to.Date)
        {
            await DisplayAlertAsync("Khoảng ngày không hợp lệ", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "OK");
            return;
        }

        try
        {
            _rangeButton.IsEnabled = false;
            var range = await _api.RangeAsync(from, to, _session.StoreId);
            _rangeRevenue.Text = RevenueApiClient.Money(range.Summary.Revenue);
            _rangeInvoices.Text = $"{range.Summary.InvoiceCount} hóa đơn, {range.Summary.CancelledInvoiceCount} hóa đơn hủy";
            _rangePayments.Text =
                $"Tiền mặt {RevenueApiClient.Money(range.Summary.CashAmount)} • CK {RevenueApiClient.Money(range.Summary.TransferAmount)} • Thẻ {RevenueApiClient.Money(range.Summary.CardAmount)} • Khác {RevenueApiClient.Money(range.Summary.OtherAmount)}";
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            await DisplayAlertAsync("Không tải được báo cáo khoảng ngày", ex.Message, "OK");
        }
        finally
        {
            _rangeButton.IsEnabled = true;
        }
    }

    private void RenderDaily(IReadOnlyList<DailyPoint> points, IReadOnlyList<DailyPoint> monthPoints)
    {
        _dailyPanel.Clear();
        var max = points.Count == 0 ? 1 : Math.Max(1, points.Max(p => p.Revenue));
        _lineDrawable.Points = monthPoints;
        _lineChart.Invalidate();
        foreach (var p in points.TakeLast(7))
        {
            _dailyPanel.Add(BarRow(p.Date, p.Revenue, max, AppColors.Blue));
        }
    }

    private void RenderPayment(IReadOnlyList<PaymentSlice> slices)
    {
        _paymentPanel.Clear();
        var max = slices.Count == 0 ? 1 : Math.Max(1, slices.Max(p => p.Amount));
        _pieDrawable.Slices = slices;
        _pieChart.Invalidate();
        foreach (var p in slices)
        {
            _paymentPanel.Add(BarRow(PaymentLabel(p.Method), p.Amount, max, AppColors.Green));
        }
    }

    private void RenderTop(IReadOnlyList<TopProductDto> items)
    {
        _topPanel.Clear();
        foreach (var item in items.Take(5))
        {
            _topPanel.Add(new Label
            {
                Text = $"{item.ProductName}  •  SL {item.Quantity:N0}  •  {RevenueApiClient.Money(item.Revenue)}",
                TextColor = AppColors.Navy,
                FontSize = 14
            });
        }
        if (_topPanel.Children.Count == 0) _topPanel.Add(EmptyLabel("Chưa có dữ liệu."));
    }

    private void RenderInvoices(IReadOnlyList<InvoiceListItem> items)
    {
        _invoicePanel.Clear();
        foreach (var item in items.Take(10))
        {
            var label = new Label
            {
                Text = $"{item.BusinessDate}  {item.TableName}  {RevenueApiClient.Money(item.Total)}  ({item.Status})",
                TextColor = AppColors.Navy,
                FontSize = 14
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowInvoiceDetailAsync(item.InvoiceId);
            label.GestureRecognizers.Add(tap);
            _invoicePanel.Add(label);
        }
        if (_invoicePanel.Children.Count == 0) _invoicePanel.Add(EmptyLabel("Chưa có hóa đơn."));
    }

    private async Task ShowInvoiceDetailAsync(string invoiceId)
    {
        try
        {
            var invoice = await _api.InvoiceDetailAsync(invoiceId, _session.StoreId);
            var payments = invoice.Payments.Count == 0
                ? "Không có thông tin thanh toán"
                : string.Join("\n", invoice.Payments.Select(x => $"{PaymentLabel(x.Method)}: {RevenueApiClient.Money(x.Amount)}"));
            var items = invoice.Items.Count == 0
                ? "Không có món"
                : string.Join("\n", invoice.Items.Select(x => $"{x.ProductName} x{x.Quantity:N0}: {RevenueApiClient.Money(x.LineTotal)}"));
            await DisplayAlertAsync(
                $"{invoice.TableName} - {RevenueApiClient.Money(invoice.Total)}",
                $"Ngày: {invoice.BusinessDate}\nTrạng thái: {invoice.Status}\n\nThanh toán:\n{payments}\n\nMón:\n{items}",
                "Đóng");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Không tải được chi tiết hóa đơn", ex.Message, "OK");
        }
    }

    private static View BarRow(string label, decimal value, decimal max, Color color)
    {
        var ratio = max <= 0 ? 0 : Math.Clamp((double)(value / max), 0, 1);
        var fill = ratio <= 0 ? 0.001 : ratio;
        var remainder = Math.Max(0.001, 1 - fill);
        var track = new Grid
        {
            BackgroundColor = Color.FromArgb("#E2E8F0"),
            HeightRequest = 10,
            HorizontalOptions = LayoutOptions.Fill,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(fill, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(remainder, GridUnitType.Star))
            }
        };
        track.Add(new BoxView
        {
            Color = color,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }, 0, 0);

        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = $"{label}: {RevenueApiClient.Money(value)}", TextColor = AppColors.Navy, FontSize = 13 },
                track
            }
        };
    }

    private void UpdateOfflineBanner()
    {
        var cache = _api.CacheStatus;
        if (cache.FromCache)
        {
            _offlineBanner.IsVisible = true;
            _offlineBanner.BackgroundColor = cache.IsStale ? Color.FromArgb("#B45309") : AppColors.Red;
            _offlineBanner.Text = cache.IsStale
                ? $"Dữ liệu offline đã cũ. Dữ liệu lưu lúc {cache.CachedAt:dd/MM/yyyy HH:mm}."
                : $"Đang dùng dữ liệu cache. Dữ liệu lưu lúc {cache.CachedAt:dd/MM/yyyy HH:mm}.";
            return;
        }

        _offlineBanner.BackgroundColor = AppColors.Red;
        _offlineBanner.IsVisible = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;
        _offlineBanner.Text = _offlineBanner.IsVisible ? "Không có kết nối Internet." : string.Empty;
    }

    private static Label EmptyLabel(string text)
        => new() { Text = text, TextColor = AppColors.Muted, FontSize = 13 };

    internal static string PaymentLabelForChart(string method) => PaymentLabel(method);

    private static string PaymentLabel(string method) => method switch
    {
        "cash" => "Tiền mặt",
        "transfer" => "Chuyển khoản",
        "card" => "Thẻ",
        _ => "Khác"
    };

    private async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        await Navigation.PushAsync(_services.GetRequiredService<LoginPage>());
        Navigation.RemovePage(this);
    }
}

internal sealed class RevenueLineDrawable : IDrawable
{
    public IReadOnlyList<DailyPoint> Points { get; set; } = [];

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Color.FromArgb("#F8FAFC");
        canvas.FillRoundedRectangle(dirtyRect, 12);
        if (Points.Count == 0)
        {
            DrawEmpty(canvas, dirtyRect, "Chưa có dữ liệu tháng");
            return;
        }

        var max = Math.Max(1, Points.Max(p => p.Revenue));
        var left = dirtyRect.Left + 12;
        var top = dirtyRect.Top + 12;
        var width = dirtyRect.Width - 24;
        var height = dirtyRect.Height - 28;
        canvas.StrokeColor = Color.FromArgb("#CBD5E1");
        canvas.StrokeSize = 1;
        canvas.DrawLine(left, top + height, left + width, top + height);

        canvas.StrokeColor = AppColors.Blue;
        canvas.StrokeSize = 3;
        var last = PointF.Zero;
        for (var i = 0; i < Points.Count; i++)
        {
            var x = left + (Points.Count == 1 ? 0 : width * i / (Points.Count - 1));
            var y = top + height - (float)(Points[i].Revenue / max) * height;
            var current = new PointF(x, y);
            if (i > 0)
            {
                canvas.DrawLine(last.X, last.Y, current.X, current.Y);
            }
            canvas.FillColor = AppColors.Blue;
            canvas.FillCircle(current, 3);
            last = current;
        }
    }

    private static void DrawEmpty(ICanvas canvas, RectF rect, string text)
    {
        canvas.FontColor = AppColors.Muted;
        canvas.DrawString(text, rect, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}

internal sealed class PaymentPieDrawable : IDrawable
{
    public IReadOnlyList<PaymentSlice> Slices { get; set; } = [];
    private static readonly Color[] Colors = [AppColors.Green, AppColors.Blue, Color.FromArgb("#F59E0B"), AppColors.Red];

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Color.FromArgb("#F8FAFC");
        canvas.FillRoundedRectangle(dirtyRect, 12);
        var total = Slices.Sum(s => s.Amount);
        if (total <= 0)
        {
            canvas.FontColor = AppColors.Muted;
            canvas.DrawString("Chưa có dữ liệu thanh toán", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var size = Math.Min(dirtyRect.Width, dirtyRect.Height) - 26;
        var pie = new RectF(dirtyRect.Left + 12, dirtyRect.Top + 12, size, size);
        var start = -90f;
        for (var i = 0; i < Slices.Count; i++)
        {
            var sweep = (float)(Slices[i].Amount / total * 360m);
            canvas.FillColor = Colors[i % Colors.Length];
            canvas.FillArc(pie, start, sweep, true);
            start += sweep;
        }

        canvas.FontColor = AppColors.Navy;
        canvas.FontSize = 12;
        var legendX = pie.Right + 12;
        var legendY = dirtyRect.Top + 18;
        for (var i = 0; i < Slices.Count; i++)
        {
            canvas.FillColor = Colors[i % Colors.Length];
            canvas.FillRectangle(legendX, legendY + i * 24, 12, 12);
            canvas.FontColor = AppColors.Navy;
            canvas.DrawString($"{DashboardPage.PaymentLabelForChart(Slices[i].Method)} {RevenueApiClient.Money(Slices[i].Amount)}", legendX + 18, legendY + i * 24 - 2, dirtyRect.Width - legendX - 22, 18, HorizontalAlignment.Left, VerticalAlignment.Top);
        }
    }
}
