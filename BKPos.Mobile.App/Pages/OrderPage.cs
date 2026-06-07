using System.Collections.ObjectModel;
using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Pages;

public sealed class OrderPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly TableDto _table;
    private readonly ObservableCollection<ProductDto> _products = [];
    private readonly ObservableCollection<OrderLineCard> _lines = [];
    private readonly Picker _categoryPicker = new() { Title = "Tất cả nhóm món" };
    private readonly Entry _search = AppUi.Entry("Tìm món");
    private readonly CollectionView _productList;
    private readonly CollectionView _lineList;
    private readonly Label _status = AppUi.Subtitle("Sẵn sàng nhận món.");
    private readonly Label _total = new() { FontSize = AppUi.S(18), FontAttributes = FontAttributes.Bold, TextColor = AppUi.Coffee };
    private readonly Label _version = AppUi.Subtitle(string.Empty);
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Accent };
    private readonly IDispatcherTimer _pollTimer;
    private List<ProductDto> _allProducts = [];
    private OrderDto _order;
    private long _lastVersion;

    public OrderPage(ApiClient api, TableDto table, OrderDto order)
    {
        _api = api;
        _table = table;
        _order = order;
        Title = table.TableName;
        BackgroundColor = AppUi.Background;
        _productList = BuildProductList();
        _lineList = BuildLineList();
        ToolbarItems.Add(new ToolbarItem("Thanh toán", null, async () => await OpenPaymentAsync()));
        ToolbarItems.Add(new ToolbarItem("In", null, async () => await ShowPrintMenuAsync()));
        Content = AppKeyboardHost.Wrap(BuildContent());

        _pollTimer = Dispatcher.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(3);
        _pollTimer.Tick += async (_, _) => await PollVersionAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _pollTimer.Start();
        await LoadProductsAsync();
        ApplyOrder(_order);
    }

    protected override void OnDisappearing()
    {
        _pollTimer.Stop();
        base.OnDisappearing();
    }

    private View BuildContent()
    {
        _categoryPicker.SelectedIndexChanged += (_, _) => ApplyProductFilter();
        _search.TextChanged += (_, _) => ApplyProductFilter();

        var add = AppUi.PrimaryButton("Thêm món");
        add.Clicked += async (_, _) => await AddSelectedProductAsync();

        var remove = AppUi.SecondaryButton("Xóa dòng");
        remove.Clicked += async (_, _) => await RemoveSelectedLineAsync();

        var actions = AppUi.SecondaryButton("Chuyển / Tách / Gộp");
        actions.Clicked += async (_, _) => await ShowOrderActionsAsync();

        var cancel = AppUi.SecondaryButton("Hủy đơn");
        cancel.TextColor = AppUi.Danger;
        cancel.Clicked += async (_, _) => await CancelOrderAsync();

        var pay = AppUi.PrimaryButton("Thanh toán");
        pay.Clicked += async (_, _) => await OpenPaymentAsync();

        var print = AppUi.SecondaryButton("In phiếu");
        print.Clicked += async (_, _) => await ShowPrintMenuAsync();

        var actionGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = (int)AppUi.S(8)
        };
        actionGrid.Add(actions, 0);
        actionGrid.Add(print, 1);
        actionGrid.Add(pay, 2);
        actionGrid.Add(cancel, 3);

        var productContent = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
            },
            RowSpacing = AppUi.S(10)
        };
        productContent.Add(new Label { Text = "Món bán", FontSize = AppUi.S(15), FontAttributes = FontAttributes.Bold, TextColor = AppUi.Ink }, 0, 0);
        productContent.Add(_categoryPicker, 0, 1);
        productContent.Add(_search, 0, 2);
        productContent.Add(_productList, 0, 3);
        productContent.Add(add, 0, 4);
        var productPanel = AppUi.CardView(productContent, 10);

        var orderContent = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
            RowSpacing = AppUi.S(8)
        };
        orderContent.Add(new Label { Text = "Đơn hiện tại", FontSize = AppUi.S(15), FontAttributes = FontAttributes.Bold, TextColor = AppUi.Ink }, 0, 0);
        orderContent.Add(new Label { Text = _table.TableName, FontAttributes = FontAttributes.Bold, TextColor = AppUi.Muted, FontSize = AppUi.S(13) }, 0, 1);
        orderContent.Add(_version, 0, 2);
        orderContent.Add(_lineList, 0, 3);
        orderContent.Add(remove, 0, 4);
        orderContent.Add(_total, 0, 5);
        orderContent.Add(actionGrid, 0, 6);
        orderContent.Add(new HorizontalStackLayout { Spacing = 8, Children = { _busy, _status } }, 0, 7);
        var orderPanel = AppUi.CardView(orderContent, 10);

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = (int)AppUi.S(12)
        };
        body.Add(productPanel, 0);
        body.Add(orderPanel, 1);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Padding = new Thickness(AppUi.S(14)),
            RowSpacing = AppUi.S(10)
        };
        root.Add(new VerticalStackLayout
        {
            Spacing = AppUi.S(3),
            Children =
            {
                AppUi.Title(_table.TableName),
                AppUi.Subtitle("Nhận món, xử lý bàn, thanh toán và in phiếu.")
            }
        }, 0, 0);
        root.Add(body, 0, 1);
        return root;
    }

    private CollectionView BuildProductList()
    {
        var list = new CollectionView
        {
            ItemsSource = _products,
            SelectionMode = SelectionMode.Single,
            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = (int)AppUi.S(8), VerticalItemSpacing = (int)AppUi.S(8) },
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { FontAttributes = FontAttributes.Bold, TextColor = AppUi.Ink, LineBreakMode = LineBreakMode.TailTruncation };
                name.SetBinding(Label.TextProperty, nameof(ProductDto.Name));
                var price = new Label { TextColor = AppUi.Coffee, FontAttributes = FontAttributes.Bold };
                price.SetBinding(Label.TextProperty, new Binding(nameof(ProductDto.Price), stringFormat: "{0:N0}đ"));
                return AppUi.CardView(new VerticalStackLayout { Spacing = 8, Children = { name, price } }, 12);
            })
        };
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is ProductDto)
            {
                await AddSelectedProductAsync();
                list.SelectedItem = null;
            }
        };
        return list;
    }

    private CollectionView BuildLineList()
    {
        return new CollectionView
        {
            ItemsSource = _lines,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { FontAttributes = FontAttributes.Bold, TextColor = AppUi.Ink };
                name.SetBinding(Label.TextProperty, nameof(OrderLineCard.ProductName));
                var summary = new Label { TextColor = AppUi.Muted };
                summary.SetBinding(Label.TextProperty, nameof(OrderLineCard.Summary));
                return AppUi.CardView(new VerticalStackLayout { Spacing = 4, Children = { name, summary } }, 12);
            })
        };
    }

    private async Task LoadProductsAsync()
    {
        if (_allProducts.Count > 0)
        {
            return;
        }

        _allProducts = (await _api.GetProductsAsync()).ToList();
        var categories = _allProducts
            .Select(product => product.CategoryName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();
        _categoryPicker.Items.Clear();
        _categoryPicker.Items.Add("Tất cả nhóm món");
        foreach (var category in categories)
        {
            _categoryPicker.Items.Add(category);
        }

        _categoryPicker.SelectedIndex = 0;
        ApplyProductFilter();
    }

    private void ApplyProductFilter()
    {
        var category = _categoryPicker.SelectedIndex > 0 ? _categoryPicker.Items[_categoryPicker.SelectedIndex] : string.Empty;
        var keyword = (_search.Text ?? string.Empty).Trim();
        var filtered = _allProducts.Where(product =>
            (string.IsNullOrWhiteSpace(category) || string.Equals(product.CategoryName, category, StringComparison.OrdinalIgnoreCase))
            && AppUi.ContainsSearch(product.Name, keyword));

        _products.Clear();
        foreach (var product in filtered)
        {
            _products.Add(product);
        }
    }

    private async Task AddSelectedProductAsync()
    {
        if (_productList.SelectedItem is not ProductDto product)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var existing = _order.Lines.FirstOrDefault(line =>
                string.Equals(line.ProductId, product.ExternalId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(line.ProductId, product.Id.ToString(), StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                await _api.AddLineAsync(_order.OrderId, product, 1);
            }
            else
            {
                await _api.UpdateLineAsync(_order.OrderId, existing.Id, existing.Quantity + 1, existing.Note ?? string.Empty);
            }

            await ReloadOrderAsync();
        }, "Đã thêm món.");
    }

    private async Task RemoveSelectedLineAsync()
    {
        if (_lineList.SelectedItem is not OrderLineCard line)
        {
            _status.Text = "Chọn dòng món cần xóa.";
            return;
        }

        await RunAsync(async () =>
        {
            await _api.RemoveLineAsync(_order.OrderId, line.Source.Id);
            await ReloadOrderAsync();
        }, "Đã xóa dòng món.");
    }

    private async Task ShowOrderActionsAsync()
    {
        var action = await DisplayActionSheetAsync("Xử lý bàn", "Đóng", null, "Chuyển bàn", "Tách dòng đang chọn", "Gộp vào bàn khác");
        if (action is null or "Đóng")
        {
            return;
        }

        var tables = (await _api.GetTablesAsync()).Select(table => new TableCard(table)).ToList();
        if (action == "Chuyển bàn")
        {
            var target = await SelectTableAsync(tables.Where(table => !table.Source.HasOpenOrder), "Chọn bàn chuyển đến");
            if (target is not null)
            {
                await RunAsync(async () =>
                {
                    await _api.TransferOrderAsync(_order.OrderId, target.Source.TableId);
                    await ReloadOrderAsync();
                }, $"Đã chuyển sang {target.Name}.");
            }
        }
        else if (action == "Tách dòng đang chọn")
        {
            if (_lineList.SelectedItem is not OrderLineCard line)
            {
                _status.Text = "Chọn dòng món cần tách.";
                return;
            }

            var target = await SelectTableAsync(tables.Where(table => !table.Source.HasOpenOrder), "Chọn bàn nhận dòng tách");
            if (target is not null)
            {
                await RunAsync(async () =>
                {
                    await _api.SplitOrderAsync(_order.OrderId, target.Source.TableId, [line.Source.Id]);
                    await ReloadOrderAsync();
                }, $"Đã tách sang {target.Name}.");
            }
        }
        else if (action == "Gộp vào bàn khác")
        {
            var target = await SelectTableAsync(
                tables.Where(table => table.Source.HasOpenOrder
                                      && !string.IsNullOrWhiteSpace(table.Source.OrderId)
                                      && !string.Equals(table.Source.OrderId, _order.OrderId, StringComparison.OrdinalIgnoreCase)),
                "Chọn bàn nhận gộp");
            if (target is not null && !string.IsNullOrWhiteSpace(target.Source.OrderId))
            {
                await RunAsync(async () =>
                {
                    await _api.MergeOrderAsync(_order.OrderId, target.Source.OrderId);
                    _order = await _api.GetOrderAsync(target.Source.OrderId);
                    ApplyOrder(_order);
                }, $"Đã gộp vào {target.Name}.");
            }
        }
    }

    private async Task<TableCard?> SelectTableAsync(IEnumerable<TableCard> source, string title)
    {
        var tables = source.ToList();
        if (tables.Count == 0)
        {
            _status.Text = "Không có bàn phù hợp.";
            return null;
        }

        var labels = tables.Select(table => $"{table.Name} | {table.Status} | {table.Source.TableId}").ToArray();
        var selected = await DisplayActionSheetAsync(title, "Đóng", null, labels);
        return tables.FirstOrDefault(table => selected?.EndsWith(table.Source.TableId, StringComparison.OrdinalIgnoreCase) == true);
    }

    private async Task OpenPaymentAsync()
    {
        await Navigation.PushModalAsync(new PaymentPage(_api, _order, async paid =>
        {
            if (paid)
            {
                await Navigation.PopAsync();
            }
            else
            {
                await ReloadOrderAsync();
            }
        }));
    }

    private async Task ShowPrintMenuAsync()
    {
        var action = await DisplayActionSheetAsync("In phiếu", "Đóng", null, "Bill", "Bếp", "Bar", "Tem ly");
        var type = action switch
        {
            "Bill" => "bill",
            "Bếp" => "kitchen",
            "Bar" => "bar",
            "Tem ly" => "cup-label",
            _ => null
        };

        if (type is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _api.PrintOrderAsync(_order.OrderId, type);
            _status.Text = result.Deduplicated ? "Đã chặn in trùng trong 60 giây." : $"Đã gửi lệnh in {action}.";
        }, "Đã gửi lệnh in.");
    }

    private async Task CancelOrderAsync()
    {
        var confirm = await DisplayAlertAsync("Hủy đơn", "Hủy đơn hiện tại và trả bàn về trống?", "Hủy đơn", "Không");
        if (!confirm)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _api.CancelOrderAsync(_order.OrderId);
            await Navigation.PopAsync();
        }, "Đã hủy đơn.");
    }

    private async Task PollVersionAsync()
    {
        try
        {
            var version = await _api.GetOrderVersionAsync(_order.OrderId);
            if (_lastVersion > 0 && version.Version != _lastVersion)
            {
                _version.Text = "Có thay đổi từ thiết bị khác, đã tải lại.";
                await ReloadOrderAsync();
            }

            _lastVersion = version.Version;
        }
        catch
        {
            // Polling is best-effort; visible operations still show detailed errors.
        }
    }

    private async Task ReloadOrderAsync()
    {
        ApplyOrder(await _api.GetOrderAsync(_order.OrderId));
    }

    private void ApplyOrder(OrderDto order)
    {
        _order = order;
        _lines.Clear();
        foreach (var line in order.Lines)
        {
            _lines.Add(new OrderLineCard(line));
        }

        _total.Text = $"Tổng: {AppUi.Money(order.Total)}";
        _version.Text = order.ModifiedAt is null ? string.Empty : $"Cập nhật: {order.ModifiedAt:HH:mm:ss}";
    }

    private async Task RunAsync(Func<Task> action, string success)
    {
        _busy.IsRunning = true;
        try
        {
            await action();
            if (!string.IsNullOrWhiteSpace(success))
            {
                _status.Text = success;
            }
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
}


