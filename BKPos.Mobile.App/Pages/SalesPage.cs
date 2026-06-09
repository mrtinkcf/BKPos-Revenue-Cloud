using System.Collections.ObjectModel;
using BKPos.Mobile.App.Services;
using Microsoft.Maui.ApplicationModel;

namespace BKPos.Mobile.App.Pages;

public sealed class SalesPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly MobileLoginSettings _loginSettings;
    private readonly MobilePrintSettings _printSettings;
    private readonly ObservableCollection<TableCard> _tables = [];
    private readonly ObservableCollection<ProductDto> _products = [];
    private readonly ObservableCollection<OrderLineCard> _lines = [];
    private readonly CollectionView _tableList;
    private readonly CollectionView _productList;
    private readonly CollectionView _lineList;
    private readonly Entry _tableSearch = new()
    {
        Placeholder = "Tìm bàn/khu",
        BackgroundColor = Colors.Transparent,
        TextColor = AppUi.Ink,
        PlaceholderColor = AppUi.Muted,
        ReturnType = ReturnType.Done,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };
    private readonly Entry _search = new()
    {
        Placeholder = "Tìm nhanh món",
        BackgroundColor = AppUi.SurfaceAlt,
        TextColor = AppUi.Ink,
        PlaceholderColor = AppUi.Muted,
        ReturnType = ReturnType.Done,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };
    private readonly Label _tableTitle = new() { Text = "Chưa chọn bàn", TextColor = AppUi.Ink, FontSize = AppUi.S(15), FontAttributes = FontAttributes.Bold };
    private readonly Label _total = new() { Text = "0 đ", TextColor = AppUi.Navy, FontSize = AppUi.S(18), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.End };
    private readonly Label _status = new() { Text = "Sẵn sàng", TextColor = AppUi.Muted, FontSize = AppUi.S(12) };
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Blue, IsVisible = false };
    private List<ZoneDto> _zones = [];
    private List<TableCard> _allTables = [];
    private List<ProductDto> _allProducts = [];
    private TableDto? _currentTable;
    private OrderDto? _currentOrder;
    private readonly Dictionary<string, CancellationTokenSource> _autoKitchenPrintTimers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _autoKitchenLock = new();
    private CancellationTokenSource? _tableRefreshDebounce;
    private bool _isBusy;

    public SalesPage(ApiClient api, MobileLoginSettings loginSettings, MobilePrintSettings printSettings)
    {
        _api = api;
        _loginSettings = loginSettings;
        _printSettings = printSettings;
        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = AppUi.Background;
        _tableList = BuildTableList();
        _productList = BuildProductList();
        _lineList = BuildLineList();
        Content = AppKeyboardHost.Wrap(BuildContent());
    }

    public SalesPage(ApiClient api, MobileLoginSettings loginSettings)
        : this(api, loginSettings, new MobilePrintSettings())
    {
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_allProducts.Count == 0)
        {
            await LoadInitialAsync();
        }
    }

    private View BuildContent()
    {
        _tableSearch.TextChanged += (_, _) => ApplyTableFilter();
        _search.TextChanged += (_, _) => ApplyProductFilter();

        var logoutDrawer = BuildLogoutDrawer();

        var body = new Grid
        {
            Padding = new Thickness(AppUi.S(6)),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(0.50, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.45, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.37, GridUnitType.Star))
            },
            ColumnSpacing = 6
        };
        body.Add(BuildTablesPanel(), 0, 0);
        body.Add(BuildOrderPanel(), 1, 0);
        body.Add(BuildProductsPanel(), 2, 0);

        _busy.HorizontalOptions = LayoutOptions.End;
        _busy.VerticalOptions = LayoutOptions.Start;
        _busy.Margin = new Thickness(0, 42, 18, 0);
        _busy.ZIndex = 11;

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star) }
        };
        root.Add(body, 0, 0);
        root.Add(_busy, 0, 0);
        root.Add(logoutDrawer, 0, 0);
        return root;
    }

    private View BuildLogoutDrawer()
    {
#if IOS
        var toggleW = AppUi.S(20);
        var logoutW = AppUi.S(72);
        var h = AppUi.S(30);
#else
        var toggleW = AppUi.S(32);
        var logoutW = AppUi.S(108);
        var h = AppUi.S(40);
#endif

        var toggleArrow = new Label
        {
            Text = "‹",
            TextColor = Colors.White,
            FontSize = AppUi.S(18),
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var toggle = new Border
        {
            BackgroundColor = AppUi.Navy2,
            StrokeThickness = 0,
            WidthRequest = toggleW,
            HeightRequest = h,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(10, 0, 0, 10)
            },
            Content = toggleArrow
        };

        var logoutText = new Label
        {
            Text = "Đăng xuất",
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = AppUi.S(10),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var logout = new Border
        {
            BackgroundColor = Color.FromArgb("#B91C1C"),
            StrokeThickness = 0,
            IsVisible = false,
            HeightRequest = h,
            WidthRequest = logoutW,
            Padding = new Thickness(AppUi.S(4), 0),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 0 },
            Content = logoutText
        };

        var drawer = new Grid
        {
            WidthRequest = toggleW + logoutW,
            HeightRequest = h,
            TranslationX = logoutW,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, AppUi.S(64), 0, 0),
            ZIndex = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(toggleW)),
                new ColumnDefinition(new GridLength(logoutW))
            },
            ColumnSpacing = 0
        };

        var isOpen = false;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            isOpen = !isOpen;
            toggleArrow.Text = isOpen ? "›" : "‹";
            if (isOpen)
            {
                logout.IsVisible = true;
            }

            await drawer.TranslateTo(isOpen ? 0 : logoutW, 0, 160, Easing.CubicOut);
            if (!isOpen)
            {
                logout.IsVisible = false;
            }
        };
        toggle.GestureRecognizers.Add(tap);
        var logoutTap = new TapGestureRecognizer();
        logoutTap.Tapped += async (_, _) => await LogoutAsync();
        logout.GestureRecognizers.Add(logoutTap);

        drawer.Add(toggle, 0, 0);
        drawer.Add(logout, 1, 0);
        return drawer;
    }

    private View BuildProductsPanel()
    {
        _search.HeightRequest = AppUi.S(36);
        _search.FontSize = AppUi.S(12);
        _search.Margin = 0;
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 8
        };
        grid.Add(SearchBox(_search), 0, 0);
        grid.Add(_productList, 0, 1);
        return AppUi.CardView(grid, 8);
    }

    private View BuildOrderPanel()
    {
        // Border chip — không bị Android clamp minimum height như Button
        Border Chip(string text, Color bg, Color fg, Func<Task> onTap)
        {
            var lbl = new Label
            {
                Text = text, TextColor = fg, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(9),
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            };
            var b = new Border
            {
                BackgroundColor = bg, StrokeThickness = 0, HeightRequest = AppUi.S(20),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 5 },
                Padding = new Thickness(AppUi.S(7), 0), Content = lbl
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await onTap();
            b.GestureRecognizers.Add(tap);
            return b;
        }

        // Border action — thay CompactButton để tránh minimum height Android
        Border ActionChip(string text, Color bg, Color fg, Func<Task> onTap)
        {
            var lbl = new Label
            {
                Text = text, TextColor = fg, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(12),
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            };
            var b = new Border
            {
                BackgroundColor = bg, StrokeThickness = 0, HeightRequest = AppUi.S(32),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(AppUi.S(10), 0), Content = lbl
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await onTap();
            b.GestureRecognizers.Add(tap);
            return b;
        }

        var printKitchen = ActionChip("In chế biến", AppUi.Blue, Colors.White, PrintKitchenAsync);
        var pay = ActionChip("✓ Thanh toán", AppUi.Red, Colors.White, PayAndPrintAsync);

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        actions.Add(printKitchen, 0, 0);
        actions.Add(pay, 1, 0);

        var totalGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Children =
            {
                new Label { Text = "Tổng", TextColor = AppUi.Navy, VerticalTextAlignment = TextAlignment.Center, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(14) },
                _total
            }
        };
        Grid.SetColumn(_total, 1);
        var totalRow = new Border
        {
            Stroke = AppUi.Blue,
            StrokeThickness = 1.8,
            StrokeDashArray = [5, 3],
            BackgroundColor = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(AppUi.S(10), AppUi.S(5)),
            Content = totalGrid
        };

        // Text rút gọn để tránh overflow trên màn hình hẹp
        var btnTransfer = Chip("Chuyển", AppUi.BlueSoft, AppUi.Blue, TransferTableAsync);
        var btnMerge    = Chip("Gộp",    AppUi.BlueSoft, AppUi.Blue, MergeTableAsync);
        var btnSplit    = Chip("Tách",   AppUi.BlueSoft, AppUi.Blue, SplitTableAsync);
        var btnCancel   = Chip("Hủy",    Color.FromArgb("#DC2626"), Colors.White, CancelCurrentTableAsync);

        _tableTitle.LineBreakMode = LineBreakMode.TailTruncation;
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Add(_tableTitle, 0, 0);
        headerRow.Add(new HorizontalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children = { btnTransfer, btnMerge, btnSplit, btnCancel }
        }, 1, 0);

        var panel = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 8
        };
        panel.Add(headerRow, 0, 0);
        panel.Add(_lineList, 0, 1);
        panel.Add(totalRow, 0, 2);
        panel.Add(actions, 0, 3);
        return AppUi.CardView(panel, 8);
    }

    private View BuildTablesPanel()
    {
        _tableSearch.HeightRequest = AppUi.S(36);
        _tableSearch.FontSize = AppUi.S(12);
        _tableSearch.Margin = 0;
        var panel = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 8
        };
        panel.Add(SearchBox(_tableSearch), 0, 0);
        panel.Add(_tableList, 0, 1);
        return AppUi.CardView(panel, 8);
    }

    private CollectionView BuildProductList()
    {
        var list = new CollectionView
        {
            ItemsSource = _products,
#if IOS
            SelectionMode = SelectionMode.None,
#else
            SelectionMode = SelectionMode.Single,
#endif
            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical)
            {
                HorizontalItemSpacing = 8,
                VerticalItemSpacing = 8
            },
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { TextColor = AppUi.Ink, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(13), LineBreakMode = LineBreakMode.TailTruncation };
                name.SetBinding(Label.TextProperty, nameof(ProductDto.Name));
                var price = new Label { TextColor = AppUi.Blue, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(13) };
                price.SetBinding(Label.TextProperty, new Binding(nameof(ProductDto.Price), stringFormat: "{0:N0}đ"));
                var unit = new Label { TextColor = AppUi.Muted, FontSize = AppUi.S(10) };
                unit.SetBinding(Label.TextProperty, nameof(ProductDto.UnitName));
                var card = AppUi.CardView(new VerticalStackLayout { Spacing = 2, Children = { name, unit, price } }, 8);
#if IOS
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (sender, _) =>
                {
                    if (sender is BindableObject bindable && bindable.BindingContext is ProductDto product)
                    {
                        await MarkTappedAsync(card);
                        await AddProductAsync(product);
                    }
                };
                card.GestureRecognizers.Add(tap);
#endif
                return card;
            })
        };
#if !IOS
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is ProductDto product)
            {
                list.SelectedItem = null;
                await AddProductAsync(product);
            }
        };
#endif
        return list;
    }

    private CollectionView BuildLineList()
    {
        var list = new CollectionView
        {
            ItemsSource = _lines,
#if IOS
            SelectionMode = SelectionMode.None,
#else
            SelectionMode = SelectionMode.Single,
#endif
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { TextColor = AppUi.Ink, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(13), LineBreakMode = LineBreakMode.TailTruncation };
                name.SetBinding(Label.TextProperty, nameof(OrderLineCard.ProductName));
                var qty = new Label { TextColor = AppUi.Orange, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(12) };
                qty.SetBinding(Label.TextProperty, nameof(OrderLineCard.QuantityText));
                var price = new Label { TextColor = AppUi.Muted, FontSize = AppUi.S(11) };
                price.SetBinding(Label.TextProperty, nameof(OrderLineCard.PricePart));
                var kitchenBadgeText = new Label
                {
                    TextColor = Colors.White,
                    FontSize = AppUi.S(9),
                    FontAttributes = FontAttributes.Bold,
                    VerticalTextAlignment = TextAlignment.Center
                };
                kitchenBadgeText.SetBinding(Label.TextProperty, nameof(OrderLineCard.KitchenPrintStatus));
                var kitchenBadge = new Border
                {
                    StrokeThickness = 0,
                    Padding = new Thickness(AppUi.S(6), AppUi.S(2)),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                    Content = kitchenBadgeText,
                    VerticalOptions = LayoutOptions.Center
                };
                kitchenBadge.SetBinding(Border.IsVisibleProperty, nameof(OrderLineCard.HasKitchenPrint));
                kitchenBadge.SetBinding(Border.BackgroundColorProperty, nameof(OrderLineCard.KitchenPrintStatusColor));
                var summary = new HorizontalStackLayout { Spacing = 4, Children = { qty, price, kitchenBadge } };
                var note = new Label { TextColor = AppUi.Warning, FontSize = AppUi.S(10), LineBreakMode = LineBreakMode.TailTruncation };
                note.SetBinding(Label.TextProperty, nameof(OrderLineCard.NoteDisplay));
                var total = new Label { TextColor = AppUi.Blue, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(13), HorizontalTextAlignment = TextAlignment.End };
                total.SetBinding(Label.TextProperty, nameof(OrderLineCard.LineTotal));
                var remove = new Button
                {
                    Text = "x",
                    BackgroundColor = Colors.Transparent,
                    TextColor = AppUi.Red,
                    BorderColor = AppUi.Red,
                    BorderWidth = 1,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = AppUi.S(12),
                    WidthRequest = AppUi.S(28),
                    HeightRequest = AppUi.S(28),
                    MinimumWidthRequest = AppUi.S(28),
                    MinimumHeightRequest = AppUi.S(28),
                    CornerRadius = (int)AppUi.S(14),
                    Padding = 0
                };
                remove.Clicked += async (sender, _) =>
                {
                    if (sender is BindableObject bindable && bindable.BindingContext is OrderLineCard selectedLine)
                    {
                        await DeleteLineQuickAsync(selectedLine);
                    }
                };
                var right = new VerticalStackLayout
                {
                    Spacing = 5,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { remove, total }
                };

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    RowDefinitions =
                    {
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto)
                    }
                };
                grid.Add(name, 0, 0);
                grid.Add(summary, 0, 1);
                grid.Add(note, 0, 2);
                grid.Add(right, 1, 0);
                Grid.SetRowSpan(right, 3);
                var card = AppUi.CardView(grid, 7);
#if IOS
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (sender, _) =>
                {
                    if (sender is BindableObject bindable && bindable.BindingContext is OrderLineCard line)
                    {
                        await MarkTappedAsync(card);
                        await EditLineAsync(line);
                    }
                };
                card.GestureRecognizers.Add(tap);
#endif
                return card;
            })
        };
#if !IOS
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is OrderLineCard line)
            {
                list.SelectedItem = null;
                await EditLineAsync(line);
            }
        };
#endif
        return list;
    }

    private CollectionView BuildTableList()
    {
        var list = new CollectionView
        {
            ItemsSource = _tables,
#if IOS
            SelectionMode = SelectionMode.None,
#else
            SelectionMode = SelectionMode.Single,
#endif
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { TextColor = AppUi.Ink, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(12), LineBreakMode = LineBreakMode.TailTruncation };
                name.SetBinding(Label.TextProperty, nameof(TableCard.Name));
                var status = new Label { FontSize = AppUi.S(10), FontAttributes = FontAttributes.Bold };
                status.SetBinding(Label.TextProperty, nameof(TableCard.Status));
                status.SetBinding(Label.TextColorProperty, nameof(TableCard.StatusColor));
                var total = new Label { TextColor = AppUi.Muted, FontSize = AppUi.S(10) };
                total.SetBinding(Label.TextProperty, nameof(TableCard.Total));
                var card = new Border
                {
                    Padding = AppUi.S(5),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Content = new VerticalStackLayout { Spacing = 1, Children = { name, status, total } }
                };
                card.SetBinding(Border.BackgroundColorProperty, nameof(TableCard.Background));
                card.SetBinding(Border.StrokeProperty, nameof(TableCard.BorderColor));
                card.SetBinding(Border.StrokeThicknessProperty, nameof(TableCard.BorderThickness));
#if IOS
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (sender, _) =>
                {
                    if (sender is BindableObject bindable && bindable.BindingContext is TableCard table)
                    {
                        await MarkTappedAsync(card);
                        await OpenTableAsync(table.Source);
                    }
                };
                card.GestureRecognizers.Add(tap);
#endif
                return card;
            })
        };
#if !IOS
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is TableCard table)
            {
                list.SelectedItem = null;
                await OpenTableAsync(table.Source);
            }
        };
#endif
        return list;
    }

    private static Button CompactButton(string text, Color background, Color textColor) => new()
    {
        Text = text,
        FontAttributes = FontAttributes.Bold,
        BackgroundColor = background,
        TextColor = textColor,
        CornerRadius = 12,
        HeightRequest = AppUi.S(34),
        FontSize = AppUi.S(11),
        Padding = new Thickness(AppUi.S(10), 0)
    };

    private static Border SearchBox(Entry entry) => new()
    {
        BackgroundColor = AppUi.SurfaceAlt,
        Stroke = AppUi.Border,
        StrokeThickness = 1,
        Padding = new Thickness(10, 0),
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        Content = entry
    };

    private static async Task MarkTappedAsync(Border card)
    {
#if IOS
        // Do not mutate bound color/stroke values here. iOS recycles CollectionView cells,
        // and restoring old colors after an async animation can leak state into another item.
        await card.ScaleTo(0.98, 55, Easing.CubicOut);
        await card.ScaleTo(1, 85, Easing.CubicOut);
#else
        await Task.CompletedTask;
#endif
    }

#if IOS
    private static void ToggleSplitLineSelection(
        OrderLineCard line,
        Border card,
        Label check,
        HashSet<string> selectedLineIds)
    {
        var selected = selectedLineIds.Contains(line.Source.Id);
        if (selected)
        {
            selectedLineIds.Remove(line.Source.Id);
            check.IsVisible = false;
            card.Stroke = AppUi.Border;
            card.StrokeThickness = 1;
            card.BackgroundColor = AppUi.Surface;
            return;
        }

        selectedLineIds.Add(line.Source.Id);
        check.IsVisible = true;
        card.Stroke = AppUi.Blue;
        card.StrokeThickness = 2;
        card.BackgroundColor = AppUi.BlueSoft;
    }
#endif

    private async Task LoadInitialAsync(bool force = false)
    {
        await RunAsync(async () =>
        {
            if (force || _zones.Count == 0)
            {
                _zones = (await _api.GetZonesAsync()).ToList();
            }

            if (force || _allProducts.Count == 0)
            {
                _allProducts = (await _api.GetProductsAsync()).ToList();
                ApplyProductFilter();
            }

            await LoadTablesAsync();
        }, "Đã tải dữ liệu bán hàng.");
    }

    private async Task LoadTablesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await _api.GetTablesAsync(cancellationToken: cancellationToken);
        _allTables = tables.Select(table => new TableCard(table)).ToList();
        ApplyTableFilter();
    }

    private void ApplyMutationResponse(MutationResponseDto mutation)
    {
        if (mutation.Order is not null)
        {
            _currentOrder = mutation.Order;
        }
        else if (_currentOrder is not null
                 && string.Equals(_currentOrder.OrderId, mutation.OrderId, StringComparison.OrdinalIgnoreCase))
        {
            _currentOrder = _currentOrder with
            {
                Total = mutation.Total,
                ModifiedAt = mutation.ModifiedAt
            };
        }

        UpdateCurrentTableSnapshot(mutation);
        ApplyOrder();
        ApplyTableFilter();
        ScheduleTablesRefresh();
    }

    private void UpdateCurrentTableSnapshot(MutationResponseDto mutation)
    {
        if (_currentTable is null)
        {
            return;
        }

        var orderId = _currentOrder?.OrderId ?? mutation.OrderId;
        _currentTable = _currentTable with
        {
            HasOpenOrder = true,
            OrderId = orderId,
            OccupiedAt = _currentTable.OccupiedAt ?? _currentOrder?.CreatedAt,
            Total = mutation.Total
        };

        for (var index = 0; index < _allTables.Count; index++)
        {
            if (string.Equals(_allTables[index].Source.TableId, _currentTable.TableId, StringComparison.OrdinalIgnoreCase))
            {
                _allTables[index] = new TableCard(_currentTable);
                break;
            }
        }
    }

    private void ScheduleTablesRefresh()
    {
        _tableRefreshDebounce?.Cancel();

        var cts = new CancellationTokenSource();
        _tableRefreshDebounce = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                var tables = await _api.GetTablesAsync(cancellationToken: cts.Token);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    _allTables = tables.Select(table => new TableCard(table)).ToList();
                    ApplyTableFilter();
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // Background table refresh must not block ordering.
            }
            finally
            {
                if (ReferenceEquals(_tableRefreshDebounce, cts))
                {
                    _tableRefreshDebounce = null;
                }

                cts.Dispose();
            }
        });
    }

    private void CancelPendingTableRefresh()
    {
        _tableRefreshDebounce?.Cancel();
        _tableRefreshDebounce = null;
    }

    private void ApplyTableFilter()
    {
        var keyword = (_tableSearch.Text ?? string.Empty).Trim();
        var filtered = _allTables
            .Where(table =>
                string.IsNullOrWhiteSpace(keyword)
                || AppUi.ContainsSearch(table.Name, keyword)
                || AppUi.ContainsSearch(table.Status, keyword)
                || AppUi.ContainsSearch(table.Source.ZoneId, keyword)
                || AppUi.ContainsSearch(GetZoneName(table.Source.ZoneId), keyword))
            .OrderByDescending(table => table.Source.HasOpenOrder)
            .ThenBy(table => table.Source.ZoneId, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(table => table.Name, StringComparer.CurrentCultureIgnoreCase);

        _tables.Clear();
        foreach (var table in filtered)
        {
            _tables.Add(new TableCard(table.Source, IsCurrentTable(table.Source.TableId)));
        }
    }

    private bool IsCurrentTable(string tableId)
        => _currentTable is not null
           && string.Equals(_currentTable.TableId, tableId, StringComparison.OrdinalIgnoreCase);

    private void ApplyProductFilter()
    {
        var keyword = (_search.Text ?? string.Empty).Trim();
        var filtered = _allProducts.Where(product =>
            string.IsNullOrWhiteSpace(keyword)
            || AppUi.ContainsSearch(product.Name, keyword)
            || AppUi.ContainsSearch(product.CategoryName, keyword));

        _products.Clear();
        foreach (var product in filtered)
        {
            _products.Add(product);
        }
    }

    private string GetZoneName(string zoneId)
        => _zones.FirstOrDefault(zone => string.Equals(zone.ExternalId, zoneId, StringComparison.OrdinalIgnoreCase))?.Name
           ?? string.Empty;

    private async Task OpenTableAsync(TableDto table)
    {
        await RunAsync(async () =>
        {
            _currentTable = table;
            var opened = await _api.OpenTableAsync(table.TableId);
            _currentOrder = opened.Order ?? await _api.GetOrderAsync(opened.OrderId);

            await LoadTablesAsync();
            _currentTable = _allTables.FirstOrDefault(item =>
                                string.Equals(item.Source.TableId, table.TableId, StringComparison.OrdinalIgnoreCase))
                            ?.Source
                            ?? table;
            ApplyOrder();
            ScheduleAutoKitchenPrintIfNeeded();
        }, $"Đã mở {table.TableName}.");
    }

    private async Task AddProductAsync(ProductDto product)
    {
        if (_currentOrder is null)
        {
            await DisplayAlert("Chưa chọn bàn", "Vui lòng chọn bàn trước khi thêm món.", "OK");
            return;
        }

        await RunAsync(async () =>
        {
            var mutation = await _api.AddLineAsync(_currentOrder.OrderId, product, 1);
            ApplyMutationResponse(mutation);
            ScheduleAutoKitchenPrintIfNeeded();
        }, $"Đã thêm {product.Name}.");
    }

    private async Task EditLineAsync(OrderLineCard line)
    {
        if (_currentOrder is null)
        {
            return;
        }

        await Navigation.PushModalAsync(new OrderLineEditPage(
            line,
            async (quantity, note) => await SaveLineAsync(line, quantity, note),
            async () => await DeleteLineAsync(line)));
    }

    private async Task SaveLineAsync(OrderLineCard line, int quantity, string note)
    {
        if (_currentOrder is null)
        {
            return;
        }

        var mutation = await _api.UpdateLineAsync(_currentOrder.OrderId, line.Source.Id, quantity, note);
        ApplyMutationResponse(mutation);
        ScheduleAutoKitchenPrintIfNeeded();
        _status.Text = "Đã cập nhật món.";
    }

    private async Task DeleteLineAsync(OrderLineCard line)
    {
        if (_currentOrder is null)
        {
            return;
        }

        var mutation = await _api.RemoveLineAsync(_currentOrder.OrderId, line.Source.Id);
        ApplyMutationResponse(mutation);
        ScheduleAutoKitchenPrintIfNeeded();
        _status.Text = "Đã xóa món.";
    }

    private async Task DeleteLineQuickAsync(OrderLineCard line)
    {
        if (_currentOrder is null)
        {
            return;
        }

        var confirm = await DisplayAlert(
            "Hủy món",
            $"Hủy {line.ProductName} khỏi {_tableTitle.Text}?",
            "Hủy món",
            "Không");
        if (!confirm)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var mutation = await _api.RemoveLineAsync(_currentOrder.OrderId, line.Source.Id);
            ApplyMutationResponse(mutation);
            ScheduleAutoKitchenPrintIfNeeded();
        }, "Đã hủy món.", "Hủy món thất bại");
    }

    private async Task RefreshCurrentOrderAsync()
    {
        if (_currentOrder is not null)
        {
            _currentOrder = await _api.GetOrderAsync(_currentOrder.OrderId);
        }
    }

    private async Task<string?> TryRefreshCurrentOrderAndTablesAsync()
    {
        try
        {
            await RefreshCurrentOrderAsync();
            ApplyOrder();
            await LoadTablesAsync();
            return null;
        }
        catch (Exception ex)
        {
            var message = AppUi.ToVietnameseError(ex);
            _status.Text = "Đã xử lý in, nhưng chưa tải lại được dữ liệu: " + message;
            return message;
        }
    }

    private async Task<string?> TryLoadTablesAfterPrintAsync()
    {
        try
        {
            await LoadTablesAsync();
            return null;
        }
        catch (Exception ex)
        {
            var message = AppUi.ToVietnameseError(ex);
            _status.Text = "Đã xử lý in, nhưng chưa tải lại được danh sách bàn: " + message;
            return message;
        }
    }

    private static string WithRefreshWarning(string message, string? refreshWarning)
        => string.IsNullOrWhiteSpace(refreshWarning)
            ? message
            : $"{message}\n\nLưu ý: đã gửi lệnh in, nhưng app chưa tải lại được dữ liệu mới. Bấm bàn hoặc tải lại để cập nhật.";

    private async Task PrintKitchenAsync()
    {
        if (_currentOrder is null)
        {
            await DisplayAlert("Chưa chọn bàn", "Vui lòng chọn bàn trước khi in chế biến.", "OK");
            return;
        }

        await RunAsync(async () =>
        {
            await RefreshCurrentOrderAsync();
            ApplyOrder();
            var orderId = _currentOrder.OrderId;
            var result = await _api.PrintOrderAsync(orderId, "kitchen");
            var refreshWarning = await TryRefreshCurrentOrderAndTablesAsync();

            if (result.Printed)
            {
                CancelAutoKitchenTimer(orderId);
                await DisplayAlert(
                    "In chế biến thành công",
                    WithRefreshWarning("Đã in phiếu bar/bếp cho các món chưa in.", refreshWarning),
                    "OK");
                return;
            }

            var message = result.Deduplicated
                ? "Lệnh in bị chặn để tránh in trùng. Vui lòng thử lại sau."
                : "Không có món mới cần in bar/bếp hoặc cấu hình in không khớp loại món.";
            await DisplayAlert("Không in được", WithRefreshWarning(message, refreshWarning), "OK");
        }, "Đã xử lý lệnh in chế biến.", "In chế biến thất bại");
    }

    private async Task TransferTableAsync()
    {
        if (!await HasCurrentTableOrderAsync("chuyển bàn", "Bàn chưa có món để chuyển."))
        {
            return;
        }

        var target = await SelectTargetTableAsync(
            "Chuyển bàn",
            table => !table.HasOpenOrder,
            "Không có bàn trống phù hợp để chuyển.");
        if (target is null || _currentOrder is null)
        {
            return;
        }

        var confirm = await DisplayAlert(
            "Chuyển bàn",
            $"Chuyển toàn bộ món từ {_tableTitle.Text} sang {target.TableName}?",
            "Chuyển",
            "Hủy");
        if (!confirm)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var sourceOrderId = _currentOrder!.OrderId;
            CancelAutoKitchenTimer(sourceOrderId);
            var result = await _api.TransferOrderAsync(sourceOrderId, target.TableId);
            await FinalizeTableMutationAsync(target.TableId, result.OrderId);
            await DisplayAlert("Đã chuyển bàn", $"Đã chuyển đơn sang {target.TableName}.", "OK");
        }, "Đã chuyển bàn.", "Chuyển bàn thất bại");
    }

    private async Task MergeTableAsync()
    {
        if (!await HasCurrentTableOrderAsync("gộp bàn", "Bàn chưa có món để gộp."))
        {
            return;
        }

        var target = await SelectTargetTableAsync(
            "Gộp bàn",
            table => table.HasOpenOrder && !string.IsNullOrWhiteSpace(table.OrderId),
            "Không có bàn đang phục vụ phù hợp để gộp.");
        if (target is null || _currentOrder is null)
        {
            return;
        }

        var confirm = await DisplayAlert(
            "Gộp bàn",
            $"Gộp toàn bộ món từ {_tableTitle.Text} sang {target.TableName}?",
            "Gộp",
            "Hủy");
        if (!confirm)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var sourceOrderId = _currentOrder!.OrderId;
            CancelAutoKitchenTimer(sourceOrderId);
            var result = await _api.MergeOrderAsync(sourceOrderId, target.OrderId!);
            await FinalizeTableMutationAsync(target.TableId, result.OrderId);
            await DisplayAlert("Đã gộp bàn", $"Đã gộp đơn sang {target.TableName}.", "OK");
        }, "Đã gộp bàn.", "Gộp bàn thất bại");
    }

    private async Task SplitTableAsync()
    {
        if (!await HasCurrentTableOrderAsync("tách bàn", "Bàn chưa có món để tách."))
        {
            return;
        }

        var selectedLines = await SelectSplitLinesAsync();
        if (selectedLines is null || selectedLines.Count == 0)
        {
            return;
        }

        var target = await SelectTargetTableAsync(
            "Tách bàn",
            _ => true,
            "Không có bàn đích phù hợp để tách.");
        if (target is null || _currentOrder is null)
        {
            return;
        }

        var confirm = await DisplayAlert(
            "Tách bàn",
            $"Tách {selectedLines.Count} dòng món sang {target.TableName}?",
            "Tách",
            "Hủy");
        if (!confirm)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var sourceOrderId = _currentOrder!.OrderId;
            CancelAutoKitchenTimer(sourceOrderId);
            var result = await _api.SplitOrderAsync(
                sourceOrderId,
                target.TableId,
                selectedLines.Select(line => line.Source.Id).ToArray());
            await FinalizeTableMutationAsync(target.TableId, result.OrderId);
            await DisplayAlert("Đã tách bàn", $"Đã tách món sang {target.TableName}.", "OK");
        }, "Đã tách bàn.", "Tách bàn thất bại");
    }

    private async Task CancelCurrentTableAsync()
    {
        if (_currentTable is null)
        {
            await DisplayAlert("Chưa chọn bàn", "Vui lòng chọn bàn cần hủy.", "OK");
            return;
        }

        if (_currentOrder is null)
        {
            await DisplayAlert("Hủy bàn", "Bàn chưa có đơn đang mở.", "OK");
            return;
        }

        try
        {
            await RefreshCurrentOrderAsync();
            ApplyOrder();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không kiểm tra được bàn", AppUi.ToVietnameseError(ex), "OK");
            return;
        }

        if (_currentOrder is null)
        {
            await DisplayAlert("Hủy bàn", "Bàn chưa có đơn đang mở.", "OK");
            return;
        }

        if (_currentOrder.Lines.Count > 0)
        {
            await DisplayAlert("Không thể hủy bàn", "Bàn đang có món. Vui lòng hủy hết món trước khi hủy bàn.", "OK");
            return;
        }

        var tableName = _currentTable.TableName;
        var orderId = _currentOrder.OrderId;
        var confirm = await DisplayAlert(
            "Hủy bàn",
            $"Hủy bàn {tableName}?",
            "Hủy bàn",
            "Không");
        if (!confirm)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _api.CancelOrderAsync(orderId);
            CancelAutoKitchenTimer(orderId);
            ClearCurrentOrder();
            await LoadTablesAsync();
        }, $"Đã hủy {tableName}.", "Hủy bàn thất bại");
    }

    private async Task<bool> HasCurrentTableOrderAsync(string actionName, string emptyMessage)
    {
        if (_currentTable is null || _currentOrder is null)
        {
            await DisplayAlert("Chưa chọn bàn", $"Vui lòng chọn bàn trước khi {actionName}.", "OK");
            return false;
        }

        if (_currentOrder.Lines.Count == 0)
        {
            await DisplayAlert("Đơn trống", emptyMessage, "OK");
            return false;
        }

        return true;
    }

    private async Task<TableDto?> SelectTargetTableAsync(
        string title,
        Func<TableDto, bool> predicate,
        string emptyMessage)
    {
        var sourceTableId = _currentTable?.TableId ?? string.Empty;
        var tables = (await _api.GetTablesAsync())
            .Where(table =>
                !string.Equals(table.TableId, sourceTableId, StringComparison.OrdinalIgnoreCase)
                && predicate(table))
            .OrderBy(table => GetZoneName(table.ZoneId), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(table => table.TableName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (tables.Count == 0)
        {
            await DisplayAlert(title, emptyMessage, "OK");
            return null;
        }

        var labels = tables
            .Select((table, index) => $"{index + 1}. {table.TableName} - {GetZoneName(table.ZoneId)} - {(table.HasOpenOrder ? "Đang phục vụ " + AppUi.Money(table.Total) : "Bàn trống")}")
            .ToArray();
        var selected = await DisplayActionSheet(title, "Hủy", null, labels);
        if (string.IsNullOrWhiteSpace(selected) || selected == "Hủy")
        {
            return null;
        }

        var selectedIndex = Array.IndexOf(labels, selected);
        return selectedIndex >= 0 ? tables[selectedIndex] : null;
    }

    private async Task<IReadOnlyList<OrderLineCard>?> SelectSplitLinesAsync()
    {
        var lines = _lines.ToList();
        if (lines.Count == 0)
        {
            await DisplayAlert("Tách bàn", "Không có món để tách.", "OK");
            return null;
        }

        var completion = new TaskCompletionSource<IReadOnlyList<OrderLineCard>?>();
        var completed = false;
#if IOS
        var selectedLineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#endif
        var list = new CollectionView
        {
            ItemsSource = lines,
#if IOS
            SelectionMode = SelectionMode.None,
#else
            SelectionMode = SelectionMode.Multiple,
#endif
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { TextColor = AppUi.Ink, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(13) };
                name.SetBinding(Label.TextProperty, nameof(OrderLineCard.ProductName));
                var summary = new Label { TextColor = AppUi.Muted, FontSize = AppUi.S(11) };
                summary.SetBinding(Label.TextProperty, nameof(OrderLineCard.Summary));
                var total = new Label { TextColor = AppUi.Blue, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(12) };
                total.SetBinding(Label.TextProperty, nameof(OrderLineCard.LineTotal));
                var check = new Label
                {
                    Text = "✓",
                    IsVisible = false,
                    TextColor = AppUi.Blue,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = AppUi.S(16),
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, AppUi.S(8), 0)
                };
                var grid = new Grid
                {
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
                    Padding = new Thickness(4)
                };
                grid.Add(name, 0, 0);
                grid.Add(summary, 0, 1);
                grid.Add(check, 1, 0);
                Grid.SetRowSpan(check, 2);
                grid.Add(total, 2, 0);
                Grid.SetRowSpan(total, 2);
                var card = AppUi.CardView(grid, 8);
#if IOS
                var tap = new TapGestureRecognizer();
                tap.Tapped += (sender, _) =>
                {
                    if (sender is BindableObject bindable && bindable.BindingContext is OrderLineCard line)
                    {
                        ToggleSplitLineSelection(line, card, check, selectedLineIds);
                    }
                };
                card.GestureRecognizers.Add(tap);
#endif
                return card;
            })
        };

        var cancel = CompactButton("Hủy", AppUi.BlueSoft, AppUi.Blue);
        var done = CompactButton("Tiếp tục", AppUi.Blue, Colors.White);
        cancel.Clicked += async (_, _) =>
        {
            completed = true;
            completion.TrySetResult(null);
            await Navigation.PopModalAsync();
        };
        done.Clicked += async (_, _) =>
        {
#if IOS
            var selected = lines.Where(line => selectedLineIds.Contains(line.Source.Id)).ToList();
#else
            var selected = list.SelectedItems.OfType<OrderLineCard>().ToList();
#endif
            if (selected.Count == 0)
            {
                await DisplayAlert("Tách bàn", "Vui lòng chọn ít nhất 1 món để tách.", "OK");
                return;
            }

            completed = true;
            completion.TrySetResult(selected);
            await Navigation.PopModalAsync();
        };

        var footer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        footer.Add(cancel, 0, 0);
        footer.Add(done, 1, 0);

        var page = new ContentPage
        {
            Title = "Tách bàn",
            BackgroundColor = AppUi.Background,
            Content = new Grid
            {
                Padding = 12,
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(GridLength.Auto)
                },
                RowSpacing = 10,
                Children =
                {
                    AppUi.CardView(new VerticalStackLayout
                    {
                        Spacing = AppUi.S(4),
                        Children =
                        {
                            new Label { Text = "Chọn món cần tách", TextColor = AppUi.Ink, FontSize = AppUi.S(16), FontAttributes = FontAttributes.Bold },
                            new Label { Text = "Chạm để chọn một hoặc nhiều dòng món.", TextColor = AppUi.Muted, FontSize = AppUi.S(11) }
                        }
                    }, 12),
                    list,
                    footer
                }
            }
        };
        Grid.SetRow(list, 1);
        Grid.SetRow(footer, 2);
        page.Disappearing += (_, _) =>
        {
            if (!completed)
            {
                completion.TrySetResult(null);
            }
        };

        await Navigation.PushModalAsync(page);
        return await completion.Task;
    }

    private async Task FinalizeTableMutationAsync(string targetTableId, string targetOrderId)
    {
        _currentOrder = await _api.GetOrderAsync(targetOrderId);
        await LoadTablesAsync();
        _currentTable = _allTables
            .Select(table => table.Source)
            .FirstOrDefault(table => string.Equals(table.TableId, targetTableId, StringComparison.OrdinalIgnoreCase))
            ?? _currentTable;
        ApplyOrder();
        ScheduleAutoKitchenPrintIfNeeded();
    }

    private async Task PayAndPrintAsync()
    {
        if (_currentOrder is null)
        {
            await DisplayAlert("Chưa chọn bàn", "Vui lòng chọn bàn cần thanh toán.", "OK");
            return;
        }

        if (_currentOrder.Total <= 0)
        {
            await DisplayAlert("Đơn trống", "Đơn chưa có món hoặc tổng tiền bằng 0.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Thanh toán",
            $"Thanh toán {_tableTitle.Text} và tự động in bill? Hệ thống sẽ tính lại giảm giá trước khi chốt.",
            "Thanh toán",
            "Hủy");
        if (!confirm)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var orderId = _currentOrder.OrderId;
            var result = await _api.PayOrderAsync(
                orderId,
                [new PaymentLineDto("cash", _currentOrder.Total)],
                0,
                Guid.NewGuid().ToString("D"));

            try
            {
                await _api.PrintOrderAsync(orderId, "bill");
                await DisplayAlert(
                    "Đã thanh toán",
                    result.DiscountAmount > 0
                        ? $"Đã thanh toán {AppUi.Money(result.Total)}. Giảm giá {AppUi.Money(result.DiscountAmount)}. Đã gửi lệnh in bill."
                        : $"Đã thanh toán {AppUi.Money(result.Total)}. Đã gửi lệnh in bill.",
                    "OK");
            }
            catch (Exception printEx)
            {
                await DisplayAlert("Đã thanh toán, in bill lỗi", AppUi.ToVietnameseError(printEx), "OK");
            }

            CancelAutoKitchenTimer(orderId);
            ClearCurrentOrder();
            await LoadTablesAsync();
        }, "Đã thanh toán.");
    }

    private void ApplyOrder()
    {
        _lines.Clear();
        if (_currentOrder is null || _currentTable is null)
        {
            _tableTitle.Text = "Chưa chọn bàn";
            _total.Text = "0 đ";
            return;
        }

        _tableTitle.Text = _currentTable.TableName;
        foreach (var line in _currentOrder.Lines)
        {
            _lines.Add(new OrderLineCard(line));
        }

        _total.Text = AppUi.Money(_currentOrder.Total);
    }

    private void ScheduleAutoKitchenPrintIfNeeded()
    {
        if (!_printSettings.AutoKitchenPrintEnabled || _currentOrder is null)
        {
            return;
        }

        var orderId = _currentOrder.OrderId;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return;
        }

        if (!_currentOrder.Lines.Any(line => line.KitchenPendingQuantity > 0))
        {
            CancelAutoKitchenTimer(orderId);
            return;
        }

        lock (_autoKitchenLock)
        {
            if (_autoKitchenPrintTimers.ContainsKey(orderId))
            {
                return;
            }

            var cts = new CancellationTokenSource();
            _autoKitchenPrintTimers[orderId] = cts;
            _ = AutoPrintKitchenAfterDelayAsync(
                orderId,
                TimeSpan.FromMinutes(_printSettings.AutoKitchenPrintDelayMinutes),
                cts.Token);
        }
    }

    private async Task AutoPrintKitchenAfterDelayAsync(string orderId, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            var order = await _api.GetOrderAsync(orderId, cancellationToken);
            if (!order.Lines.Any(line => line.KitchenPendingQuantity > 0))
            {
                return;
            }

            var result = await _api.PrintOrderAsync(orderId, "kitchen", cancellationToken);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                string? refreshWarning = null;
                if (string.Equals(_currentOrder?.OrderId, orderId, StringComparison.OrdinalIgnoreCase))
                {
                    refreshWarning = await TryRefreshCurrentOrderAndTablesAsync();
                    _status.Text = result.Printed
                        ? "Đã tự động in chế biến."
                        : "Tự động in chế biến không có món mới.";
                }
                else
                {
                    refreshWarning = await TryLoadTablesAfterPrintAsync();
                }

                await DisplayAlert(
                    result.Printed ? "Tự động in chế biến thành công" : "Tự động in chế biến chưa in",
                    WithRefreshWarning(result.Printed
                        ? "Đã tự động in phiếu bar/bếp cho các món chưa in."
                        : "Không có món mới cần in bar/bếp hoặc cấu hình in không khớp loại món.",
                        refreshWarning),
                    "OK");
            });
        }
        catch (OperationCanceledException)
        {
            // Timer was cancelled by manual print, payment, or empty order.
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var message = AppUi.ToVietnameseError(ex);
                _status.Text = "Tự động in chế biến lỗi: " + message;
                await DisplayAlert("Tự động in chế biến thất bại", message, "OK");
            });
        }
        finally
        {
            RemoveAutoKitchenTimer(orderId);
        }
    }

    private void CancelAutoKitchenTimer(string orderId)
    {
        CancellationTokenSource? cts = null;
        lock (_autoKitchenLock)
        {
            if (_autoKitchenPrintTimers.Remove(orderId, out var existing))
            {
                cts = existing;
            }
        }

        cts?.Cancel();
        cts?.Dispose();
    }

    private void RemoveAutoKitchenTimer(string orderId)
    {
        lock (_autoKitchenLock)
        {
            if (_autoKitchenPrintTimers.Remove(orderId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    private void CancelAllAutoKitchenTimers()
    {
        List<CancellationTokenSource> timers;
        lock (_autoKitchenLock)
        {
            timers = _autoKitchenPrintTimers.Values.ToList();
            _autoKitchenPrintTimers.Clear();
        }

        foreach (var timer in timers)
        {
            timer.Cancel();
            timer.Dispose();
        }
    }

    private void ClearCurrentOrder()
    {
        if (!string.IsNullOrWhiteSpace(_currentOrder?.OrderId))
        {
            CancelAutoKitchenTimer(_currentOrder.OrderId);
        }

        _currentOrder = null;
        _currentTable = null;
        ApplyOrder();
    }

    private async Task LogoutAsync()
    {
        var confirm = await DisplayAlert("Đăng xuất", "Quay lại màn hình đăng nhập?", "Đăng xuất", "Hủy");
        if (!confirm)
        {
            return;
        }

        _loginSettings.DisableAutoLogin();
        CancelPendingTableRefresh();
        CancelAllAutoKitchenTimers();
        try
        {
            await _api.LogoutAsync();
        }
        catch
        {
            // Login screen can create a new session even if remote logout is unavailable.
        }

        await Navigation.PopToRootAsync();
    }

    private async Task RunAsync(Func<Task> action, string success, string errorTitle = "Lỗi")
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        _busy.IsVisible = true;
        _busy.IsRunning = true;
        try
        {
            await action();
            _status.Text = success;
        }
        catch (Exception ex)
        {
            await DisplayAlert(errorTitle, AppUi.ToVietnameseError(ex), "OK");
            _status.Text = "Có lỗi, vui lòng thử lại.";
        }
        finally
        {
            _busy.IsRunning = false;
            _busy.IsVisible = false;
            _isBusy = false;
        }
    }
}


