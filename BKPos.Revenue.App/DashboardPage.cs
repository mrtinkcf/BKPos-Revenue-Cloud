using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using MauiPicker = Microsoft.Maui.Controls.Picker;
using MauiDatePicker = Microsoft.Maui.Controls.DatePicker;
using MauiScrollView = Microsoft.Maui.Controls.ScrollView;

namespace BKPos.Revenue.App;

public sealed class DashboardPage : ContentPage
{
    private readonly RevenueApiClient _api;
    private readonly RevenueSessionStore _session;
    private readonly IServiceProvider _services;
    private readonly MauiPicker _storePicker = new() { Title = "Chọn cửa hàng", TextColor = AppColors.Navy, FontSize = 13 };
    private readonly MauiDatePicker _datePicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = 13 };
    private readonly MauiDatePicker _fromPicker = new() { Date = DateTime.Today.AddDays(-6), TextColor = AppColors.Navy, FontSize = 13 };
    private readonly MauiDatePicker _toPicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = 13 };
    private readonly Label _offlineBanner = new() { TextColor = Colors.White, BackgroundColor = AppColors.Red, Padding = new Thickness(12, 8), IsVisible = false, FontSize = 13 };
    private readonly Label _syncStatus = new() { TextColor = Color.FromArgb("#CBD5E1"), FontSize = 12 };
    private readonly Label _revenue = ValueLabel(30, AppColors.Blue);
    private readonly Label _invoiceCount = ValueLabel(22, AppColors.Navy);
    private readonly Label _avg = ValueLabel(22, AppColors.Navy);
    private readonly Label _cancelled = ValueLabel(22, AppColors.Red);
    private readonly Label _rangeRevenue = ValueLabel(22, AppColors.Blue);
    private readonly Label _rangeInvoices = new() { FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy };
    private readonly Label _rangePayments = new() { FontSize = 13, TextColor = AppColors.Muted };
    private readonly VerticalStackLayout _paymentPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _dailyPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _topPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _invoicePanel = new() { Spacing = 8 };
    private readonly RevenueLineDrawable _lineDrawable = new();
    private readonly PaymentPieDrawable _pieDrawable = new();
    private readonly GraphicsView _lineChart;
    private readonly GraphicsView _pieChart;
    private readonly Button _refreshButton = IconButton("Làm mới", AppColors.Blue);
    private readonly Button _logoutButton = IconButton("Thoát", AppColors.Red);
    private readonly Button _rangeButton = IconButton("Xem báo cáo", AppColors.Blue);
    private readonly RefreshView _refreshView = new();
    private readonly IDispatcherTimer? _autoRefreshTimer;
    private bool _loading;
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
        Microsoft.Maui.Controls.NavigationPage.SetHasNavigationBar(this, false);
        On<iOS>().SetUseSafeArea(true);
        _autoRefreshTimer = Dispatcher.CreateTimer();
        _autoRefreshTimer.Interval = TimeSpan.FromSeconds(60);
        _autoRefreshTimer.Tick += async (_, _) => await LoadReportsAsync(silent: true);
        Build();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _autoRefreshTimer?.Start();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        _autoRefreshTimer?.Stop();
        base.OnDisappearing();
    }

    private void Build()
    {
        _refreshButton.Clicked += async (_, _) => await LoadAsync();
        _logoutButton.Clicked += async (_, _) => await LogoutAsync();
        _rangeButton.Clicked += async (_, _) => await LoadRangeAsync();
        _refreshView.Refreshing += async (_, _) => await LoadAsync(fromPull: true);
        _datePicker.DateSelected += async (_, _) =>
        {
            var date = PickerDate(_datePicker);
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

        _refreshView.Content = new MauiScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(12),
                Spacing = 12,
                Children =
                {
                    _offlineBanner,
                    FilterCard(),
                    SummaryGrid(),
                    Section("Báo cáo khoảng ngày", RangeCard()),
                    Section("Doanh thu 7 ngày", new VerticalStackLayout { Spacing = 10, Children = { _lineChart, _dailyPanel } }),
                    Section("Phương thức thanh toán", new VerticalStackLayout { Spacing = 10, Children = { _pieChart, _paymentPanel } }),
                    Section("Top sản phẩm", _topPanel),
                    Section("Hóa đơn gần nhất", _invoicePanel)
                }
            }
        };

        var contentHost = new Grid { Children = { _refreshView } };
        Grid.SetRow(contentHost, 1);

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
                contentHost
            }
        };
    }

    private View Header()
    {
        var titleBlock = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = "BKPos Revenue", TextColor = Colors.White, FontSize = 20, FontAttributes = FontAttributes.Bold },
                _syncStatus
            }
        };
        Grid.SetColumn(titleBlock, 0);

        var refreshHost = new Grid { Children = { _refreshButton } };
        Grid.SetColumn(refreshHost, 1);

        var logoutHost = new Grid { Children = { _logoutButton } };
        Grid.SetColumn(logoutHost, 2);

        return new Grid
        {
            Padding = new Thickness(14, 12),
            BackgroundColor = AppColors.Navy,
            ColumnSpacing = 8,
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
            },
            Children =
            {
                titleBlock,
                refreshHost,
                logoutHost
            }
        };
    }

    private View FilterCard()
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions = { new RowDefinition(GridLength.Auto) }
        };
        grid.Add(InputShell(_storePicker), 0, 0);
        grid.Add(InputShell(_datePicker), 1, 0);
        return Card(grid, 14);
    }

    private View RangeCard()
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 10,
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
        grid.Add(InputShell(_fromPicker), 0, 0);
        grid.Add(InputShell(_toPicker), 1, 0);
        grid.Add(_rangeButton, 0, 1);
        Grid.SetColumnSpan(_rangeButton, 2);
        var rangeSummary = new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label { Text = "Tổng doanh thu", TextColor = AppColors.Muted, FontSize = 12 },
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
        grid.Add(SummaryCard("Doanh thu", _revenue, AppColors.Blue), 0, 0);
        grid.Add(SummaryCard("Hóa đơn", _invoiceCount, AppColors.Green), 1, 0);
        grid.Add(SummaryCard("Trung bình", _avg, Color.FromArgb("#F59E0B")), 0, 1);
        grid.Add(SummaryCard("Đơn hủy", _cancelled, AppColors.Red), 1, 1);
        return grid;
    }

    private static View SummaryCard(string title, Label value, Color accent)
    {
        var box = new BoxView { Color = accent, WidthRequest = 4, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Fill };
        var stack = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = title, TextColor = AppColors.Muted, FontSize = 12 },
                value
            }
        };
        Grid.SetColumn(stack, 1);

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            Children =
            {
                box,
                stack
            }
        };
        return Card(content, 14);
    }

    private static View Section(string title, View content)
        => Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = title, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy },
                content
            }
        }, 14);

    private static Border Card(View content, double padding)
        => new()
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            BackgroundColor = AppColors.Card,
            Padding = new Thickness(padding),
            Content = content
        };

    private static Border InputShell(View content)
        => new()
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Padding = new Thickness(8, 0),
            Content = content
        };

    private async Task LoadAsync(bool fromPull = false)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            _loading = true;
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
            if (!fromPull)
            {
                await DisplayAlertAsync("Không tải được dữ liệu", ex.Message, "OK");
            }
        }
        finally
        {
            _refreshButton.IsEnabled = true;
            _refreshView.IsRefreshing = false;
            _loading = false;
        }
    }

    private async Task LoadReportsAsync(bool silent = false)
    {
        if (_loading && silent)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_session.StoreId))
        {
            return;
        }

        try
        {
            var date = PickerDate(_datePicker);
            var today = await _api.TodayAsync(date, _session.StoreId);
            var month = await _api.MonthAsync(date, _session.StoreId);
            var top = await _api.TopProductsAsync(date.AddDays(-6), date, _session.StoreId);
            var invoices = await _api.InvoicesAsync(date.AddDays(-6), date, _session.StoreId);

            _syncStatus.Text = today.LastSyncAt is null
                ? "Chưa có lần đồng bộ cloud"
                : $"Đồng bộ gần nhất: {today.LastSyncAt:dd/MM/yyyy HH:mm}";
            _revenue.Text = RevenueApiClient.Money(today.Summary.Revenue);
            _invoiceCount.Text = today.Summary.InvoiceCount.ToString("N0");
            _avg.Text = RevenueApiClient.Money(today.Summary.AverageInvoiceValue);
            _cancelled.Text = today.Summary.CancelledInvoiceCount.ToString("N0");
            RenderDaily(today.Revenue7Days.Count > 0 ? today.Revenue7Days : month.Daily, month.Daily);
            RenderPayment(today.PaymentBreakdown);
            RenderTop(top.Items);
            RenderInvoices(invoices.Items);
            await LoadRangeAsync(silent: true);
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            if (!silent)
            {
                await DisplayAlertAsync("Không tải được báo cáo", ex.Message, "OK");
            }
        }
    }

    private async Task LoadRangeAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId))
        {
            return;
        }

        var from = PickerDate(_fromPicker);
        var to = PickerDate(_toPicker);
        if (from.Date > to.Date)
        {
            if (!silent)
            {
                await DisplayAlertAsync("Khoảng ngày không hợp lệ", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "OK");
            }
            return;
        }

        try
        {
            _rangeButton.IsEnabled = false;
            var range = await _api.RangeAsync(from, to, _session.StoreId);
            _rangeRevenue.Text = RevenueApiClient.Money(range.Summary.Revenue);
            _rangeInvoices.Text = $"{range.Summary.InvoiceCount:N0} hóa đơn, {range.Summary.CancelledInvoiceCount:N0} hóa đơn hủy";
            _rangePayments.Text =
                $"Tiền mặt {RevenueApiClient.Money(range.Summary.CashAmount)}  •  CK {RevenueApiClient.Money(range.Summary.TransferAmount)}  •  Thẻ {RevenueApiClient.Money(range.Summary.CardAmount)}  •  Khác {RevenueApiClient.Money(range.Summary.OtherAmount)}";
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            if (!silent)
            {
                await DisplayAlertAsync("Không tải được báo cáo khoảng ngày", ex.Message, "OK");
            }
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
            _dailyPanel.Add(BarRow(ShortDate(p.Date), p.Revenue, max, AppColors.Blue));
        }
        if (_dailyPanel.Children.Count == 0) _dailyPanel.Add(EmptyLabel("Chưa có dữ liệu."));
    }

    private void RenderPayment(IReadOnlyList<PaymentSlice> slices)
    {
        _paymentPanel.Clear();
        var max = slices.Count == 0 ? 1 : Math.Max(1, slices.Max(p => p.Amount));
        _pieDrawable.Slices = slices;
        _pieChart.Invalidate();
        foreach (var p in slices.Where(s => s.Amount > 0))
        {
            _paymentPanel.Add(BarRow(PaymentLabel(p.Method), p.Amount, max, AppColors.Green));
        }
        if (_paymentPanel.Children.Count == 0) _paymentPanel.Add(EmptyLabel("Chưa có dữ liệu thanh toán."));
    }

    private void RenderTop(IReadOnlyList<TopProductDto> items)
    {
        _topPanel.Clear();
        foreach (var item in items.Take(5))
        {
            _topPanel.Add(ListRow(item.ProductName, $"SL {item.Quantity:N0}  •  {RevenueApiClient.Money(item.Revenue)}"));
        }
        if (_topPanel.Children.Count == 0) _topPanel.Add(EmptyLabel("Chưa có dữ liệu."));
    }

    private void RenderInvoices(IReadOnlyList<InvoiceListItem> items)
    {
        _invoicePanel.Clear();
        foreach (var item in items.Take(10))
        {
            var row = ListRow($"{item.TableName} - {RevenueApiClient.Money(item.Total)}", $"{ShortDate(item.BusinessDate)}  •  {StatusLabel(item.Status)}");
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowInvoiceDetailAsync(item.InvoiceId);
            row.GestureRecognizers.Add(tap);
            _invoicePanel.Add(row);
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
                $"Ngày: {invoice.BusinessDate}\nTrạng thái: {StatusLabel(invoice.Status)}\n\nThanh toán:\n{payments}\n\nMón:\n{items}",
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
            HeightRequest = 9,
            HorizontalOptions = LayoutOptions.Fill,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(fill, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(remainder, GridUnitType.Star))
            }
        };
        track.Add(new BoxView { Color = color, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }, 0, 0);
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

    private static View ListRow(string title, string subtitle)
    {
        var subtitleLabel = new Label { Text = subtitle, TextColor = AppColors.Muted, FontSize = 12, HorizontalTextAlignment = TextAlignment.End };
        Grid.SetColumn(subtitleLabel, 1);

        return new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Padding = new Thickness(12, 9),
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Children =
                {
                    new Label { Text = title, TextColor = AppColors.Navy, FontSize = 14, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation },
                    subtitleLabel
                }
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
                ? $"Dữ liệu offline đã cũ. Lưu lúc {cache.CachedAt:dd/MM/yyyy HH:mm}."
                : $"Đang dùng dữ liệu cache. Lưu lúc {cache.CachedAt:dd/MM/yyyy HH:mm}.";
            return;
        }

        _offlineBanner.BackgroundColor = AppColors.Red;
        _offlineBanner.IsVisible = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;
        _offlineBanner.Text = _offlineBanner.IsVisible ? "Không có kết nối Internet." : string.Empty;
    }

    private static Label EmptyLabel(string text)
        => new() { Text = text, TextColor = AppColors.Muted, FontSize = 13 };

    private static Label ValueLabel(double size, Color color)
        => new() { FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = color, LineBreakMode = LineBreakMode.NoWrap };

    private static Button IconButton(string text, Color color)
        => new() { Text = text, BackgroundColor = color, TextColor = Colors.White, CornerRadius = 12, HeightRequest = 40, Padding = new Thickness(12, 0), FontSize = 13, FontAttributes = FontAttributes.Bold };

    internal static string PaymentLabelForChart(string method) => PaymentLabel(method);

    private static string PaymentLabel(string method) => method switch
    {
        "cash" => "Tiền mặt",
        "transfer" => "Chuyển khoản",
        "card" => "Thẻ",
        _ => "Khác"
    };

    private static string StatusLabel(string status) => status switch
    {
        "paid" => "Đã thanh toán",
        "edited" => "Đã sửa",
        "cancelled" => "Đã hủy",
        _ => status
    };

    private static string ShortDate(string date)
        => DateTime.TryParse(date, out var value) ? value.ToString("dd/MM") : date;

    private static DateTime PickerDate(MauiDatePicker picker)
        => picker.Date ?? DateTime.Today;
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
        canvas.FillRoundedRectangle(dirtyRect, 14);
        if (Points.Count == 0)
        {
            DrawEmpty(canvas, dirtyRect, "Chưa có dữ liệu tháng");
            return;
        }

        var max = Math.Max(1, Points.Max(p => p.Revenue));
        var left = dirtyRect.Left + 14;
        var top = dirtyRect.Top + 14;
        var width = dirtyRect.Width - 28;
        var height = dirtyRect.Height - 32;
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
        canvas.FillRoundedRectangle(dirtyRect, 14);
        var rows = Slices.Where(s => s.Amount > 0).ToList();
        var total = rows.Sum(s => s.Amount);
        if (total <= 0)
        {
            canvas.FontColor = AppColors.Muted;
            canvas.DrawString("Chưa có dữ liệu thanh toán", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var size = Math.Min(dirtyRect.Width * 0.42f, dirtyRect.Height - 28);
        var pie = new RectF(dirtyRect.Left + 14, dirtyRect.Top + 14, size, size);
        var start = -90f;
        for (var i = 0; i < rows.Count; i++)
        {
            var sweep = (float)(rows[i].Amount / total * 360m);
            canvas.FillColor = Colors[i % Colors.Length];
            canvas.FillArc(pie, start, sweep, true);
            start += sweep;
        }

        canvas.FontColor = AppColors.Navy;
        canvas.FontSize = 12;
        var legendX = pie.Right + 12;
        var legendY = dirtyRect.Top + 18;
        for (var i = 0; i < rows.Count; i++)
        {
            canvas.FillColor = Colors[i % Colors.Length];
            canvas.FillRectangle(legendX, legendY + i * 24, 12, 12);
            canvas.FontColor = AppColors.Navy;
            canvas.DrawString($"{DashboardPage.PaymentLabelForChart(rows[i].Method)} {RevenueApiClient.Money(rows[i].Amount)}", legendX + 18, legendY + i * 24 - 2, dirtyRect.Right - legendX - 22, 18, HorizontalAlignment.Left, VerticalAlignment.Top);
        }
    }
}


