using System.Collections.ObjectModel;
using BKPos.Core.Interfaces;
using BKPos.Mobile.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace BKPos.Mobile.App.Pages;

public sealed class TablesPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly IHardwareIdProvider _hardwareIdProvider;
    private readonly ObservableCollection<ZoneDto> _zones = [];
    private readonly ObservableCollection<TableCard> _tables = [];
    private readonly Picker _zonePicker = new() { Title = "Tất cả khu vực", ItemDisplayBinding = new Binding(nameof(ZoneDto.Name)) };
    private readonly CollectionView _tableList;
    private readonly Label _status = AppUi.Subtitle("Chọn bàn để bắt đầu nhận order.");
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Accent };

    public TablesPage(ApiClient api, IHardwareIdProvider hardwareIdProvider)
    {
        _api = api;
        _hardwareIdProvider = hardwareIdProvider;
        Title = "Bàn";
        BackgroundColor = AppUi.Background;
        _tableList = BuildTableList();
        ToolbarItems.Add(new ToolbarItem("Cài đặt", null, async () => await Navigation.PushAsync(new SettingsPage(_api, _hardwareIdProvider))));
        Content = BuildContent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private View BuildContent()
    {
        _zonePicker.ItemsSource = _zones;
        _zonePicker.SelectedIndexChanged += async (_, _) => await LoadTablesAsync();

        var refresh = AppUi.SecondaryButton("Tải lại");
        refresh.Clicked += async (_, _) => await LoadAsync();

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        header.Add(AppUi.Title("Sơ đồ bàn"), 0);
        header.Add(refresh, 1);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Padding = new Thickness(AppUi.S(18), AppUi.S(14)),
            RowSpacing = AppUi.S(12)
        };
        grid.Add(header, 0, 0);
        grid.Add(AppUi.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                AppUi.Subtitle("Bàn xanh là bàn trống, bàn cam là đang phục vụ."),
                _zonePicker
            }
        }), 0, 1);
        grid.Add(_tableList, 0, 2);
        grid.Add(AppUi.CardView(new HorizontalStackLayout { Spacing = 8, Children = { _busy, _status } }, 12), 0, 3);
        return grid;
    }

    private CollectionView BuildTableList()
    {
        var list = new CollectionView
        {
            ItemsSource = _tables,
            SelectionMode = SelectionMode.Single,
            ItemsLayout = new GridItemsLayout(3, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = AppUi.S(10), VerticalItemSpacing = AppUi.S(10) },
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { FontSize = AppUi.S(15), FontAttributes = FontAttributes.Bold, TextColor = AppUi.Ink };
                name.SetBinding(Label.TextProperty, nameof(TableCard.Name));
                var status = new Label { TextColor = AppUi.Muted, FontSize = AppUi.S(12) };
                status.SetBinding(Label.TextProperty, nameof(TableCard.Status));
                var total = new Label { FontAttributes = FontAttributes.Bold, TextColor = AppUi.Coffee, FontSize = AppUi.S(12) };
                total.SetBinding(Label.TextProperty, nameof(TableCard.Total));
                var card = new Border
                {
                    Padding = AppUi.S(12),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 22 },
                    Content = new VerticalStackLayout { Spacing = 8, Children = { name, status, total } }
                };
                card.SetBinding(Border.BackgroundColorProperty, nameof(TableCard.Background));
                return card;
            })
        };
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is TableCard table)
            {
                list.SelectedItem = null;
                await OpenTableAsync(table);
            }
        };
        return list;
    }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            _zones.Clear();
            foreach (var zone in await _api.GetZonesAsync())
            {
                _zones.Add(zone);
            }

            await LoadTablesAsync();
        }, "Đã tải sơ đồ bàn.");
    }

    private async Task LoadTablesAsync()
    {
        var zone = _zonePicker.SelectedItem as ZoneDto;
        _tables.Clear();
        foreach (var table in await _api.GetTablesAsync(zone?.ExternalId))
        {
            _tables.Add(new TableCard(table));
        }
    }

    private async Task OpenTableAsync(TableCard table)
    {
        await RunAsync(async () =>
        {
            OrderDto order;
            if (table.Source.HasOpenOrder && !string.IsNullOrWhiteSpace(table.Source.OrderId))
            {
                order = await _api.GetOrderAsync(table.Source.OrderId);
            }
            else
            {
                var opened = await _api.OpenTableAsync(table.Source.TableId);
                order = opened.Order ?? await _api.GetOrderAsync(opened.OrderId);
            }

            await Navigation.PushAsync(new OrderPage(_api, table.Source, order));
        }, $"Đã mở {table.Name}.");
    }

    private async Task RunAsync(Func<Task> action, string success)
    {
        _busy.IsRunning = true;
        try
        {
            await action();
            _status.Text = success;
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
