using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using MauiPicker = Microsoft.Maui.Controls.Picker;
using MauiDatePicker = Microsoft.Maui.Controls.DatePicker;
using MauiScrollView = Microsoft.Maui.Controls.ScrollView;
using MauiNavigationPage = Microsoft.Maui.Controls.NavigationPage;

namespace BKPos.Revenue.App;

public sealed class DashboardPage : ContentPage
{
    // ── dependencies ─────────────────────────────────────────────────────
    private readonly RevenueApiClient _api;
    private readonly RevenueSessionStore _session;
    private readonly IServiceProvider _services;

    // ── data controls (kept for existing Render* / Load* methods) ────────
    private readonly MauiDatePicker _fromPicker = new() { Date = DateTime.Today.AddDays(-6), TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiDatePicker _toPicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiPicker _rangePresetPicker = new() { Title = "Chọn khoảng báo cáo", TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiDatePicker _invoiceFromPicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiDatePicker _invoiceToPicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiPicker _invoicePresetPicker = new() { Title = "Lọc hóa đơn", TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiDatePicker _inventoryFromPicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiDatePicker _inventoryToPicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiPicker _inventoryPresetPicker = new() { Title = "Lọc kho hàng", TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly Label _offlineBanner = new() { TextColor = Colors.White, BackgroundColor = AppColors.Red, Padding = new Thickness(AppUi.S(12), AppUi.S(6)), IsVisible = false, FontSize = AppUi.S(13) };
    private readonly Label _syncStatus = new() { TextColor = Color.FromArgb("#94A3B8"), FontSize = AppUi.S(11) };
    private readonly Label _todayBadge = new() { TextColor = AppColors.Blue, FontSize = AppUi.S(12), FontAttributes = FontAttributes.Bold };
    private readonly Label _revenue = ValueLabel(AppUi.S(28), AppColors.Blue);
    private readonly Label _invoiceCount = ValueLabel(AppUi.S(22), AppColors.Navy);
    private readonly Label _avg = ValueLabel(AppUi.S(22), AppColors.Navy);
    private readonly Label _cancelled = ValueLabel(AppUi.S(22), AppColors.Red);
    private readonly Label _rangeRevenue = ValueLabel(AppUi.S(22), AppColors.Blue);
    private readonly Label _rangeInvoices = new() { FontSize = AppUi.S(14), FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy };
    private readonly Label _rangePayments = new() { FontSize = AppUi.S(12), TextColor = AppColors.Muted, LineBreakMode = LineBreakMode.WordWrap };
    private readonly MauiDatePicker _homeInventoryFromPicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiDatePicker _homeInventoryToPicker = new() { Date = DateTime.Today, TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly MauiPicker _homeInventoryPresetPicker = new() { Title = "Lọc kho hàng", TextColor = AppColors.Navy, FontSize = AppUi.S(13), BackgroundColor = Colors.Transparent };
    private readonly Label _homeInventoryImportValue = ValueLabel(AppUi.S(20), AppColors.Green);
    private readonly Label _homeInventoryExportValue = ValueLabel(AppUi.S(20), AppColors.Orange);
    private readonly Label _homeInventoryStockValue = ValueLabel(AppUi.S(20), AppColors.Blue);
    private readonly Label _homeInventoryStatus = new() { FontSize = AppUi.S(12), TextColor = AppColors.Muted, LineBreakMode = LineBreakMode.WordWrap };
    private readonly VerticalStackLayout _paymentPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _dailyPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _topPanel = new() { Spacing = 8 };
    private readonly VerticalStackLayout _inventoryPanel = new() { Spacing = 8 };
    private readonly Label _inventorySummary = new() { FontSize = AppUi.S(12), TextColor = AppColors.Muted, LineBreakMode = LineBreakMode.WordWrap };
    private readonly VerticalStackLayout _invoicePanel = new() { Spacing = 8 };
    private readonly Label _invoicePageInfo = new() { TextColor = AppColors.Muted, FontSize = AppUi.S(12) };
    private readonly RevenueLineDrawable _lineDrawable = new();
    private readonly PaymentPieDrawable _pieDrawable = new();
    private readonly GraphicsView _lineChart;
    private readonly GraphicsView _pieChart;
    private readonly Button _rangeButton = new()
    {
        Text = "Xem báo cáo",
        BackgroundColor = AppColors.Blue,
        TextColor = Colors.White,
        CornerRadius = 12,
        HeightRequest = AppUi.S(46),
        FontSize = AppUi.S(14),
        FontAttributes = FontAttributes.Bold
    };
    private readonly Button _invoiceFilterButton = new()
    {
        Text = "Lọc hóa đơn",
        BackgroundColor = AppColors.Blue,
        TextColor = Colors.White,
        CornerRadius = 12,
        HeightRequest = AppUi.S(44),
        FontSize = AppUi.S(13),
        FontAttributes = FontAttributes.Bold
    };
    private readonly Button _invoicePrevButton = new()
    {
        Text = "Trước",
        BackgroundColor = Color.FromArgb("#E2E8F0"),
        TextColor = AppColors.Navy,
        CornerRadius = 10,
        HeightRequest = AppUi.S(40),
        FontSize = AppUi.S(12)
    };
    private readonly Button _invoiceNextButton = new()
    {
        Text = "Tiếp",
        BackgroundColor = AppColors.Blue,
        TextColor = Colors.White,
        CornerRadius = 10,
        HeightRequest = AppUi.S(40),
        FontSize = AppUi.S(12)
    };
    private readonly Button _homeInventoryButton = new()
    {
        Text = "Xem thống kê kho",
        BackgroundColor = AppColors.Blue,
        TextColor = Colors.White,
        CornerRadius = 12,
        HeightRequest = AppUi.S(44),
        FontSize = AppUi.S(13),
        FontAttributes = FontAttributes.Bold
    };
    private readonly Button _inventoryFilterButton = new()
    {
        Text = "Xem kho hàng",
        BackgroundColor = AppColors.Blue,
        TextColor = Colors.White,
        CornerRadius = 12,
        HeightRequest = AppUi.S(44),
        FontSize = AppUi.S(13),
        FontAttributes = FontAttributes.Bold
    };
    private readonly List<RefreshView> _refreshViews = [];
    private readonly IDispatcherTimer? _autoRefreshTimer;
    private const int InvoicePageSize = 20;
    private bool _loading;
    private bool _updatingRangePreset;
    private bool _updatingInvoicePreset;
    private bool _updatingInventoryPreset;
    private bool _updatingHomeInventoryPreset;
    private bool _autoRefreshing;
    private int _invoicePage = 1;
    private List<StoreDto> _stores = [];
    private string _storeTimezone = "Asia/Ho_Chi_Minh";
    private OpenTablesReport? _lastOpenTablesReport;

    // ── header action buttons ─────────────────────────────────────────────
    private readonly Button _refreshButton = new()
    {
        Text = "⟳",
        BackgroundColor = Colors.Transparent,
        TextColor = Colors.White,
        FontSize = AppUi.S(22),
        HeightRequest = 44,
        WidthRequest = 44,
        Padding = Thickness.Zero
    };
    private readonly Button _logoutButton = new()
    {
        Text = "↩",
        BackgroundColor = Colors.Transparent,
        TextColor = Color.FromArgb("#94A3B8"),
        FontSize = AppUi.S(20),
        HeightRequest = 44,
        WidthRequest = 44,
        Padding = Thickness.Zero
    };

    // ── tab navigation ────────────────────────────────────────────────────
    private enum Tab { Home, Tables, Invoices, Inventory }
    private Tab _activeTab = Tab.Home;
    private readonly bool[] _loadedTabs = new bool[4];
    private DateTimeOffset _lastSwipeAt = DateTimeOffset.MinValue;
    private bool _switchingTab;
    private bool _tabVisibilityInitialized;
    private readonly Label _headerTitle = new() { TextColor = Colors.White, FontSize = AppUi.S(18), FontAttributes = FontAttributes.Bold };
    private readonly Label[] _tabIconLabels = new Label[4];
    private readonly Label[] _tabTextLabels = new Label[4];
    private View? _homeView, _tablesView, _invoicesView, _topView;

    private readonly Label _tableCountLabel = new() { Text = "–", FontSize = AppUi.S(32), FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy };
    private readonly Label _tableEstimatedLabel = ValueLabel(AppUi.S(22), AppColors.Blue);
    private readonly VerticalStackLayout _tablesPanel = new() { Spacing = AppUi.S(10) };

    private static readonly string[] TabTitles = ["Tổng quan", "Bàn phục vụ", "Hóa đơn", "Kho hàng"];
    private static readonly string[] TabIconChars = ["📈", "🪑", "🧾", "📦"];

    // ─────────────────────────────────────────────────────────────────────
    public DashboardPage(RevenueApiClient api, RevenueSessionStore session, IServiceProvider services)
    {
        _api = api;
        _session = session;
        _services = services;
        _lineChart = CreateChartView(_lineDrawable);
        _pieChart = CreateChartView(_pieDrawable);
        BackgroundColor = Colors.White;
        MauiNavigationPage.SetHasNavigationBar(this, false);
        HideSoftInputOnTapped = true;
        On<iOS>().SetUseSafeArea(true);
        _todayBadge.Text = $"DOANH THU HÔM NAY • {StoreToday():dd/MM/yyyy}";
        _autoRefreshTimer = Dispatcher.CreateTimer();
        _autoRefreshTimer.Interval = TimeSpan.FromSeconds(60);
        _autoRefreshTimer.Tick += async (_, _) => await AutoRefreshAsync();
        Build();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _autoRefreshTimer?.Start();
        await LoadAsync();
        InvalidateChartsSoon();
    }

    protected override void OnDisappearing()
    {
        _autoRefreshTimer?.Stop();
        base.OnDisappearing();
    }

    // ── UI build ──────────────────────────────────────────────────────────
    private void Build()
    {
        _refreshButton.Clicked += async (_, _) => await LoadAsync();
        _logoutButton.Clicked += async (_, _) => await LogoutAsync();
        _rangeButton.Clicked += async (_, _) => await LoadRangeAsync();
        _invoiceFilterButton.Clicked += async (_, _) =>
        {
            _invoicePage = 1;
            await LoadInvoicesAsync();
        };
        _invoicePrevButton.Clicked += async (_, _) =>
        {
            if (_invoicePage <= 1) return;
            _invoicePage--;
            await LoadInvoicesAsync();
        };
        _invoiceNextButton.Clicked += async (_, _) =>
        {
            _invoicePage++;
            await LoadInvoicesAsync();
        };
        _rangePresetPicker.Items.Add("Hôm nay");
        _rangePresetPicker.Items.Add("Hôm qua");
        _rangePresetPicker.Items.Add("7 ngày gần nhất");
        _rangePresetPicker.Items.Add("Tháng trước");
        _rangePresetPicker.Items.Add("Tùy chọn");
        _rangePresetPicker.SelectedIndexChanged += async (_, _) =>
        {
            ApplyRangePreset();
            await LoadRangeAsync(silent: true);
        };
        _fromPicker.DateSelected += (_, _) => MarkCustomRange();
        _toPicker.DateSelected += (_, _) => MarkCustomRange();
        _rangePresetPicker.SelectedIndex = 2;

        _homeInventoryButton.Clicked += async (_, _) => await LoadHomeInventoryAsync();
        _homeInventoryPresetPicker.Items.Add("Hôm nay");
        _homeInventoryPresetPicker.Items.Add("Hôm qua");
        _homeInventoryPresetPicker.Items.Add("7 ngày gần nhất");
        _homeInventoryPresetPicker.Items.Add("Tháng trước");
        _homeInventoryPresetPicker.Items.Add("Tùy chọn");
        _homeInventoryPresetPicker.SelectedIndexChanged += async (_, _) =>
        {
            ApplyHomeInventoryPreset();
            await LoadHomeInventoryAsync(silent: true);
        };
        _homeInventoryFromPicker.DateSelected += (_, _) => MarkCustomHomeInventoryRange();
        _homeInventoryToPicker.DateSelected += (_, _) => MarkCustomHomeInventoryRange();
        _homeInventoryPresetPicker.SelectedIndex = 0;

        _invoicePresetPicker.Items.Add("Hôm nay");
        _invoicePresetPicker.Items.Add("Hôm qua");
        _invoicePresetPicker.Items.Add("7 ngày gần nhất");
        _invoicePresetPicker.Items.Add("Tháng trước");
        _invoicePresetPicker.Items.Add("Tùy chọn");
        _invoicePresetPicker.SelectedIndexChanged += async (_, _) =>
        {
            ApplyInvoicePreset();
            _invoicePage = 1;
            await LoadInvoicesAsync(silent: true);
        };
        _invoiceFromPicker.DateSelected += (_, _) => MarkCustomInvoiceRange();
        _invoiceToPicker.DateSelected += (_, _) => MarkCustomInvoiceRange();
        _invoicePresetPicker.SelectedIndex = 0;

        _inventoryFilterButton.Clicked += async (_, _) => await LoadInventoryAsync();
        _inventoryPresetPicker.Items.Add("Hôm nay");
        _inventoryPresetPicker.Items.Add("Hôm qua");
        _inventoryPresetPicker.Items.Add("7 ngày gần nhất");
        _inventoryPresetPicker.Items.Add("Tháng trước");
        _inventoryPresetPicker.Items.Add("Tùy chọn");
        _inventoryPresetPicker.SelectedIndexChanged += async (_, _) =>
        {
            ApplyInventoryPreset();
            await LoadInventoryAsync(silent: true);
        };
        _inventoryFromPicker.DateSelected += (_, _) => MarkCustomInventoryRange();
        _inventoryToPicker.DateSelected += (_, _) => MarkCustomInventoryRange();
        _inventoryPresetPicker.SelectedIndex = 0;

        _homeView    = BuildHomeView();
        _tablesView  = BuildTablesView();
        _invoicesView = BuildInvoicesView();
        _topView     = BuildInventoryView();

        var contentGrid = new Grid();
        contentGrid.Children.Add(_homeView);
        contentGrid.Children.Add(_tablesView);
        contentGrid.Children.Add(_invoicesView);
        contentGrid.Children.Add(_topView);
        AddSwipeGestures(contentGrid);
        Grid.SetRow(contentGrid, 1);

        var tabBar = BuildTabBar();
        Grid.SetRow(tabBar, 2);

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children = { BuildHeader(), contentGrid, tabBar }
        };

        SwitchTab(Tab.Home);
    }

    // ── header ────────────────────────────────────────────────────────────
    private View BuildHeader()
    {
        Grid.SetColumn(_refreshButton, 1);
        Grid.SetColumn(_logoutButton, 2);

        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 4,
            Children = { _headerTitle, _refreshButton, _logoutButton }
        };

        return new VerticalStackLayout
        {
            BackgroundColor = AppColors.Navy,
            Padding = new Thickness(AppUi.S(14), AppUi.S(10), AppUi.S(14), AppUi.S(10)),
            Spacing = AppUi.S(6),
            Children = { _offlineBanner, titleRow, _syncStatus }
        };
    }

    // ── home tab ──────────────────────────────────────────────────────────
    private View BuildHomeView()
    {
        var rangeGrid = new Grid
        {
            ColumnSpacing = AppUi.S(8),
            RowSpacing = AppUi.S(10),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        var presetShell = InputShell(_rangePresetPicker);
        rangeGrid.Add(presetShell, 0, 0);
        Grid.SetColumnSpan(presetShell, 2);
        rangeGrid.Add(InputShell(_fromPicker), 0, 1);
        rangeGrid.Add(InputShell(_toPicker), 1, 1);
        rangeGrid.Add(_rangeButton, 0, 2);
        Grid.SetColumnSpan(_rangeButton, 2);
        var rangeSummary = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = "Tổng doanh thu", TextColor = AppColors.Muted, FontSize = AppUi.S(12) },
                _rangeRevenue, _rangeInvoices
            }
        };
        rangeGrid.Add(rangeSummary, 0, 3);
        Grid.SetColumnSpan(rangeSummary, 2);

        var inventoryFilterGrid = new Grid
        {
            ColumnSpacing = AppUi.S(8),
            RowSpacing = AppUi.S(10),
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
        var inventoryPresetShell = InputShell(_homeInventoryPresetPicker);
        inventoryFilterGrid.Add(inventoryPresetShell, 0, 0);
        Grid.SetColumnSpan(inventoryPresetShell, 2);
        inventoryFilterGrid.Add(InputShell(_homeInventoryFromPicker), 0, 1);
        inventoryFilterGrid.Add(InputShell(_homeInventoryToPicker), 1, 1);
        inventoryFilterGrid.Add(_homeInventoryButton, 0, 2);
        Grid.SetColumnSpan(_homeInventoryButton, 2);

        var inventoryStats = new Grid
        {
            ColumnSpacing = AppUi.S(8),
            RowSpacing = AppUi.S(8),
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
        inventoryStats.Add(SummaryCard("Tổng nhập kho", _homeInventoryImportValue, AppColors.Green), 0, 0);
        inventoryStats.Add(SummaryCard("Tổng xuất kho", _homeInventoryExportValue, AppColors.Orange), 1, 0);
        var stockCard = SummaryCard("Tổng giá trị tồn kho", _homeInventoryStockValue, AppColors.Blue);
        inventoryStats.Add(stockCard, 0, 1);
        Grid.SetColumnSpan(stockCard, 2);

        return Refreshable(new MauiScrollView
        {
            Content = new VerticalStackLayout
            {
                BackgroundColor = AppColors.Surface,
                Padding = new Thickness(AppUi.S(12)),
                Spacing = AppUi.S(12),
                Children =
                {
                    TodaySummaryBadge(),
                    SummaryGrid(),
                    Section("Báo cáo khoảng ngày", rangeGrid),
                    Section("Thống kê kho", new VerticalStackLayout
                    {
                        Spacing = AppUi.S(10),
                        Children = { inventoryFilterGrid, inventoryStats, _homeInventoryStatus }
                    }),
                    Section("Doanh thu 7 ngày",
                        new VerticalStackLayout { Spacing = AppUi.S(10), Children = { _lineChart, _dailyPanel } })
                }
            }
        });
    }

    // ── tables tab ────────────────────────────────────────────────────────
    private View BuildTablesView()
    {
        var leftCol = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = "Đang phục vụ", TextColor = AppColors.Muted, FontSize = AppUi.S(12) },
                _tableCountLabel,
                new Label { Text = "bàn", TextColor = AppColors.Muted, FontSize = AppUi.S(13) }
            }
        };
        var rightCol = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = "Doanh thu ước tính", TextColor = AppColors.Muted, FontSize = AppUi.S(12) },
                _tableEstimatedLabel
            }
        };
        Grid.SetColumn(rightCol, 1);

        var statsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = AppUi.S(16),
            Children = { leftCol, rightCol }
        };

        return Refreshable(new MauiScrollView
        {
            Content = new VerticalStackLayout
            {
                BackgroundColor = AppColors.Surface,
                Padding = new Thickness(AppUi.S(12)),
                Spacing = AppUi.S(12),
                Children =
                {
                    Card(statsGrid, AppUi.S(16)),
                    Section("Danh sách bàn", _tablesPanel)
                }
            }
        });
    }

    // ── invoices tab ──────────────────────────────────────────────────────
    private View BuildInvoicesView()
    {
        var filterGrid = new Grid
        {
            ColumnSpacing = AppUi.S(8),
            RowSpacing = AppUi.S(10),
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

        var presetShell = InputShell(_invoicePresetPicker);
        filterGrid.Add(presetShell, 0, 0);
        Grid.SetColumnSpan(presetShell, 2);
        filterGrid.Add(InputShell(_invoiceFromPicker), 0, 1);
        filterGrid.Add(InputShell(_invoiceToPicker), 1, 1);
        filterGrid.Add(_invoiceFilterButton, 0, 2);
        Grid.SetColumnSpan(_invoiceFilterButton, 2);

        var pagingGrid = new Grid
        {
            ColumnSpacing = AppUi.S(8),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Children = { _invoicePrevButton, _invoicePageInfo, _invoiceNextButton }
        };
        Grid.SetColumn(_invoicePageInfo, 1);
        Grid.SetColumn(_invoiceNextButton, 2);

        return Refreshable(new MauiScrollView
        {
            Content = new VerticalStackLayout
            {
                BackgroundColor = AppColors.Surface,
                Padding = new Thickness(AppUi.S(12)),
                Spacing = AppUi.S(12),
                Children =
                {
                    Section("Bộ lọc hóa đơn", filterGrid),
                    Section("Danh sách hóa đơn", _invoicePanel),
                    Card(pagingGrid, AppUi.S(10))
                }
            }
        });
    }

    // ── inventory tab ─────────────────────────────────────────────────────
    private View BuildInventoryView()
    {
        var filterGrid = new Grid
        {
            ColumnSpacing = AppUi.S(8),
            RowSpacing = AppUi.S(10),
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

        var presetShell = InputShell(_inventoryPresetPicker);
        filterGrid.Add(presetShell, 0, 0);
        Grid.SetColumnSpan(presetShell, 2);
        filterGrid.Add(InputShell(_inventoryFromPicker), 0, 1);
        filterGrid.Add(InputShell(_inventoryToPicker), 1, 1);
        filterGrid.Add(_inventoryFilterButton, 0, 2);
        Grid.SetColumnSpan(_inventoryFilterButton, 2);

        return Refreshable(new MauiScrollView
        {
            Content = new VerticalStackLayout
            {
                BackgroundColor = AppColors.Surface,
                Padding = new Thickness(AppUi.S(12)),
                Spacing = AppUi.S(12),
                Children =
                {
                    Section("Bộ lọc kho hàng", filterGrid),
                    Section("Nhập - xuất - tồn", new VerticalStackLayout
                    {
                        Spacing = AppUi.S(10),
                        Children = { _inventorySummary, _inventoryPanel }
                    })
                }
            }
        });
    }

    // ── tab bar ───────────────────────────────────────────────────────────
    private View BuildTabBar()
    {
        var tabs = new[] { Tab.Home, Tab.Tables, Tab.Invoices, Tab.Inventory };
        var columns = Enumerable.Repeat(new ColumnDefinition(GridLength.Star), 4).ToList();

        var grid = new Grid
        {
            BackgroundColor = Colors.White,
            ColumnDefinitions = new ColumnDefinitionCollection(columns.ToArray())
        };

        // Top divider
        var divider = new BoxView { Color = Color.FromArgb("#E2E8F0"), HeightRequest = 1, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Start };
        Grid.SetColumnSpan(divider, 4);
        grid.Children.Add(divider);

        for (var i = 0; i < 4; i++)
        {
            var idx = i;
            _tabIconLabels[i] = new Label
            {
                Text = TabIconChars[i],
                FontSize = AppUi.S(24),
                HorizontalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Center
            };
            _tabTextLabels[i] = new Label
            {
                Text = TabTitles[i],
                FontSize = AppUi.S(10),
                HorizontalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Center
            };

            var cell = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Spacing = 2,
                Padding = new Thickness(0, AppUi.S(8), 0, AppUi.S(6)),
                Children = { _tabIconLabels[idx], _tabTextLabels[idx] }
            };

            var tapHost = new ContentView
            {
                HeightRequest = AppUi.S(60),
                HorizontalOptions = LayoutOptions.Fill,
                Content = cell
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                var direction = Math.Sign(idx - (int)_activeTab);
                SwitchTab(tabs[idx], animate: direction != 0, direction: direction);
            };
            tapHost.GestureRecognizers.Add(tap);

            Grid.SetColumn(tapHost, i);
            grid.Children.Add(tapHost);
        }

        return grid;
    }

    private RefreshView Refreshable(View content)
    {
        content.HorizontalOptions = LayoutOptions.Fill;
        content.VerticalOptions = LayoutOptions.Fill;
        AddSwipeGestures(content);
        if (content is MauiScrollView scrollView)
        {
            scrollView.HorizontalOptions = LayoutOptions.Fill;
            scrollView.VerticalOptions = LayoutOptions.Fill;
            if (scrollView.Content is View scrollContent)
            {
                scrollContent.MinimumHeightRequest = Math.Max(AppUi.ScreenHeight - AppUi.S(170), AppUi.S(420));
                AddSwipeGestures(scrollContent);
            }
        }

        var refreshView = new RefreshView
        {
            Content = content,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            RefreshColor = AppColors.Blue
        };
        refreshView.Refreshing += async (_, _) => await LoadAsync(fromPull: true);
        AddSwipeGestures(refreshView);
        _refreshViews.Add(refreshView);
        return refreshView;
    }

    private void AddSwipeGestures(View view)
    {
        var left = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
        left.Swiped += (_, _) => SwitchRelativeTab(1);
        var right = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
        right.Swiped += (_, _) => SwitchRelativeTab(-1);
        view.GestureRecognizers.Add(left);
        view.GestureRecognizers.Add(right);
    }

    private void SwitchRelativeTab(int delta)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastSwipeAt < TimeSpan.FromMilliseconds(350))
        {
            return;
        }

        _lastSwipeAt = now;
        var next = Math.Clamp((int)_activeTab + delta, 0, TabTitles.Length - 1);
        if (next != (int)_activeTab)
        {
            SwitchTab((Tab)next, animate: true, direction: delta);
        }
    }

    private async void SwitchTab(Tab tab, bool animate = false, int direction = 0)
    {
        if (_switchingTab || (tab == _activeTab && _tabVisibilityInitialized && !animate))
        {
            return;
        }

        var previousTab = _activeTab;
        var previousView = GetTabView(previousTab);
        var nextView = GetTabView(tab);

        _switchingTab = true;
        try
        {
            _activeTab = tab;
            _headerTitle.Text = TabTitles[(int)tab];

            if (animate && _tabVisibilityInitialized && previousView is not null && nextView is not null && previousView != nextView)
            {
                direction = direction == 0 ? Math.Sign((int)tab - (int)previousTab) : Math.Sign(direction);
                await AnimateTabTransitionAsync(previousView, nextView, direction == 0 ? 1 : direction);
            }
            else
            {
                SetVisibleTab(tab);
            }

            _tabVisibilityInitialized = true;

            for (var i = 0; i < 4; i++)
            {
                var active = i == (int)tab;
                _tabIconLabels[i].TextColor = active ? AppColors.Blue : AppColors.Muted;
                _tabTextLabels[i].TextColor = active ? AppColors.Blue : AppColors.Muted;
            }

            if (tab == Tab.Home)
            {
                InvalidateChartsSoon();
            }

            if (!_loading && _stores.Count > 0 && !_loadedTabs[(int)tab])
            {
                await LoadActiveTabAsync(silent: true, force: false);
            }
        }
        finally
        {
            _switchingTab = false;
        }
    }

    private View? GetTabView(Tab tab) => tab switch
    {
        Tab.Home => _homeView,
        Tab.Tables => _tablesView,
        Tab.Invoices => _invoicesView,
        Tab.Inventory => _topView,
        _ => null
    };

    private void SetVisibleTab(Tab tab)
    {
        _homeView!.IsVisible = tab == Tab.Home;
        _tablesView!.IsVisible = tab == Tab.Tables;
        _invoicesView!.IsVisible = tab == Tab.Invoices;
        _topView!.IsVisible = tab == Tab.Inventory;

        foreach (var view in new[] { _homeView, _tablesView, _invoicesView, _topView }.Where(v => v is not null).Cast<View>())
        {
            view.Opacity = 1;
            view.TranslationX = 0;
        }
    }

    private async Task AnimateTabTransitionAsync(View previousView, View nextView, int direction)
    {
        var width = Width > 0 ? Width : AppUi.ScreenWidth;
        nextView.TranslationX = direction * width;
        nextView.Opacity = 0.98;
        nextView.IsVisible = true;

        var outgoing = previousView.TranslateTo(-direction * width * 0.28, 0, 180, Easing.CubicOut);
        var incoming = nextView.TranslateTo(0, 0, 220, Easing.CubicOut);
        await Task.WhenAll(outgoing, incoming);

        previousView.IsVisible = false;
        previousView.TranslationX = 0;
        previousView.Opacity = 1;
        nextView.TranslationX = 0;
        nextView.Opacity = 1;
    }

    private void ApplyRangePreset()
    {
        var today = DateTime.Today;
        _updatingRangePreset = true;
        switch (_rangePresetPicker.SelectedIndex)
        {
            case 0:
                _fromPicker.Date = today;
                _toPicker.Date = today;
                break;
            case 1:
                _fromPicker.Date = today.AddDays(-1);
                _toPicker.Date = today.AddDays(-1);
                break;
            case 3:
                var firstThisMonth = new DateTime(today.Year, today.Month, 1);
                _fromPicker.Date = firstThisMonth.AddMonths(-1);
                _toPicker.Date = firstThisMonth.AddDays(-1);
                break;
            case 2:
                _fromPicker.Date = today.AddDays(-6);
                _toPicker.Date = today;
                break;
            default:
                break;
        }
        _updatingRangePreset = false;
    }

    private void MarkCustomRange()
    {
        if (_updatingRangePreset)
        {
            return;
        }

        _rangePresetPicker.SelectedIndex = 4;
    }

    private void ApplyHomeInventoryPreset()
    {
        var today = DateTime.Today;
        _updatingHomeInventoryPreset = true;
        switch (_homeInventoryPresetPicker.SelectedIndex)
        {
            case 0:
                _homeInventoryFromPicker.Date = today;
                _homeInventoryToPicker.Date = today;
                break;
            case 1:
                _homeInventoryFromPicker.Date = today.AddDays(-1);
                _homeInventoryToPicker.Date = today.AddDays(-1);
                break;
            case 3:
                var firstThisMonth = new DateTime(today.Year, today.Month, 1);
                _homeInventoryFromPicker.Date = firstThisMonth.AddMonths(-1);
                _homeInventoryToPicker.Date = firstThisMonth.AddDays(-1);
                break;
            case 2:
                _homeInventoryFromPicker.Date = today.AddDays(-6);
                _homeInventoryToPicker.Date = today;
                break;
            default:
                break;
        }
        _updatingHomeInventoryPreset = false;
    }

    private void MarkCustomHomeInventoryRange()
    {
        if (_updatingHomeInventoryPreset)
        {
            return;
        }

        _homeInventoryPresetPicker.SelectedIndex = 4;
    }

    private void ApplyInvoicePreset()
    {
        var today = DateTime.Today;
        _updatingInvoicePreset = true;
        switch (_invoicePresetPicker.SelectedIndex)
        {
            case 0:
                _invoiceFromPicker.Date = today;
                _invoiceToPicker.Date = today;
                break;
            case 1:
                _invoiceFromPicker.Date = today.AddDays(-1);
                _invoiceToPicker.Date = today.AddDays(-1);
                break;
            case 3:
                var firstThisMonth = new DateTime(today.Year, today.Month, 1);
                _invoiceFromPicker.Date = firstThisMonth.AddMonths(-1);
                _invoiceToPicker.Date = firstThisMonth.AddDays(-1);
                break;
            case 2:
                _invoiceFromPicker.Date = today.AddDays(-6);
                _invoiceToPicker.Date = today;
                break;
            default:
                break;
        }
        _updatingInvoicePreset = false;
    }

    private void MarkCustomInvoiceRange()
    {
        if (_updatingInvoicePreset)
        {
            return;
        }

        _invoicePage = 1;
        _invoicePresetPicker.SelectedIndex = 4;
    }

    private void ApplyInventoryPreset()
    {
        var today = DateTime.Today;
        _updatingInventoryPreset = true;
        switch (_inventoryPresetPicker.SelectedIndex)
        {
            case 0:
                _inventoryFromPicker.Date = today;
                _inventoryToPicker.Date = today;
                break;
            case 1:
                _inventoryFromPicker.Date = today.AddDays(-1);
                _inventoryToPicker.Date = today.AddDays(-1);
                break;
            case 3:
                var firstThisMonth = new DateTime(today.Year, today.Month, 1);
                _inventoryFromPicker.Date = firstThisMonth.AddMonths(-1);
                _inventoryToPicker.Date = firstThisMonth.AddDays(-1);
                break;
            case 2:
                _inventoryFromPicker.Date = today.AddDays(-6);
                _inventoryToPicker.Date = today;
                break;
            default:
                break;
        }
        _updatingInventoryPreset = false;
    }

    private void MarkCustomInventoryRange()
    {
        if (_updatingInventoryPreset)
        {
            return;
        }

        _inventoryPresetPicker.SelectedIndex = 4;
    }

    // ── summary cards ─────────────────────────────────────────────────────
    private View SummaryGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = AppUi.S(10),
            RowSpacing = AppUi.S(10),
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
        var bar = new BoxView { Color = accent, WidthRequest = 4, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Fill };
        var stack = new VerticalStackLayout { Spacing = 5, Children = { new Label { Text = title, TextColor = AppColors.Muted, FontSize = AppUi.S(12) }, value } };
        Grid.SetColumn(stack, 1);
        return Card(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = AppUi.S(10),
            Children = { bar, stack }
        }, AppUi.S(14));
    }

    // ── reusable UI helpers ───────────────────────────────────────────────
    private static View Section(string title, View content)
        => Card(new VerticalStackLayout
        {
            Spacing = AppUi.S(10),
            Children =
            {
                new Label { Text = title, FontSize = AppUi.S(16), FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy },
                content
            }
        }, AppUi.S(14));

    private static Border Card(View content, double padding)
        => new()
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(16) },
            BackgroundColor = AppColors.Card,
            Padding = new Thickness(padding),
            Content = content
        };

    private static Border InputShell(View content)
        => new()
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Padding = new Thickness(AppUi.S(8), 0),
            Content = content
        };

    private Border TodaySummaryBadge()
        => new()
        {
            Stroke = Color.FromArgb("#BFDBFE"),
            StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(999) },
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            Padding = new Thickness(AppUi.S(12), AppUi.S(7)),
            HorizontalOptions = LayoutOptions.Start,
            Content = _todayBadge
        };

    private GraphicsView CreateChartView(IDrawable drawable)
    {
        var chart = new GraphicsView
        {
            Drawable = drawable,
            HeightRequest = AppUi.ChartHeight,
            MinimumHeightRequest = AppUi.ChartHeight,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start
        };
        chart.SizeChanged += (_, _) =>
        {
            if (chart.Width > 0 && chart.Height > 0)
            {
                chart.Invalidate();
            }
        };
        return chart;
    }

    private void InvalidateChartsSoon()
    {
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () =>
        {
            _lineChart.Invalidate();
            _pieChart.Invalidate();
        });
    }

    private DateTime StoreToday()
        => RevenueTime.ToStoreTime(DateTimeOffset.Now, _storeTimezone).Date;

    private static string FriendlyDataError(Exception ex)
    {
        if (ex is HttpRequestException || ex is TaskCanceledException || ex is TimeoutException ||
            ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return "Kết nối cloud tạm thời chậm. Vui lòng kéo xuống để tải lại.";
        }

        return string.IsNullOrWhiteSpace(ex.Message)
            ? "Không đọc được dữ liệu. Vui lòng thử lại."
            : ex.Message;
    }

    // ── data loading (unchanged logic) ───────────────────────────────────
    private async Task LoadAsync(bool fromPull = false)
    {
        if (_loading) return;
        try
        {
            _loading = true;
            _refreshButton.IsEnabled = false;
            _api.ResetCacheStatus();
            UpdateOfflineBanner();

            await EnsureStoreAsync();
            await LoadActiveTabAsync(silent: fromPull, force: true);
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            if (!fromPull)
                await DisplayAlert("Không tải được dữ liệu", ex.Message, "OK");
        }
        finally
        {
            _refreshButton.IsEnabled = true;
            ClearRefreshIndicators();
            _loading = false;
        }
    }

    private async Task AutoRefreshAsync()
    {
        if (_loading || _autoRefreshing)
        {
            return;
        }

        try
        {
            _autoRefreshing = true;
            _api.ResetCacheStatus();
            await EnsureStoreAsync();
            await LoadActiveTabAsync(silent: true, force: true);
            UpdateOfflineBanner();
        }
        catch
        {
            UpdateOfflineBanner();
        }
        finally
        {
            ClearRefreshIndicators();
            _autoRefreshing = false;
        }
    }

    private async Task EnsureStoreAsync()
    {
        if (_stores.Count > 0 && !string.IsNullOrWhiteSpace(_session.StoreId))
        {
            return;
        }

        var stores = await _api.StoresAsync();
        _stores = stores.Stores.Where(s => s.Enabled).ToList();
        if (_stores.Count == 0)
            throw new InvalidOperationException("Tenant chưa có cửa hàng Revenue Cloud đang bật.");

        var selectedStore = _stores.FirstOrDefault(s => s.StoreId == _session.StoreId) ?? _stores[0];
        _session.StoreId = selectedStore.StoreId;
        _storeTimezone = selectedStore.Timezone;
    }

    private async Task LoadActiveTabAsync(bool silent = false, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId))
        {
            return;
        }

        var tab = _activeTab;
        if (!force && _loadedTabs[(int)tab])
        {
            return;
        }

        switch (tab)
        {
            case Tab.Home:
                await LoadReportsAsync(silent);
                break;
            case Tab.Tables:
                await LoadOpenTablesAsync(silent);
                break;
            case Tab.Invoices:
                await LoadInvoicesAsync(silent);
                break;
            case Tab.Inventory:
                await LoadInventoryAsync(silent);
                break;
        }

        _loadedTabs[(int)tab] = true;
    }

    private void ClearRefreshIndicators()
    {
        foreach (var refreshView in _refreshViews)
        {
            refreshView.IsRefreshing = false;
        }
    }

    private async Task LoadReportsAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId)) return;
        try
        {
            var requestedDate = StoreToday();
            var today = await _api.TodayAsync(requestedDate, _session.StoreId);
            _storeTimezone = string.IsNullOrWhiteSpace(today.Timezone) ? _storeTimezone : today.Timezone;
            requestedDate = StoreToday();
            _todayBadge.Text = $"DOANH THU HÔM NAY • {requestedDate:dd/MM/yyyy}";
            _syncStatus.Text = today.LastSyncAt is null
                ? "Chưa có lần đồng bộ cloud"
                : $"Đồng bộ: {RevenueTime.FormatStore(today.LastSyncAt, _storeTimezone, "dd/MM/yyyy HH:mm")}";
            _revenue.Text = RevenueApiClient.Money(today.Summary.Revenue);
            _invoiceCount.Text = today.Summary.InvoiceCount.ToString("N0");
            _avg.Text = RevenueApiClient.Money(today.Summary.AverageInvoiceValue);
            _cancelled.Text = today.Summary.CancelledInvoiceCount.ToString("N0");
            RenderDaily(today.Revenue7Days, today.Revenue7Days);
            InvalidateChartsSoon();
            await LoadRangeAsync(silent: true);
            await LoadHomeInventoryAsync(silent: true);
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            if (!silent)
                await DisplayAlert("Không tải được báo cáo", ex.Message, "OK");
        }
    }

    private static DateTime ResolveLatestDataDate(DateTime requestedDate, TodayReport today, MonthReport month)
    {
        if (today.Summary.InvoiceCount > 0 || today.Summary.Revenue > 0)
        {
            return requestedDate.Date;
        }

        var latest = month.Daily
            .Where(point => point.InvoiceCount > 0 || point.Revenue > 0)
            .Select(point => DateTime.TryParse(point.Date, out var date) ? date.Date : (DateTime?)null)
            .Where(date => date is not null && date.Value <= requestedDate.Date)
            .Select(date => date!.Value)
            .DefaultIfEmpty(requestedDate.Date)
            .Max();

        return latest;
    }

    private async Task LoadRangeAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId)) return;
        var from = PickerDate(_fromPicker);
        var to = PickerDate(_toPicker);
        if (from.Date > to.Date)
        {
            if (!silent)
                await DisplayAlert("Khoảng ngày không hợp lệ", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "OK");
            return;
        }
        try
        {
            _rangeButton.IsEnabled = false;
            var range = await _api.RangeAsync(from, to, _session.StoreId);
            _rangeRevenue.Text = RevenueApiClient.Money(range.Summary.Revenue);
            _rangeInvoices.Text = $"{range.Summary.InvoiceCount:N0} hóa đơn, {range.Summary.CancelledInvoiceCount:N0} hủy";
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            if (!silent)
                await DisplayAlert("Không tải được báo cáo khoảng ngày", ex.Message, "OK");
        }
        finally
        {
            _rangeButton.IsEnabled = true;
        }
    }

    private async Task LoadHomeInventoryAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId)) return;
        var from = PickerDate(_homeInventoryFromPicker);
        var to = PickerDate(_homeInventoryToPicker);
        if (from.Date > to.Date)
        {
            if (!silent)
                await DisplayAlert("Khoảng ngày không hợp lệ", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "OK");
            return;
        }

        try
        {
            _homeInventoryButton.IsEnabled = false;
            var periodInventoryTask = _api.InventoryAsync(from, to, _session.StoreId);
            var currentStockTask = _api.InventoryCurrentStockAsync(_session.StoreId);
            await Task.WhenAll(periodInventoryTask, currentStockTask);
            RenderHomeInventory(periodInventoryTask.Result, currentStockTask.Result);
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            _homeInventoryImportValue.Text = RevenueApiClient.Money(0);
            _homeInventoryExportValue.Text = RevenueApiClient.Money(0);
            _homeInventoryStockValue.Text = RevenueApiClient.Money(0);
            _homeInventoryStatus.Text = FriendlyDataError(ex);
            UpdateOfflineBanner();
            if (!silent)
                await DisplayAlert("Không tải được thống kê kho", ex.Message, "OK");
        }
        finally
        {
            _homeInventoryButton.IsEnabled = true;
        }
    }

    private async Task LoadOpenTablesAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId)) return;
        try
        {
            var openTables = await _api.OpenTablesAsync(_session.StoreId);
            _lastOpenTablesReport = openTables;
            RenderTables(openTables, string.Empty);
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            RenderTables(_lastOpenTablesReport, FriendlyDataError(ex));
            UpdateOfflineBanner();
            if (!silent && _lastOpenTablesReport is null)
                await DisplayAlert("Không tải được bàn đang phục vụ", ex.Message, "OK");
        }
    }

    private async Task LoadInventoryAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId)) return;
        var from = PickerDate(_inventoryFromPicker);
        var to = PickerDate(_inventoryToPicker);
        if (from.Date > to.Date)
        {
            if (!silent)
                await DisplayAlert("Khoảng ngày không hợp lệ", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "OK");
            return;
        }

        try
        {
            _inventoryFilterButton.IsEnabled = false;
            var inventory = await _api.InventoryAsync(from, to, _session.StoreId);
            RenderInventory(inventory);
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            if (!silent)
                await DisplayAlert("Không tải được kho hàng", ex.Message, "OK");
        }
        finally
        {
            _inventoryFilterButton.IsEnabled = true;
        }
    }

    private async Task LoadInvoicesAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_session.StoreId)) return;
        var from = PickerDate(_invoiceFromPicker);
        var to = PickerDate(_invoiceToPicker);
        if (from.Date > to.Date)
        {
            if (!silent)
                await DisplayAlert("Khoảng ngày không hợp lệ", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "OK");
            return;
        }

        try
        {
            _invoiceFilterButton.IsEnabled = false;
            _invoicePrevButton.IsEnabled = false;
            _invoiceNextButton.IsEnabled = false;
            var invoices = await _api.InvoicesAsync(from, to, _session.StoreId, _invoicePage, InvoicePageSize);
            if (_invoicePage > 1 && invoices.Items.Count == 0 && invoices.TotalItems > 0)
            {
                _invoicePage = Math.Max(1, (int)Math.Ceiling(invoices.TotalItems / (double)InvoicePageSize));
                invoices = await _api.InvoicesAsync(from, to, _session.StoreId, _invoicePage, InvoicePageSize);
            }

            RenderInvoices(invoices.Items);
            UpdateInvoicePaging(invoices);
            UpdateOfflineBanner();
        }
        catch (Exception ex)
        {
            UpdateOfflineBanner();
            if (!silent)
                await DisplayAlert("Không tải được hóa đơn", ex.Message, "OK");
        }
        finally
        {
            _invoiceFilterButton.IsEnabled = true;
        }
    }

    // ── render methods (unchanged logic) ──────────────────────────────────
    private void RenderDaily(IReadOnlyList<DailyPoint> points, IReadOnlyList<DailyPoint> monthPoints)
    {
        _dailyPanel.Clear();
        var max = points.Count == 0 ? 1 : Math.Max(1, points.Max(p => p.Revenue));
        _lineDrawable.Points = points.Count > 0 ? points : monthPoints;
        _lineChart.Invalidate();
        foreach (var p in points.TakeLast(7))
            _dailyPanel.Add(BarRow(ShortDate(p.Date), p.Revenue, max, AppColors.Blue));
        if (_dailyPanel.Children.Count == 0)
            _dailyPanel.Add(EmptyLabel("Chưa có dữ liệu."));
    }

    private void RenderPayment(IReadOnlyList<PaymentSlice> slices)
    {
        _paymentPanel.Clear();
        var max = slices.Count == 0 ? 1 : Math.Max(1, slices.Max(p => p.Amount));
        _pieDrawable.Slices = slices;
        _pieChart.Invalidate();
        foreach (var p in slices.Where(s => s.Amount > 0))
            _paymentPanel.Add(BarRow(PaymentLabel(p.Method), p.Amount, max, AppColors.Green));
        if (_paymentPanel.Children.Count == 0)
            _paymentPanel.Add(EmptyLabel("Chưa có dữ liệu thanh toán."));
    }

    private void RenderHomeInventory(InventoryReportResponse report, InventoryReportResponse currentStockReport)
    {
        var rows = report.Items;
        var importValue = rows.Sum(item => item.ImportQty * item.LastImportPrice);
        var exportValue = rows.Sum(item => item.TotalExportQty * item.LastImportPrice);
        var currentStockRows = LatestInventoryRows(currentStockReport.Items);
        var stockValue = currentStockRows.Sum(item => item.ClosingQty * item.LastImportPrice);
        var currentStockCount = currentStockRows.Count(item => item.ClosingQty != 0);


        _homeInventoryImportValue.Text = RevenueApiClient.Money(importValue);
        _homeInventoryExportValue.Text = RevenueApiClient.Money(exportValue);
        _homeInventoryStockValue.Text = RevenueApiClient.Money(stockValue);
        _homeInventoryStatus.Text = rows.Count == 0
            ? $"Chưa có dữ liệu nhập/xuất trong khoảng {ShortDate(report.From)} - {ShortDate(report.To)} • Tồn hiện tại: {currentStockCount:N0} mặt hàng."
            : $"Nhập/xuất {ShortDate(report.From)} - {ShortDate(report.To)} • Tồn hiện tại: {currentStockCount:N0} mặt hàng.";
    }

    private static List<InventoryReportItem> LatestInventoryRows(IEnumerable<InventoryReportItem> rows)
        => rows
            .GroupBy(item => string.IsNullOrWhiteSpace(item.ProductId) ? item.ProductName : item.ProductId)
            .Select(group => group
                .OrderByDescending(item => ParseReportDate(item.BusinessDate))
                .ThenByDescending(item => item.UpdatedAt ?? DateTimeOffset.MinValue)
                .First())
            .ToList();

    private void RenderInventory(InventoryReportResponse report)
    {
        _inventoryPanel.Clear();
        var rows = report.Items
            .OrderBy(row => row.ProductName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.BusinessDate)
            .ToList();

        _inventorySummary.Text = rows.Count == 0
            ? $"Không có phát sinh kho trong khoảng {ShortDate(report.From)} - {ShortDate(report.To)}."
            : $"{rows.Count:N0} dòng kho • {ShortDate(report.From)} - {ShortDate(report.To)}";

        foreach (var item in rows.Take(200))
            _inventoryPanel.Add(InventoryRow(item));

        if (_inventoryPanel.Children.Count == 0)
        {
            _inventoryPanel.Add(EmptyLabel("Chưa có dữ liệu kho."));
        }
    }

    private void RenderTables(OpenTablesReport? report, string error)
    {
        _tablesPanel.Clear();
        if (report is null)
        {
            _tableCountLabel.Text = "–";
            _tableEstimatedLabel.Text = RevenueApiClient.Money(0);
            _tablesPanel.Add(EmptyLabel(string.IsNullOrWhiteSpace(error)
                ? "Chưa có dữ liệu bàn đang phục vụ."
                : error));
            return;
        }

        _tableCountLabel.Text = report.TableCount.ToString("N0");
        _tableEstimatedLabel.Text = RevenueApiClient.Money(report.EstimatedTotal);

        foreach (var table in report.Tables.OrderBy(t => t.OccupiedAt ?? DateTimeOffset.MaxValue).ThenBy(t => t.TableName).Take(100))
        {
            var zone = table.ZoneName;
            var opened = table.OccupiedAt is null
                ? "Chưa rõ giờ mở"
                : $"Mở {RevenueTime.FormatStore(table.OccupiedAt, _storeTimezone, "HH:mm dd/MM")}";
            var subtitle = string.IsNullOrWhiteSpace(zone)
                ? opened
                : $"{zone} • {opened}";

            var row = ListRow(
                $"{table.TableName} — {RevenueApiClient.Money(table.Total)}",
                subtitle,
                showChevron: true);
            var selectedTable = table;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await Navigation.PushAsync(new OpenTableDetailPage(selectedTable, _storeTimezone));
            row.GestureRecognizers.Add(tap);
            _tablesPanel.Add(row);
        }

        if (_tablesPanel.Children.Count == 0)
        {
            _tablesPanel.Add(EmptyLabel("Hiện không có bàn đang phục vụ."));
        }
    }

    private void RenderInvoices(IReadOnlyList<InvoiceListItem> items)
    {
        _invoicePanel.Clear();
        foreach (var item in items.Take(InvoicePageSize))
        {
            var row = ListRow(
                $"{item.TableName} — {RevenueApiClient.Money(item.Total)}",
                $"{ShortDate(item.BusinessDate)}  •  {StatusLabel(item.Status)}",
                showChevron: true);
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowInvoiceDetailAsync(item.InvoiceId);
            row.GestureRecognizers.Add(tap);
            _invoicePanel.Add(row);
        }
        if (_invoicePanel.Children.Count == 0)
            _invoicePanel.Add(EmptyLabel("Chưa có hóa đơn."));
    }

    private void UpdateInvoicePaging(InvoiceListResponse invoices)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(invoices.TotalItems / (double)InvoicePageSize));
        _invoicePage = Math.Clamp(_invoicePage, 1, totalPages);
        var from = PickerDate(_invoiceFromPicker);
        var to = PickerDate(_invoiceToPicker);
        _invoicePageInfo.Text = invoices.TotalItems == 0
            ? $"Không có hóa đơn • {from:dd/MM/yyyy} - {to:dd/MM/yyyy}"
            : $"Trang {_invoicePage}/{totalPages} • {invoices.TotalItems:N0} hóa đơn • {from:dd/MM/yyyy} - {to:dd/MM/yyyy}";
        _invoicePrevButton.IsEnabled = _invoicePage > 1;
        _invoiceNextButton.IsEnabled = _invoicePage < totalPages;
    }

    private async Task ShowInvoiceDetailAsync(string invoiceId)
    {
        try
        {
            var invoice = await _api.InvoiceDetailAsync(invoiceId, _session.StoreId);
            await Navigation.PushAsync(new InvoiceDetailPage(invoice, _storeTimezone));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không tải được chi tiết hóa đơn", ex.Message, "OK");
        }
    }

    // ── bar row (proportional, no fixed width) ────────────────────────────
    private static View BarRow(string label, decimal value, decimal max, Color color)
    {
        var ratio = max <= 0 ? 0 : Math.Clamp((double)(value / max), 0, 1);
        var fill = ratio <= 0 ? 0.001 : ratio;
        var remainder = Math.Max(0.001, 1 - fill);
        var track = new Grid
        {
            BackgroundColor = Color.FromArgb("#E2E8F0"),
            HeightRequest = 8,
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
                new Label { Text = $"{label}: {RevenueApiClient.Money(value)}", TextColor = AppColors.Navy, FontSize = AppUi.S(13) },
                track
            }
        };
    }

    private static Border InventoryRow(InventoryReportItem item)
    {
        var unit = string.IsNullOrWhiteSpace(item.UnitName) ? string.Empty : $" {item.UnitName}";
        var isLow = item.MinStock > 0 && item.ClosingQty <= item.MinStock;

        // Header: tên hàng + ngày
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        header.Add(new Label
        {
            Text = item.ProductName,
            TextColor = AppColors.Navy,
            FontSize = AppUi.S(14),
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation
        }, 0, 0);
        header.Add(new Label
        {
            Text = ShortDate(item.BusinessDate),
            TextColor = AppColors.Muted,
            FontSize = AppUi.S(12),
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);

        // Divider
        var divider = new BoxView
        {
            Color = Color.FromArgb("#E2E8F0"),
            HeightRequest = 1,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, AppUi.S(2), 0, 0)
        };

        // Dòng nhập
        var importRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        importRow.Add(new Label
        {
            Text = $"↓  Nhập  {FormatQty(item.ImportQty)}{unit}",
            TextColor = AppColors.Green,
            FontSize = AppUi.S(13),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        importRow.Add(new Label
        {
            Text = RevenueApiClient.Money(item.LastImportPrice) + "/đvt",
            TextColor = AppColors.Muted,
            FontSize = AppUi.S(12),
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);

        // Dòng xuất
        var exportRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        exportRow.Add(new Label
        {
            Text = $"↑  Xuất bán  {FormatQty(item.SoldQty)}{unit}",
            TextColor = AppColors.Orange,
            FontSize = AppUi.S(13),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        exportRow.Add(new Label
        {
            Text = $"Xuất khác  {FormatQty(item.ManualExportQty)}{unit}",
            TextColor = AppColors.Muted,
            FontSize = AppUi.S(12),
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);

        // Dòng tồn
        var stockRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        stockRow.Add(new Label
        {
            Text = $"■  Tồn  {FormatQty(item.ClosingQty)}{unit}",
            TextColor = AppColors.Blue,
            FontSize = AppUi.S(13),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        if (isLow)
        {
            stockRow.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#FEE2E2"),
                Stroke = Color.FromArgb("#FCA5A5"),
                StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(6) },
                Padding = new Thickness(AppUi.S(6), AppUi.S(2)),
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = $"⚠ ≤ {FormatQty(item.MinStock)}{unit}",
                    TextColor = AppColors.Red,
                    FontSize = AppUi.S(11),
                    FontAttributes = FontAttributes.Bold
                }
            }, 1, 0);
        }

        return new Border
        {
            Stroke = Color.FromArgb(isLow ? "#FDBA74" : "#E2E8F0"),
            StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(12) },
            BackgroundColor = isLow ? Color.FromArgb("#FFFBEB") : Colors.White,
            Padding = new Thickness(AppUi.S(14), AppUi.S(12)),
            Content = new VerticalStackLayout
            {
                Spacing = AppUi.S(7),
                Children = { header, divider, importRow, exportRow, stockRow }
            }
        };
    }

    private static Border ListRow(string title, string subtitle, bool showChevron)
    {
        var subtitleLabel = new Label { Text = subtitle, TextColor = AppColors.Muted, FontSize = AppUi.S(12) };
        var chevron = new Label { Text = "›", TextColor = AppColors.Muted, FontSize = AppUi.S(22), VerticalTextAlignment = TextAlignment.Center, IsVisible = showChevron };
        Grid.SetColumn(chevron, 1);

        return new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(12) },
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Padding = new Thickness(AppUi.S(14), AppUi.S(13)),
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 3,
                        Children =
                        {
                            new Label { Text = title, TextColor = AppColors.Navy, FontSize = AppUi.S(14), FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation },
                            subtitleLabel
                        }
                    },
                    chevron
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

    private async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        await Navigation.PushAsync(_services.GetRequiredService<LoginPage>());
        Navigation.RemovePage(this);
    }

    private static Label EmptyLabel(string text)
        => new() { Text = text, TextColor = AppColors.Muted, FontSize = AppUi.S(13) };

    private static Label ValueLabel(double size, Color color)
        => new() { FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = color, LineBreakMode = LineBreakMode.NoWrap };

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

    private static DateTime ParseReportDate(string date)
        => DateTime.TryParse(date, out var value) ? value.Date : DateTime.MinValue;

    private static string FormatQty(decimal value)
        => value % 1 == 0
            ? value.ToString("N0")
            : value.ToString("N2").TrimEnd('0').TrimEnd('.', ',');

    private static DateTime PickerDate(MauiDatePicker picker)
        => picker.Date ?? DateTime.Today;
}

// ── chart drawables (unchanged) ───────────────────────────────────────────
internal sealed class RevenueLineDrawable : IDrawable
{
    public IReadOnlyList<DailyPoint> Points { get; set; } = [];

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0) return;
        canvas.FillColor = Color.FromArgb("#F8FAFC");
        canvas.FillRoundedRectangle(dirtyRect, 14);
        if (Points.Count == 0) { DrawEmpty(canvas, dirtyRect, "Chưa có dữ liệu"); return; }

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
            if (i > 0) canvas.DrawLine(last.X, last.Y, current.X, current.Y);
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
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0) return;
        canvas.FillColor = Color.FromArgb("#F8FAFC");
        canvas.FillRoundedRectangle(dirtyRect, 14);
        var rows = Slices.Where(s => s.Amount > 0).ToList();
        var total = rows.Sum(s => s.Amount);
        if (total <= 0) { canvas.FontColor = AppColors.Muted; canvas.DrawString("Chưa có dữ liệu", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center); return; }

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
            canvas.DrawString(
                $"{DashboardPage.PaymentLabelForChart(rows[i].Method)} {RevenueApiClient.Money(rows[i].Amount)}",
                legendX + 18, legendY + i * 24 - 2, dirtyRect.Right - legendX - 22, 18,
                HorizontalAlignment.Left, VerticalAlignment.Top);
        }
    }
}
