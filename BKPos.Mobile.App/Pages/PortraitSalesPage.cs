using System.Collections.ObjectModel;
using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Pages;

public sealed class PortraitSalesPage : ContentPage
{
    // ── State ────────────────────────────────────────────────────────────────
    private readonly ApiClient _api;
    private readonly MobileLoginSettings _loginSettings;
    private readonly MobilePrintSettings _printSettings;
    private readonly MobileOrientationSettings _orientationSettings;

    private readonly ObservableCollection<TableCard> _tables = [];
    private readonly ObservableCollection<OrderLineCard> _lines = [];
    private readonly ObservableCollection<PortraitProductCard> _products = [];
    private readonly ObservableCollection<PrintedBillCard> _printedBills = [];

    private readonly CollectionView _tableList;
    private readonly CollectionView _lineList;
    private readonly CollectionView _productList;
    private readonly CollectionView _printedBillList;

    private readonly Entry _tableSearch = new()
    {
        Placeholder = "Tìm bàn/khu",
        BackgroundColor = Colors.Transparent,
        TextColor = AppUi.Ink,
        PlaceholderColor = AppUi.Muted,
        ReturnType = ReturnType.Done,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };
    private readonly Entry _productSearch = new()
    {
        Placeholder = "Tìm nhanh món",
        BackgroundColor = Colors.Transparent,
        TextColor = AppUi.Ink,
        PlaceholderColor = AppUi.Muted,
        ReturnType = ReturnType.Done,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };

    private readonly Label _headerTitle = new()
    {
        Text = "BKPos Mobile",
        TextColor = Colors.White,
        FontSize = AppUi.S(15),
        FontAttributes = FontAttributes.Bold,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalTextAlignment = TextAlignment.Center,
        LineBreakMode = LineBreakMode.TailTruncation
    };
    private readonly Label _tableTitle = new()
    {
        Text = "Chưa chọn bàn",
        TextColor = AppUi.Ink,
        FontSize = AppUi.S(14),
        FontAttributes = FontAttributes.Bold
    };
    private readonly Label _total = new()
    {
        Text = "0 đ",
        TextColor = AppUi.Navy,
        FontSize = AppUi.S(17),
        FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.End
    };
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Blue, IsVisible = false };

    private List<ZoneDto> _zones = [];
    private List<TableCard> _allTables = [];
    private List<ProductDto> _allProducts = [];
    private TableDto? _currentTable;
    private OrderDto? _currentOrder;
    private Dictionary<string, int> _orderedQtyByProductId = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CancellationTokenSource> _autoKitchenPrintTimers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _autoKitchenLock = new();
    private CancellationTokenSource? _tableRefreshDebounce;
    private bool _isBusy;

    // ── Tab panels (content grids for each tab) ───────────────────────────
    private Grid? _tablesPanel;
    private Grid? _orderPanel;
    private Grid? _productsPanel;
    private Grid? _printedBillsPanel;
    private int _activeTab = 0;

    // ── Tab bar labels ────────────────────────────────────────────────────
    private readonly Label _tabBanLabel = new() { Text = "Bàn", FontSize = AppUi.S(12), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _tabDonLabel = new() { Text = "Món ăn", FontSize = AppUi.S(12), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _tabMonLabel = new() { Text = "Hóa đơn", FontSize = AppUi.S(12), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _tabBillLabel = new() { Text = "Bill đã in", FontSize = AppUi.S(12), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Border _tabBanBtn = null!;
    private readonly Border _tabDonBtn = null!;
    private readonly Border _tabMonBtn = null!;
    private readonly Border _tabBillBtn = null!;

    // ── Inner record ──────────────────────────────────────────────────────
    private sealed record PortraitProductCard(ProductDto Source, int CurrentQty)
    {
        public string Name => Source.Name;
        public decimal Price => Source.Price;
        public string UnitName => Source.UnitName;
        public bool HasQty => CurrentQty > 0;
        public string QtyText => $"×{CurrentQty}";
        public Color CardBackground => HasQty ? Color.FromArgb("#EFF6FF") : AppUi.Surface;
        public Color CardBorder => HasQty ? AppUi.Blue : AppUi.Border;
        public float CardBorderThickness => HasQty ? 1.5f : 1f;
    }

    public PortraitSalesPage(
        ApiClient api,
        MobileLoginSettings loginSettings,
        MobilePrintSettings printSettings,
        MobileOrientationSettings orientationSettings)
    {
        _api = api;
        _loginSettings = loginSettings;
        _printSettings = printSettings;
        _orientationSettings = orientationSettings;
        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = AppUi.Background;

        _tableList = BuildTableList();
        _lineList = BuildLineList();
        _productList = BuildProductList();
        _printedBillList = BuildPrintedBillList();

        (_tabBanBtn, _tabDonBtn, _tabMonBtn, _tabBillBtn) = BuildTabButtons();

        Content = AppKeyboardHost.Wrap(BuildContent());
        ApplyTabHighlight();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_allProducts.Count == 0)
            await LoadInitialAsync();
    }

    // ── UI construction ───────────────────────────────────────────────────

    private View BuildContent()
    {
        _tableSearch.TextChanged += (_, _) => ApplyTableFilter();
        _productSearch.TextChanged += (_, _) => ApplyProductFilter();

        _tablesPanel = BuildTablesPanel();
        _orderPanel = BuildOrderPanel();
        _productsPanel = BuildProductsPanel();
        _printedBillsPanel = BuildPrintedBillsPanel();

        _orderPanel.IsVisible = false;
        _productsPanel.IsVisible = false;
        _printedBillsPanel.IsVisible = false;

        var contentArea = new Grid();
        contentArea.Add(_tablesPanel);
        contentArea.Add(_orderPanel);
        contentArea.Add(_productsPanel);
        contentArea.Add(_printedBillsPanel);

        _busy.HorizontalOptions = LayoutOptions.End;
        _busy.VerticalOptions = LayoutOptions.End;
        _busy.Margin = new Thickness(0, 0, AppUi.S(8), AppUi.S(8));
        _busy.ZIndex = 10;

        var contentWithBusy = new Grid();
        contentWithBusy.Add(contentArea);
        contentWithBusy.Add(_busy);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.Add(BuildTopBar(), 0, 0);
        root.Add(contentWithBusy, 0, 1);
        root.Add(BuildTabBar(), 0, 2);

        return root;
    }

    private View BuildTopBar()
    {
        // Landscape toggle (top-left)
        var landscapeToggle = new Label
        {
            Text = "↔",
            TextColor = Colors.White,
            FontSize = AppUi.S(20),
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = AppUi.S(40)
        };
        var landTap = new TapGestureRecognizer();
        landTap.Tapped += async (_, _) => await SwitchToLandscapeAsync();
        landscapeToggle.GestureRecognizers.Add(landTap);

        // Logout (top-right)
        var logoutTrigger = new Label
        {
            Text = "≡",
            TextColor = Colors.White,
            FontSize = AppUi.S(22),
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = AppUi.S(40)
        };
        var logoutTap = new TapGestureRecognizer();
        logoutTap.Tapped += async (_, _) => await ConfirmLogoutAsync();
        logoutTrigger.GestureRecognizers.Add(logoutTap);

        var bar = new Grid
        {
            BackgroundColor = AppUi.Navy,
            Padding = new Thickness(AppUi.S(4), AppUi.S(6)),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        bar.Add(landscapeToggle, 0, 0);
        bar.Add(_headerTitle, 1, 0);
        bar.Add(logoutTrigger, 2, 0);

        return bar;
    }

    private (Border ban, Border don, Border mon, Border bill) BuildTabButtons()
    {
        Border MakeTab(Label lbl, string icon, int idx)
        {
            var ico = new Label
            {
                Text = icon,
                FontSize = AppUi.S(18),
                TextColor = AppUi.Muted,
                HorizontalTextAlignment = TextAlignment.Center
            };
            lbl.TextColor = AppUi.Muted;
            var stack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                Children = { ico, lbl }
            };
            var btn = new Border
            {
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                Padding = new Thickness(AppUi.S(6), AppUi.S(6)),
                Content = stack
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SwitchTab(idx);
            btn.GestureRecognizers.Add(tap);
            return btn;
        }

        var ban = MakeTab(_tabBanLabel, "🗂", 0);
        var don = MakeTab(_tabDonLabel, "🍽", 1);
        var mon = MakeTab(_tabMonLabel, "📋", 2);
        var bill = MakeTab(_tabBillLabel, "🧾", 3);
        return (ban, don, mon, bill);
    }

    private View BuildTabBar()
    {
        var bar = new Grid
        {
            BackgroundColor = AppUi.Surface,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 0
        };
        bar.Add(_tabBanBtn, 0, 0);
        bar.Add(_tabDonBtn, 1, 0);
        bar.Add(_tabMonBtn, 2, 0);
        bar.Add(_tabBillBtn, 3, 0);

        var wrapper = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(1)),
                new RowDefinition(GridLength.Auto)
            }
        };
        wrapper.Add(new BoxView { Color = AppUi.Border }, 0, 0);
        wrapper.Add(bar, 0, 1);
        return wrapper;
    }

    private Grid BuildTablesPanel()
    {
        _tableSearch.HeightRequest = AppUi.S(34);
        _tableSearch.FontSize = AppUi.S(12);
        var panel = new Grid
        {
            Padding = new Thickness(AppUi.S(8)),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 8
        };
        panel.Add(PortraitSearchBox(_tableSearch), 0, 0);
        panel.Add(_tableList, 0, 1);
        return panel;
    }

    private Grid BuildOrderPanel()
    {
        Border Chip(string text, Color bg, Color fg, Func<Task> onTap)
        {
            var lbl = new Label
            {
                Text = text, TextColor = fg, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(10),
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            };
            var b = new Border
            {
                BackgroundColor = bg, StrokeThickness = 0, HeightRequest = AppUi.S(24),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Padding = new Thickness(AppUi.S(8), 0), Content = lbl
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await onTap();
            b.GestureRecognizers.Add(tap);
            return b;
        }

        Border ActionBtn(string text, Color bg, Color fg, Func<Task> onTap)
        {
            var lbl = new Label
            {
                Text = text, TextColor = fg, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(13),
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            };
            var b = new Border
            {
                BackgroundColor = bg, StrokeThickness = 0, HeightRequest = AppUi.S(38),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(AppUi.S(10), 0), Content = lbl
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await onTap();
            b.GestureRecognizers.Add(tap);
            return b;
        }

        _tableTitle.LineBreakMode = LineBreakMode.TailTruncation;
        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        titleRow.Add(_tableTitle, 0, 0);
        titleRow.Add(new HorizontalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                Chip("Chuyển", AppUi.BlueSoft, AppUi.Blue, TransferTableAsync),
                Chip("Gộp", AppUi.BlueSoft, AppUi.Blue, MergeTableAsync),
                Chip("Tách", AppUi.BlueSoft, AppUi.Blue, SplitTableAsync),
                Chip("Hủy", Color.FromArgb("#DC2626"), Colors.White, CancelCurrentTableAsync)
            }
        }, 1, 0);

        var totalRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(AppUi.S(10), AppUi.S(4))
        };
        totalRow.Add(new Label
        {
            Text = "Tổng cộng",
            TextColor = AppUi.Navy,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            FontSize = AppUi.S(14)
        }, 0, 0);
        totalRow.Add(_total, 1, 0);

        var totalBorder = new Border
        {
            Stroke = AppUi.Blue,
            StrokeThickness = 1.5,
            StrokeDashArray = [5, 3],
            BackgroundColor = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = totalRow
        };

        var actionRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        actionRow.Add(ActionBtn("In chế biến", AppUi.Blue, Colors.White, PrintKitchenAsync), 0, 0);
        actionRow.Add(ActionBtn("✓ Thanh toán", AppUi.Red, Colors.White, PayAndPrintAsync), 1, 0);

        var panel = new Grid
        {
            Padding = new Thickness(AppUi.S(8)),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 8
        };
        panel.Add(AppUi.CardView(titleRow, 8), 0, 0);
        panel.Add(_lineList, 0, 1);
        panel.Add(totalBorder, 0, 2);
        panel.Add(actionRow, 0, 3);
        return panel;
    }

    private Grid BuildProductsPanel()
    {
        _productSearch.HeightRequest = AppUi.S(34);
        _productSearch.FontSize = AppUi.S(12);
        var panel = new Grid
        {
            Padding = new Thickness(AppUi.S(8)),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 8
        };
        panel.Add(PortraitSearchBox(_productSearch), 0, 0);
        panel.Add(_productList, 0, 1);
        return panel;
    }

    private Grid BuildPrintedBillsPanel()
    {
        var panel = new Grid
        {
            Padding = new Thickness(AppUi.S(8)),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star)
            }
        };
        panel.Add(_printedBillList, 0, 0);
        return panel;
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
            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical)
            {
                HorizontalItemSpacing = 6,
                VerticalItemSpacing = 6
            },
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
                    Padding = AppUi.S(6),
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
                    if (sender is BindableObject bo && bo.BindingContext is TableCard table)
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
                    Padding = new Thickness(AppUi.S(5), AppUi.S(2)),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                    Content = kitchenBadgeText,
                    VerticalOptions = LayoutOptions.Center
                };
                kitchenBadge.SetBinding(Border.IsVisibleProperty, nameof(OrderLineCard.HasKitchenPrint));
                kitchenBadge.SetBinding(Border.BackgroundColorProperty, nameof(OrderLineCard.KitchenPrintStatusColor));
                var summary = new HorizontalStackLayout { Spacing = 4, Children = { qty, price, kitchenBadge } };
                var note = new Label { TextColor = AppUi.Warning, FontSize = AppUi.S(10), LineBreakMode = LineBreakMode.TailTruncation };
                note.SetBinding(Label.TextProperty, nameof(OrderLineCard.NoteDisplay));
                var lineTotal = new Label { TextColor = AppUi.Blue, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(13), HorizontalTextAlignment = TextAlignment.End };
                lineTotal.SetBinding(Label.TextProperty, nameof(OrderLineCard.LineTotal));
                var remove = new Button
                {
                    Text = "x",
                    BackgroundColor = Colors.Transparent,
                    TextColor = AppUi.Red,
                    BorderColor = AppUi.Red,
                    BorderWidth = 1,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = AppUi.S(11),
                    WidthRequest = AppUi.S(26),
                    HeightRequest = AppUi.S(26),
                    MinimumWidthRequest = AppUi.S(26),
                    MinimumHeightRequest = AppUi.S(26),
                    CornerRadius = (int)AppUi.S(13),
                    Padding = 0
                };
                remove.Clicked += async (sender, _) =>
                {
                    if (sender is BindableObject bo && bo.BindingContext is OrderLineCard line)
                        await DeleteLineQuickAsync(line);
                };
                var right = new VerticalStackLayout
                {
                    Spacing = 4,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { remove, lineTotal }
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
                    if (sender is BindableObject bo && bo.BindingContext is OrderLineCard line)
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
                HorizontalItemSpacing = 6,
                VerticalItemSpacing = 6
            },
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label
                {
                    TextColor = AppUi.Ink,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = AppUi.S(12),
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                name.SetBinding(Label.TextProperty, nameof(PortraitProductCard.Name));
                var price = new Label { TextColor = AppUi.Blue, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(12) };
                price.SetBinding(Label.TextProperty,
                    new Binding(nameof(PortraitProductCard.Price), stringFormat: "{0:N0}đ"));
                var unit = new Label { TextColor = AppUi.Muted, FontSize = AppUi.S(10) };
                unit.SetBinding(Label.TextProperty, nameof(PortraitProductCard.UnitName));

                // Quantity badge (top-right overlay)
                var qtyBadge = new Border
                {
                    BackgroundColor = AppUi.Blue,
                    StrokeThickness = 0,
                    Padding = new Thickness(AppUi.S(5), AppUi.S(1)),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, 0, 0, 0),
                    Content = new Label
                    {
                        TextColor = Colors.White,
                        FontSize = AppUi.S(10),
                        FontAttributes = FontAttributes.Bold,
                        VerticalTextAlignment = TextAlignment.Center
                    }
                };
                ((Label)qtyBadge.Content).SetBinding(Label.TextProperty, nameof(PortraitProductCard.QtyText));
                qtyBadge.SetBinding(VisualElement.IsVisibleProperty, nameof(PortraitProductCard.HasQty));

                var cardContent = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto)
                    }
                };
                cardContent.Add(name, 0, 0);
                cardContent.Add(unit, 0, 1);
                cardContent.Add(price, 0, 2);

                var card = new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                    Padding = new Thickness(AppUi.S(8))
                };
                card.SetBinding(Border.BackgroundColorProperty, nameof(PortraitProductCard.CardBackground));
                card.SetBinding(Border.StrokeProperty, nameof(PortraitProductCard.CardBorder));
                card.SetBinding(Border.StrokeThicknessProperty, nameof(PortraitProductCard.CardBorderThickness));

                var inner = new Grid();
                inner.Add(cardContent);
                inner.Add(qtyBadge);
                card.Content = inner;

#if IOS
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (sender, _) =>
                {
                    if (sender is BindableObject bo && bo.BindingContext is PortraitProductCard pc)
                    {
                        await MarkTappedAsync(card);
                        await AddProductAsync(pc.Source);
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
            if (e.CurrentSelection.FirstOrDefault() is PortraitProductCard pc)
            {
                list.SelectedItem = null;
                await AddProductAsync(pc.Source);
            }
        };
#endif
        return list;
    }

    private CollectionView BuildPrintedBillList()
    {
        var list = new CollectionView
        {
            ItemsSource = _printedBills,
#if IOS
            SelectionMode = SelectionMode.None,
#else
            SelectionMode = SelectionMode.Single,
#endif
            ItemTemplate = new DataTemplate(() =>
            {
                var table = new Label
                {
                    TextColor = AppUi.Ink,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = AppUi.S(14),
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                table.SetBinding(Label.TextProperty, nameof(PrintedBillCard.TableName));

                var paidAt = new Label
                {
                    TextColor = AppUi.Muted,
                    FontSize = AppUi.S(11)
                };
                paidAt.SetBinding(Label.TextProperty, nameof(PrintedBillCard.PaidAtText));

                var cashier = new Label
                {
                    TextColor = AppUi.Muted,
                    FontSize = AppUi.S(11),
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                cashier.SetBinding(Label.TextProperty, nameof(PrintedBillCard.CashierText));

                var discount = new Label
                {
                    TextColor = AppUi.Orange,
                    FontSize = AppUi.S(11),
                    FontAttributes = FontAttributes.Bold
                };
                discount.SetBinding(Label.TextProperty, nameof(PrintedBillCard.DiscountText));
                discount.SetBinding(VisualElement.IsVisibleProperty, nameof(PrintedBillCard.HasDiscount));

                var total = new Label
                {
                    TextColor = AppUi.Blue,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = AppUi.S(16),
                    HorizontalTextAlignment = TextAlignment.End,
                    VerticalTextAlignment = TextAlignment.Center
                };
                total.SetBinding(Label.TextProperty, nameof(PrintedBillCard.TotalText));

                var left = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { table, paidAt, cashier, discount }
                };

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 8
                };
                grid.Add(left, 0, 0);
                grid.Add(total, 1, 0);

                var card = AppUi.CardView(grid, 9);
#if IOS
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (sender, _) =>
                {
                    if (sender is BindableObject bo && bo.BindingContext is PrintedBillCard bill)
                    {
                        await MarkTappedAsync(card);
                        await OpenPrintedBillAsync(bill);
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
            if (e.CurrentSelection.FirstOrDefault() is PrintedBillCard bill)
            {
                list.SelectedItem = null;
                await OpenPrintedBillAsync(bill);
            }
        };
#endif
        return list;
    }

    private static Border PortraitSearchBox(Entry entry) => new()
    {
        BackgroundColor = AppUi.SurfaceAlt,
        Stroke = AppUi.Border,
        StrokeThickness = 1,
        Padding = new Thickness(10, 0),
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        Content = entry
    };

    // ── Tab switching ─────────────────────────────────────────────────────

    private void SwitchTab(int tab)
    {
        _activeTab = tab;
        if (_tablesPanel != null) _tablesPanel.IsVisible = tab == 0;
        if (_productsPanel != null) _productsPanel.IsVisible = tab == 1;
        if (_orderPanel != null) _orderPanel.IsVisible = tab == 2;
        if (_printedBillsPanel != null) _printedBillsPanel.IsVisible = tab == 3;
        UpdateHeaderTitle();
        ApplyTabHighlight();

        if (tab == 3)
            _ = LoadPrintedBillsAsync(force: true);
    }

    private void UpdateHeaderTitle()
    {
        if (_activeTab == 3)
        {
            _headerTitle.Text = "Bill đã in";
            return;
        }

        if (_currentTable is null)
        {
            _headerTitle.Text = "BKPos Mobile";
            return;
        }

        _headerTitle.Text = _activeTab switch
        {
            1 => $"Chọn món cho {_currentTable.TableName}",
            2 => $"Hóa đơn {_currentTable.TableName}",
            _ => "BKPos Mobile"
        };
    }

    private void ApplyTabHighlight()
    {
        void Style(Label lbl, Border btn, bool active)
        {
            lbl.TextColor = active ? AppUi.Blue : AppUi.Muted;
            if (btn.Content is VerticalStackLayout vsl && vsl.Children[0] is Label iconLbl)
                iconLbl.TextColor = active ? AppUi.Blue : AppUi.Muted;
            btn.BackgroundColor = active ? AppUi.BlueSoft : Colors.Transparent;
        }

        if (_tabBanBtn == null) return;
        Style(_tabBanLabel, _tabBanBtn, _activeTab == 0);
        Style(_tabDonLabel, _tabDonBtn, _activeTab == 1);
        Style(_tabMonLabel, _tabMonBtn, _activeTab == 2);
        Style(_tabBillLabel, _tabBillBtn, _activeTab == 3);
    }

    // ── Orientation switch ────────────────────────────────────────────────

    private async Task SwitchToLandscapeAsync()
    {
        _orientationSettings.Save(MobileOrientationSettings.Landscape,
            _orientationSettings.Load().remember);
        OrientationService.Current.LockLandscape();
        var landscape = new SalesPage(_api, _loginSettings, _printSettings);
        Navigation.InsertPageBefore(landscape, this);
        await Navigation.PopAsync(false);
    }

    // ── Data loading ──────────────────────────────────────────────────────

    private async Task LoadInitialAsync(bool force = false)
    {
        await RunAsync(async () =>
        {
            if (force || _zones.Count == 0)
                _zones = (await _api.GetZonesAsync()).ToList();

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
        _allTables = tables.Select(t => new TableCard(t)).ToList();
        ApplyTableFilter();
    }


    private async Task LoadPrintedBillsAsync(bool force = false)
    {
        await RunAsync(async () =>
        {
            var bills = await _api.GetPrintedBillsTodayAsync();
            _printedBills.Clear();
            foreach (var bill in bills.OrderByDescending(b => b.PaidAt ?? b.BusinessDate ?? b.StartTime))
                _printedBills.Add(new PrintedBillCard(bill));
        }, "Đã tải bill hôm nay.", "Tải bill thất bại");
    }

    private async Task OpenPrintedBillAsync(PrintedBillCard bill)
    {
        await RunAsync(async () =>
        {
            var detail = await _api.GetPrintedBillAsync(bill.Source.OrderId);
            await Navigation.PushModalAsync(new PrintedBillDetailPage(_api, detail));
        }, "Đã mở chi tiết bill.", "Mở bill thất bại");
    }
    // ── Filter ────────────────────────────────────────────────────────────

    private void ApplyTableFilter()
    {
        var keyword = (_tableSearch.Text ?? string.Empty).Trim();
        var filtered = _allTables
            .Where(t =>
                string.IsNullOrWhiteSpace(keyword)
                || AppUi.ContainsSearch(t.Name, keyword)
                || AppUi.ContainsSearch(t.Status, keyword)
                || AppUi.ContainsSearch(GetZoneName(t.Source.ZoneId), keyword))
            .OrderByDescending(t => t.Source.HasOpenOrder)
            .ThenBy(t => t.Source.ZoneId, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase);

        _tables.Clear();
        foreach (var t in filtered)
            _tables.Add(new TableCard(t.Source, IsCurrentTable(t.Source.TableId)));
    }

    private void ApplyProductFilter()
    {
        var keyword = (_productSearch.Text ?? string.Empty).Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _allProducts
            : _allProducts
                .Select(product => new { Product = product, Score = AppUi.ProductSearchScore(product.Name, keyword) })
                .Where(item => item.Score >= 0)
                .OrderBy(item => item.Score)
                .ThenBy(item => item.Product.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => item.Product);

        _products.Clear();
        foreach (var p in filtered)
        {
            var qty = _orderedQtyByProductId.GetValueOrDefault(p.ExternalId, 0);
            _products.Add(new PortraitProductCard(p, qty));
        }
    }

    private void UpdateProductQuantities()
    {
        _orderedQtyByProductId = _currentOrder?.Lines
            .GroupBy(l => l.ProductId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        ApplyProductFilter();
    }

    private bool IsCurrentTable(string tableId)
        => _currentTable is not null
           && string.Equals(_currentTable.TableId, tableId, StringComparison.OrdinalIgnoreCase);

    private string GetZoneName(string zoneId)
        => _zones.FirstOrDefault(z => string.Equals(z.ExternalId, zoneId, StringComparison.OrdinalIgnoreCase))?.Name
           ?? string.Empty;

    // ── Order state ───────────────────────────────────────────────────────

    private void ApplyOrder()
    {
        _lines.Clear();
        if (_currentOrder is null || _currentTable is null)
        {
            _tableTitle.Text = "Chưa chọn bàn";
            _total.Text = "0 đ";
            UpdateProductQuantities();
            UpdateHeaderTitle();
            return;
        }

        _tableTitle.Text = _currentTable.TableName;
        foreach (var line in _currentOrder.Lines)
            _lines.Add(new OrderLineCard(line));
        _total.Text = AppUi.Money(_currentOrder.Total);
        UpdateProductQuantities();
        UpdateHeaderTitle();
    }

    private async Task ApplyMutationResponseAsync(MutationResponseDto mutation)
    {
        if (mutation.Order is not null)
        {
            _currentOrder = mutation.Order;
        }
        else if (_currentOrder is not null
                 && string.Equals(_currentOrder.OrderId, mutation.OrderId, StringComparison.OrdinalIgnoreCase))
        {
            _currentOrder = await _api.GetOrderAsync(_currentOrder.OrderId);
        }

        UpdateCurrentTableSnapshot(_currentOrder?.OrderId ?? mutation.OrderId, _currentOrder?.Total ?? mutation.Total);
        ApplyOrder();
    }

    private void UpdateCurrentTableSnapshot(string orderId, decimal total)
    {
        if (_currentTable is null) return;

        _currentTable = _currentTable with
        {
            HasOpenOrder = true,
            OrderId = orderId,
            OccupiedAt = _currentTable.OccupiedAt ?? _currentOrder?.CreatedAt,
            Total = total
        };

        for (var i = 0; i < _allTables.Count; i++)
        {
            if (string.Equals(_allTables[i].Source.TableId, _currentTable.TableId, StringComparison.OrdinalIgnoreCase))
            {
                _allTables[i].Update(_currentTable, false);
                break;
            }
        }

        RefreshVisibleTableSelection(updatedTable: _currentTable);
    }

    private void RefreshVisibleTableSelection(string? previousTableId = null, TableDto? updatedTable = null)
    {
        var currentTableId = _currentTable?.TableId;
        for (var i = 0; i < _tables.Count; i++)
        {
            var t = _tables[i];
            var hasUpdated = updatedTable is not null
                && string.Equals(t.Source.TableId, updatedTable.TableId, StringComparison.OrdinalIgnoreCase);
            var isPrev = !string.IsNullOrWhiteSpace(previousTableId)
                && string.Equals(t.Source.TableId, previousTableId, StringComparison.OrdinalIgnoreCase);
            var isCur = !string.IsNullOrWhiteSpace(currentTableId)
                && string.Equals(t.Source.TableId, currentTableId, StringComparison.OrdinalIgnoreCase);

            if (!hasUpdated && !isPrev && !isCur) continue;

            if (hasUpdated) t.Update(updatedTable!, isCur);
            else t.SetCurrent(isCur);
        }
    }

    private void ClearCurrentOrder()
    {
        if (!string.IsNullOrWhiteSpace(_currentOrder?.OrderId))
            CancelAutoKitchenTimer(_currentOrder.OrderId);

        var prevId = _currentTable?.TableId;
        _currentOrder = null;
        _currentTable = null;
        RefreshVisibleTableSelection(prevId);
        ApplyOrder();
    }

    // ── Table refresh ──────────────────────────────────────────────────────

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
                    if (cts.IsCancellationRequested) return;
                    _allTables = tables.Select(t => new TableCard(t)).ToList();
                    ApplyTableFilter();
                });
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                if (ReferenceEquals(_tableRefreshDebounce, cts))
                    _tableRefreshDebounce = null;
                cts.Dispose();
            }
        });
    }

    private void CancelPendingTableRefresh()
    {
        _tableRefreshDebounce?.Cancel();
        _tableRefreshDebounce = null;
    }

    // ── Business actions ──────────────────────────────────────────────────

    private async Task OpenTableAsync(TableDto table)
    {
        await RunAsync(async () =>
        {
            var prevId = _currentTable?.TableId;
            _currentTable = table;
            RefreshVisibleTableSelection(prevId);

            var opened = await _api.OpenTableAsync(table.TableId);
            _currentOrder = opened.Order ?? await _api.GetOrderAsync(opened.OrderId);
            UpdateCurrentTableSnapshot(_currentOrder.OrderId, _currentOrder.Total);
            ApplyOrder();
            ScheduleAutoKitchenPrintIfNeeded();
            SwitchTab(1);
        }, $"Đã mở {table.TableName}.");
    }

    private async Task AddProductAsync(ProductDto product)
    {
        if (_currentOrder is null)
        {
            await DisplayAlert("Chưa chọn bàn", "Vui lòng chọn bàn (Tab Bàn) trước khi thêm món.", "OK");
            return;
        }

        await RunAsync(async () =>
        {
            var mutation = await _api.AddLineAsync(_currentOrder.OrderId, product, 1);
            await ApplyMutationResponseAsync(mutation);
            ScheduleAutoKitchenPrintIfNeeded();
        }, $"Đã thêm {product.Name}.");
    }

    private async Task EditLineAsync(OrderLineCard line)
    {
        if (_currentOrder is null) return;
        await Navigation.PushModalAsync(new OrderLineEditPage(
            line,
            async (qty, note) => await SaveLineAsync(line, qty, note),
            async () => await DeleteLineAsync(line)));
    }

    private async Task SaveLineAsync(OrderLineCard line, int quantity, string note)
    {
        if (_currentOrder is null) return;
        var mutation = await _api.UpdateLineAsync(_currentOrder.OrderId, line.Source.Id, quantity, note);
        await ApplyMutationResponseAsync(mutation);
        ScheduleAutoKitchenPrintIfNeeded();
    }

    private async Task DeleteLineAsync(OrderLineCard line)
    {
        if (_currentOrder is null) return;
        var mutation = await _api.RemoveLineAsync(_currentOrder.OrderId, line.Source.Id);
        await ApplyMutationResponseAsync(mutation);
        ScheduleAutoKitchenPrintIfNeeded();
    }

    private async Task DeleteLineQuickAsync(OrderLineCard line)
    {
        if (_currentOrder is null) return;
        var confirm = await DisplayAlert("Hủy món", $"Hủy {line.ProductName} khỏi {_tableTitle.Text}?", "Hủy món", "Không");
        if (!confirm) return;

        await RunAsync(async () =>
        {
            var mutation = await _api.RemoveLineAsync(_currentOrder.OrderId, line.Source.Id);
            await ApplyMutationResponseAsync(mutation);
            ScheduleAutoKitchenPrintIfNeeded();
        }, "Đã hủy món.", "Hủy món thất bại");
    }

    private async Task RefreshCurrentOrderAsync()
    {
        if (_currentOrder is not null)
            _currentOrder = await _api.GetOrderAsync(_currentOrder.OrderId);
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
            return AppUi.ToVietnameseError(ex);
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
            return AppUi.ToVietnameseError(ex);
        }
    }

    private static string WithRefreshWarning(string message, string? warn)
        => string.IsNullOrWhiteSpace(warn) ? message
            : $"{message}\n\nLưu ý: đã gửi lệnh in, nhưng app chưa tải lại được dữ liệu mới.";

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
            var warn = await TryRefreshCurrentOrderAndTablesAsync();

            if (result.Printed)
            {
                CancelAutoKitchenTimer(orderId);
                await DisplayAlert("In chế biến thành công",
                    WithRefreshWarning("Đã in phiếu bar/bếp cho các món chưa in.", warn), "OK");
                return;
            }

            var msg = result.Deduplicated
                ? "Lệnh in bị chặn để tránh in trùng. Vui lòng thử lại sau."
                : "Không có món mới cần in bar/bếp hoặc cấu hình in không khớp loại món.";
            await DisplayAlert("Không in được", WithRefreshWarning(msg, warn), "OK");
        }, "Đã xử lý lệnh in chế biến.", "In chế biến thất bại");
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

        var confirm = await DisplayAlert("Thanh toán",
            $"Thanh toán {_tableTitle.Text} và tự động in bill?",
            "Thanh toán", "Hủy");
        if (!confirm) return;

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
                await DisplayAlert("Đã thanh toán",
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
            _printedBills.Clear();
            await LoadTablesAsync();
            SwitchTab(0);
        }, "Đã thanh toán.");
    }

    private async Task TransferTableAsync()
    {
        if (!await HasCurrentTableOrderAsync("chuyển bàn", "Bàn chưa có món để chuyển.")) return;

        var target = await SelectTargetTableAsync("Chuyển bàn", t => !t.HasOpenOrder, "Không có bàn trống phù hợp.");
        if (target is null || _currentOrder is null) return;

        var confirm = await DisplayAlert("Chuyển bàn",
            $"Chuyển toàn bộ món từ {_tableTitle.Text} sang {target.TableName}?", "Chuyển", "Hủy");
        if (!confirm) return;

        await RunAsync(async () =>
        {
            var srcId = _currentOrder!.OrderId;
            CancelAutoKitchenTimer(srcId);
            var result = await _api.TransferOrderAsync(srcId, target.TableId);
            await FinalizeTableMutationAsync(target.TableId, result.OrderId);
            await DisplayAlert("Đã chuyển bàn", $"Đã chuyển đơn sang {target.TableName}.", "OK");
        }, "Đã chuyển bàn.", "Chuyển bàn thất bại");
    }

    private async Task MergeTableAsync()
    {
        if (!await HasCurrentTableOrderAsync("gộp bàn", "Bàn chưa có món để gộp.")) return;

        var target = await SelectTargetTableAsync("Gộp bàn",
            t => t.HasOpenOrder && !string.IsNullOrWhiteSpace(t.OrderId),
            "Không có bàn đang phục vụ phù hợp.");
        if (target is null || _currentOrder is null) return;

        var confirm = await DisplayAlert("Gộp bàn",
            $"Gộp toàn bộ món từ {_tableTitle.Text} sang {target.TableName}?", "Gộp", "Hủy");
        if (!confirm) return;

        await RunAsync(async () =>
        {
            var srcId = _currentOrder!.OrderId;
            CancelAutoKitchenTimer(srcId);
            var result = await _api.MergeOrderAsync(srcId, target.OrderId!);
            await FinalizeTableMutationAsync(target.TableId, result.OrderId);
            await DisplayAlert("Đã gộp bàn", $"Đã gộp đơn sang {target.TableName}.", "OK");
        }, "Đã gộp bàn.", "Gộp bàn thất bại");
    }

    private async Task SplitTableAsync()
    {
        if (!await HasCurrentTableOrderAsync("tách bàn", "Bàn chưa có món để tách.")) return;

        var selected = await SelectSplitLinesAsync();
        if (selected is null || selected.Count == 0) return;

        var target = await SelectTargetTableAsync("Tách bàn", _ => true, "Không có bàn đích phù hợp.");
        if (target is null || _currentOrder is null) return;

        var confirm = await DisplayAlert("Tách bàn",
            $"Tách {selected.Count} dòng món sang {target.TableName}?", "Tách", "Hủy");
        if (!confirm) return;

        await RunAsync(async () =>
        {
            var srcId = _currentOrder!.OrderId;
            CancelAutoKitchenTimer(srcId);
            var result = await _api.SplitOrderAsync(srcId, target.TableId,
                selected.Select(l => l.Source.Id).ToArray());
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
            await DisplayAlert("Không thể hủy bàn", "Bàn đang có món. Vui lòng hủy hết món trước.", "OK");
            return;
        }

        var tableName = _currentTable.TableName;
        var orderId = _currentOrder.OrderId;
        var confirm = await DisplayAlert("Hủy bàn", $"Hủy bàn {tableName}?", "Hủy bàn", "Không");
        if (!confirm) return;

        await RunAsync(async () =>
        {
            await _api.CancelOrderAsync(orderId);
            CancelAutoKitchenTimer(orderId);
            ClearCurrentOrder();
            await LoadTablesAsync();
            SwitchTab(0);
        }, $"Đã hủy {tableName}.", "Hủy bàn thất bại");
    }

    private async Task<bool> HasCurrentTableOrderAsync(string action, string emptyMsg)
    {
        if (_currentTable is null || _currentOrder is null)
        {
            await DisplayAlert("Chưa chọn bàn", $"Vui lòng chọn bàn trước khi {action}.", "OK");
            return false;
        }

        if (_currentOrder.Lines.Count == 0)
        {
            await DisplayAlert("Đơn trống", emptyMsg, "OK");
            return false;
        }

        return true;
    }

    private async Task<TableDto?> SelectTargetTableAsync(string title, Func<TableDto, bool> predicate, string emptyMsg)
    {
        var srcId = _currentTable?.TableId ?? string.Empty;
        var tables = (await _api.GetTablesAsync())
            .Where(t => !string.Equals(t.TableId, srcId, StringComparison.OrdinalIgnoreCase) && predicate(t))
            .OrderBy(t => GetZoneName(t.ZoneId), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.TableName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (tables.Count == 0)
        {
            await DisplayAlert(title, emptyMsg, "OK");
            return null;
        }

        var labels = tables
            .Select((t, i) => $"{i + 1}. {t.TableName} - {GetZoneName(t.ZoneId)} - {(t.HasOpenOrder ? "Đang phục vụ " + AppUi.Money(t.Total) : "Bàn trống")}")
            .ToArray();
        var selected = await DisplayActionSheet(title, "Hủy", null, labels);
        if (string.IsNullOrWhiteSpace(selected) || selected == "Hủy") return null;
        var idx = Array.IndexOf(labels, selected);
        return idx >= 0 ? tables[idx] : null;
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
        var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                var lineTotal = new Label { TextColor = AppUi.Blue, FontAttributes = FontAttributes.Bold, FontSize = AppUi.S(12) };
                lineTotal.SetBinding(Label.TextProperty, nameof(OrderLineCard.LineTotal));
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
                grid.Add(lineTotal, 2, 0);
                Grid.SetRowSpan(lineTotal, 2);
                var card = AppUi.CardView(grid, 8);
#if IOS
                var tap = new TapGestureRecognizer();
                tap.Tapped += (sender, _) =>
                {
                    if (sender is BindableObject bo && bo.BindingContext is OrderLineCard line)
                    {
                        var sel = selectedIds.Contains(line.Source.Id);
                        if (sel) { selectedIds.Remove(line.Source.Id); check.IsVisible = false; card.Stroke = AppUi.Border; card.StrokeThickness = 1; card.BackgroundColor = AppUi.Surface; }
                        else { selectedIds.Add(line.Source.Id); check.IsVisible = true; card.Stroke = AppUi.Blue; card.StrokeThickness = 2; card.BackgroundColor = AppUi.BlueSoft; }
                    }
                };
                card.GestureRecognizers.Add(tap);
#endif
                return card;
            })
        };

        var cancel = new Button { Text = "Hủy", BackgroundColor = AppUi.BlueSoft, TextColor = AppUi.Blue, FontAttributes = FontAttributes.Bold, CornerRadius = 12, HeightRequest = AppUi.S(34), FontSize = AppUi.S(11) };
        var done = new Button { Text = "Tiếp tục", BackgroundColor = AppUi.Blue, TextColor = Colors.White, FontAttributes = FontAttributes.Bold, CornerRadius = 12, HeightRequest = AppUi.S(34), FontSize = AppUi.S(11) };
        cancel.Clicked += async (_, _) => { completed = true; completion.TrySetResult(null); await Navigation.PopModalAsync(); };
        done.Clicked += async (_, _) =>
        {
#if IOS
            var sel = lines.Where(l => selectedIds.Contains(l.Source.Id)).ToList();
#else
            var sel = list.SelectedItems.OfType<OrderLineCard>().ToList();
#endif
            if (sel.Count == 0) { await DisplayAlert("Tách bàn", "Vui lòng chọn ít nhất 1 món.", "OK"); return; }
            completed = true;
            completion.TrySetResult(sel);
            await Navigation.PopModalAsync();
        };

        var footer = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
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
                            new Label { Text = "Chọn món cần tách", TextColor = AppUi.Ink, FontSize = AppUi.S(15), FontAttributes = FontAttributes.Bold },
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
        page.Disappearing += (_, _) => { if (!completed) completion.TrySetResult(null); };
        await Navigation.PushModalAsync(page);
        return await completion.Task;
    }

    private async Task FinalizeTableMutationAsync(string targetTableId, string targetOrderId)
    {
        _currentOrder = await _api.GetOrderAsync(targetOrderId);
        await LoadTablesAsync();
        _currentTable = _allTables
            .Select(t => t.Source)
            .FirstOrDefault(t => string.Equals(t.TableId, targetTableId, StringComparison.OrdinalIgnoreCase))
            ?? _currentTable;
        ApplyOrder();
        ScheduleAutoKitchenPrintIfNeeded();
    }

    // ── Auto kitchen print ────────────────────────────────────────────────

    private void ScheduleAutoKitchenPrintIfNeeded()
    {
        if (!_printSettings.AutoKitchenPrintEnabled || _currentOrder is null) return;
        var orderId = _currentOrder.OrderId;
        if (string.IsNullOrWhiteSpace(orderId)) return;
        if (!_currentOrder.Lines.Any(l => l.KitchenPendingQuantity > 0)) { CancelAutoKitchenTimer(orderId); return; }

        lock (_autoKitchenLock)
        {
            if (_autoKitchenPrintTimers.ContainsKey(orderId)) return;
            var cts = new CancellationTokenSource();
            _autoKitchenPrintTimers[orderId] = cts;
            _ = AutoPrintKitchenAfterDelayAsync(orderId, TimeSpan.FromMinutes(_printSettings.AutoKitchenPrintDelayMinutes), cts.Token);
        }
    }

    private async Task AutoPrintKitchenAfterDelayAsync(string orderId, TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
            var order = await _api.GetOrderAsync(orderId, token);
            if (!order.Lines.Any(l => l.KitchenPendingQuantity > 0)) return;
            var result = await _api.PrintOrderAsync(orderId, "kitchen", token);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                string? warn = null;
                if (string.Equals(_currentOrder?.OrderId, orderId, StringComparison.OrdinalIgnoreCase))
                    warn = await TryRefreshCurrentOrderAndTablesAsync();
                else
                    warn = await TryLoadTablesAfterPrintAsync();

                await DisplayAlert(
                    result.Printed ? "Tự động in chế biến thành công" : "Tự động in chế biến chưa in",
                    WithRefreshWarning(result.Printed
                        ? "Đã tự động in phiếu bar/bếp cho các món chưa in."
                        : "Không có món mới cần in bar/bếp.", warn),
                    "OK");
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await DisplayAlert("Tự động in chế biến thất bại", AppUi.ToVietnameseError(ex), "OK"));
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
                cts = existing;
        }
        cts?.Cancel();
        cts?.Dispose();
    }

    private void RemoveAutoKitchenTimer(string orderId)
    {
        lock (_autoKitchenLock)
        {
            if (_autoKitchenPrintTimers.Remove(orderId, out var cts))
                cts.Dispose();
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
        foreach (var t in timers) { t.Cancel(); t.Dispose(); }
    }

    // ── Logout ────────────────────────────────────────────────────────────

    private async Task ConfirmLogoutAsync()
    {
        var confirm = await DisplayAlert("Đăng xuất", "Quay lại màn hình đăng nhập?", "Đăng xuất", "Hủy");
        if (!confirm) return;
        _loginSettings.DisableAutoLogin();
        CancelPendingTableRefresh();
        CancelAllAutoKitchenTimers();
        try { await _api.LogoutAsync(); } catch { }
        await Navigation.PopToRootAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task MarkTappedAsync(Border card)
    {
#if IOS
        await card.ScaleTo(0.98, 55, Easing.CubicOut);
        await card.ScaleTo(1, 85, Easing.CubicOut);
#else
        await Task.CompletedTask;
#endif
    }

    private async Task RunAsync(Func<Task> action, string success, string errorTitle = "Lỗi")
    {
        if (_isBusy) return;
        _isBusy = true;
        _busy.IsVisible = true;
        _busy.IsRunning = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await DisplayAlert(errorTitle, AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            _busy.IsRunning = false;
            _busy.IsVisible = false;
            _isBusy = false;
        }
    }
}
