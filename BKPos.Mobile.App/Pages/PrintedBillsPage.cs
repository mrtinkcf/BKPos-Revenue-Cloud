using System.Collections.ObjectModel;
using System.ComponentModel;
using BKPos.Mobile.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace BKPos.Mobile.App.Pages;

public sealed class PrintedBillsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly ObservableCollection<BillListItem> _bills = [];
    private readonly CollectionView _list;
    private readonly ContentView _detailHost = new();
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Blue, IsVisible = false };
    private readonly Button _printButton;
    private readonly Label _totalLabel = new()
    {
        Text = string.Empty,
        TextColor = AppUi.Blue,
        FontSize = AppUi.S(15),
        FontAttributes = FontAttributes.Bold,
        VerticalTextAlignment = TextAlignment.Center
    };
    private PrintedBillDetailResponseDto? _selectedDetail;
    private BillListItem? _selectedItem;
    private bool _isLoading;

    public PrintedBillsPage(ApiClient api)
    {
        _api = api;
        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = AppUi.Background;
        _list = BuildList();
        _printButton = new Button
        {
            Text = "In lại bill",
            BackgroundColor = AppUi.Blue,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = AppUi.S(11),
            CornerRadius = 8,
            HeightRequest = AppUi.S(30),
            MinimumHeightRequest = 0,
            Padding = new Thickness(AppUi.S(10), 0),
            IsEnabled = false
        };
        _printButton.Clicked += async (_, _) => await ReprintAsync();
        Content = BuildContent();
        RenderEmptyDetail("Chọn bill để xem chi tiết.");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private View BuildContent()
    {
        var back = new Label
        {
            Text = "‹ Quay về",
            TextColor = Colors.White,
            FontSize = AppUi.S(12),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        var backTap = new TapGestureRecognizer();
        backTap.Tapped += async (_, _) => await Navigation.PopModalAsync();
        back.GestureRecognizers.Add(backTap);

        var header = new Grid
        {
            BackgroundColor = AppUi.Navy,
            Padding = new Thickness(AppUi.S(10), AppUi.S(4)),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        header.Add(new Label
        {
            Text = "Bill đã in hôm nay",
            TextColor = Colors.White,
            FontSize = AppUi.S(14),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        header.Add(back, 1, 0);

        // Footer của panel phải: tổng tiền bên trái + nút in lại bên phải
        var footer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = AppUi.S(8)
        };
        footer.Add(_totalLabel, 0, 0);
        footer.Add(_printButton, 1, 0);

        // Panel trái: danh sách bill
        var leftInner = new Grid
        {
            Padding = new Thickness(AppUi.S(6))
        };
        leftInner.Add(_list);
        var left = AppUi.CardView(leftInner, 8);

        // Panel phải: chi tiết + footer tổng
        var rightInner = new Grid
        {
            Padding = new Thickness(AppUi.S(6)),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = AppUi.S(6)
        };
        rightInner.Add(_detailHost, 0, 0);
        rightInner.Add(footer, 0, 1);
        var right = AppUi.CardView(rightInner, 8);

        var body = new Grid
        {
            Padding = new Thickness(AppUi.S(8)),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(0.82, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.18, GridUnitType.Star))
            },
            ColumnSpacing = AppUi.S(8)
        };
        body.Add(left, 0, 0);
        body.Add(right, 1, 0);

        _busy.HorizontalOptions = LayoutOptions.End;
        _busy.VerticalOptions = LayoutOptions.Start;
        _busy.Margin = new Thickness(0, AppUi.S(46), AppUi.S(10), 0);
        _busy.ZIndex = 10;

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(header, 0, 0);
        root.Add(body, 0, 1);
        root.Add(_busy, 0, 1);
        return root;
    }

    private CollectionView BuildList()
    {
        var list = new CollectionView
        {
            ItemsSource = _bills,
            SelectionMode = SelectionMode.None,
            EmptyView = new Label
            {
                Text = "Chưa có bill đã in hôm nay.",
                TextColor = AppUi.Muted,
                FontSize = AppUi.S(12),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            },
            ItemTemplate = new DataTemplate(() =>
            {
                var tableName = new Label
                {
                    TextColor = AppUi.Ink,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = AppUi.S(12),
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                tableName.SetBinding(Label.TextProperty, nameof(BillListItem.TableName));

                var subText = new Label
                {
                    TextColor = AppUi.Muted,
                    FontSize = AppUi.S(10),
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                subText.SetBinding(Label.TextProperty, nameof(BillListItem.SubText));

                var total = new Label
                {
                    TextColor = AppUi.Blue,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = AppUi.S(12),
                    HorizontalTextAlignment = TextAlignment.End,
                    VerticalTextAlignment = TextAlignment.Center
                };
                total.SetBinding(Label.TextProperty, nameof(BillListItem.TotalText));

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = AppUi.S(6)
                };
                grid.Add(new VerticalStackLayout { Spacing = 2, Children = { tableName, subText } }, 0, 0);
                grid.Add(total, 1, 0);

                var card = new Border
                {
                    Padding = new Thickness(AppUi.S(8), AppUi.S(5)),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Margin = new Thickness(0, 0, 0, AppUi.S(4)),
                    Content = grid
                };
                card.SetBinding(Border.BackgroundColorProperty, nameof(BillListItem.CardBackground));
                card.SetBinding(Border.StrokeProperty, nameof(BillListItem.CardStroke));
                card.SetBinding(Border.StrokeThicknessProperty, nameof(BillListItem.CardStrokeThickness));

                var tap = new TapGestureRecognizer();
                tap.Tapped += async (sender, _) =>
                {
                    if (sender is BindableObject bo && bo.BindingContext is BillListItem item)
                        await SelectBillAsync(item);
                };
                card.GestureRecognizers.Add(tap);
                return card;
            })
        };
        return list;
    }

    private async Task LoadAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        _busy.IsVisible = true;
        _busy.IsRunning = true;

        try
        {
            var bills = await _api.GetPrintedBillsTodayAsync();
            _bills.Clear();
            _selectedItem = null;
            foreach (var bill in bills.OrderByDescending(b => b.PaidAt ?? b.BusinessDate ?? b.StartTime))
                _bills.Add(new BillListItem(new PrintedBillCard(bill)));

            if (_bills.Count > 0)
                await SelectBillAsync(_bills[0]);
            else
                RenderEmptyDetail("Chưa có bill để hiển thị.");
        }
        catch (Exception ex)
        {
            RenderEmptyDetail("Không tải được danh sách bill.");
            await DisplayAlert("Tải bill thất bại", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            _busy.IsRunning = false;
            _busy.IsVisible = false;
            _isLoading = false;
        }
    }

    private async Task SelectBillAsync(BillListItem item)
    {
        if (_selectedItem is not null) _selectedItem.IsSelected = false;
        item.IsSelected = true;
        _selectedItem = item;

        try
        {
            _busy.IsVisible = true;
            _busy.IsRunning = true;
            _selectedDetail = await _api.GetPrintedBillAsync(item.Card.Source.OrderId);
            RenderDetail(_selectedDetail);
        }
        catch (Exception ex)
        {
            RenderEmptyDetail("Không mở được chi tiết bill.");
            await DisplayAlert("Mở bill thất bại", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            _busy.IsRunning = false;
            _busy.IsVisible = false;
        }
    }

    private void RenderEmptyDetail(string message)
    {
        _selectedDetail = null;
        _printButton.IsEnabled = false;
        _totalLabel.Text = string.Empty;
        _detailHost.Content = new Label
        {
            Text = message,
            TextColor = AppUi.Muted,
            FontSize = AppUi.S(13),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
    }

    private void RenderDetail(PrintedBillDetailResponseDto detail)
    {
        _printButton.IsEnabled = true;
        _totalLabel.Text = "Tổng: " + AppUi.Money(detail.Bill.Total);

        var paidAt = detail.Bill.PaidAt?.ToString("HH:mm dd/MM/yyyy")
            ?? detail.Bill.BusinessDate?.ToString("dd/MM/yyyy")
            ?? string.Empty;
        var billName = string.IsNullOrWhiteSpace(detail.Bill.TableName) ? "Mang về" : detail.Bill.TableName;

        var summaryStack = new VerticalStackLayout { Spacing = 2 };
        summaryStack.Add(new Label
        {
            Text = billName,
            TextColor = AppUi.Ink,
            FontAttributes = FontAttributes.Bold,
            FontSize = AppUi.S(13)
        });
        summaryStack.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(paidAt) ? "Đã thanh toán" : paidAt,
            TextColor = AppUi.Muted,
            FontSize = AppUi.S(11)
        });
        if (!string.IsNullOrWhiteSpace(detail.Bill.CashierName))
        {
            summaryStack.Add(new Label
            {
                Text = "Thu ngân: " + detail.Bill.CashierName,
                TextColor = AppUi.Muted,
                FontSize = AppUi.S(11)
            });
        }

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = AppUi.S(6)
        };
        content.Add(AppUi.CardView(summaryStack, 8), 0, 0);
        content.Add(BuildLineTable(detail), 0, 1);
        _detailHost.Content = content;
    }

    private static View BuildLineTable(PrintedBillDetailResponseDto detail)
    {
        var rows = detail.Lines.Select((line, index) => new PrintedBillLineCard(index + 1, line)).ToList();

        var header = new Grid
        {
            BackgroundColor = AppUi.Navy,
            Padding = new Thickness(AppUi.S(7), AppUi.S(5)),
            ColumnDefinitions = BuildLineColumns()
        };
        header.Add(HeaderLabel("Món"), 0, 0);
        header.Add(HeaderLabel("SL"), 1, 0);
        header.Add(HeaderLabel("ĐVT"), 2, 0);
        header.Add(HeaderLabel("Đơn giá"), 3, 0);
        header.Add(HeaderLabel("T.Tiền"), 4, 0);

        var list = new CollectionView
        {
            ItemsSource = rows,
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = CellLabel(TextAlignment.Start);
                name.SetBinding(Label.TextProperty, nameof(PrintedBillLineCard.ProductName));
                var qty = CellLabel(TextAlignment.Center, bold: true);
                qty.SetBinding(Label.TextProperty, nameof(PrintedBillLineCard.QuantityText));
                var unit = CellLabel(TextAlignment.Center);
                unit.SetBinding(Label.TextProperty, nameof(PrintedBillLineCard.UnitName));
                var price = CellLabel(TextAlignment.End);
                price.SetBinding(Label.TextProperty, nameof(PrintedBillLineCard.UnitPriceText));
                var total = CellLabel(TextAlignment.End, bold: true);
                total.SetBinding(Label.TextProperty, nameof(PrintedBillLineCard.LineTotalText));

                var row = new Grid
                {
                    Padding = new Thickness(AppUi.S(7), AppUi.S(6)),
                    ColumnDefinitions = BuildLineColumns()
                };
                row.Add(name, 0, 0);
                row.Add(qty, 1, 0);
                row.Add(unit, 2, 0);
                row.Add(price, 3, 0);
                row.Add(total, 4, 0);

                return new Border
                {
                    Stroke = AppUi.Border,
                    StrokeThickness = 0.5,
                    StrokeShape = new RoundRectangle { CornerRadius = 0 },
                    BackgroundColor = AppUi.Surface,
                    Content = row
                };
            })
        };

        var table = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        table.Add(header, 0, 0);
        table.Add(list, 0, 1);
        return AppUi.CardView(table, 0);
    }

    private async Task ReprintAsync()
    {
        if (_selectedDetail is null) return;
        _printButton.IsEnabled = false;
        try
        {
            var result = await _api.PrintOrderAsync(_selectedDetail.Bill.OrderId, "bill", force: true);
            await DisplayAlert(
                result.Printed ? "Đã in lại bill" : "Chưa in được",
                result.Printed
                    ? "Đã gửi lệnh in lại bill."
                    : "Máy in không in. Kiểm tra cấu hình máy in bill trên BKPos Mobile Server.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("In lại bill thất bại", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            _printButton.IsEnabled = _selectedDetail is not null;
        }
    }

    private static ColumnDefinitionCollection BuildLineColumns() =>
    [
        new ColumnDefinition(GridLength.Star),
        new ColumnDefinition(new GridLength(AppUi.S(34))),
        new ColumnDefinition(new GridLength(AppUi.S(42))),
        new ColumnDefinition(new GridLength(AppUi.S(68))),
        new ColumnDefinition(new GridLength(AppUi.S(74)))
    ];

    private static Label HeaderLabel(string text) => new()
    {
        Text = text,
        TextColor = Colors.White,
        FontSize = AppUi.S(10),
        FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private static Label CellLabel(TextAlignment alignment, bool bold = false) => new()
    {
        TextColor = AppUi.Ink,
        FontSize = AppUi.S(10),
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        HorizontalTextAlignment = alignment,
        VerticalTextAlignment = TextAlignment.Center,
        LineBreakMode = LineBreakMode.TailTruncation
    };

    // ── BillListItem ─────────────────────────────────────────────────────────
    private sealed class BillListItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public BillListItem(PrintedBillCard card) => Card = card;

        public PrintedBillCard Card { get; }

        public string TableName => Card.TableName;
        public string TotalText => Card.TotalText;
        public string SubText
        {
            get
            {
                var cashier = Card.Source.CashierName;
                return string.IsNullOrWhiteSpace(cashier)
                    ? Card.PaidAtText
                    : $"{Card.PaidAtText} · {cashier}";
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardBackground)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardStroke)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardStrokeThickness)));
            }
        }

        public Color CardBackground => _isSelected ? AppUi.BlueSoft : AppUi.Surface;
        public Color CardStroke => _isSelected ? AppUi.Blue : AppUi.Border;
        public double CardStrokeThickness => _isSelected ? 2.0 : 1.0;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ── PrintedBillLineCard ──────────────────────────────────────────────────
    private sealed record PrintedBillLineCard(int Index, PrintedBillLineDto Source)
    {
        public string ProductName => Source.ProductName;
        public string QuantityText => Source.Quantity.ToString();
        public string UnitName => Source.UnitName;
        public string UnitPriceText => AppUi.Money(Source.UnitPrice);
        public string LineTotalText => AppUi.Money(Source.LineTotal);
    }
}
