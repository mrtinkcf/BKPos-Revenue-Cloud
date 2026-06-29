using BKPos.Mobile.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace BKPos.Mobile.App.Pages;

public sealed class PrintedBillDetailPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly PrintedBillDetailResponseDto _detail;

    public PrintedBillDetailPage(ApiClient api, PrintedBillDetailResponseDto detail)
    {
        _api = api;
        _detail = detail;
        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = AppUi.Background;
        Content = BuildContent();
    }

    private View BuildContent()
    {
        var title = new Label
        {
            Text = $"Bill {_detail.Bill.TableName}",
            TextColor = Colors.White,
            FontSize = AppUi.S(17),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var back = AppUi.IconButton("‹", Colors.White);
        back.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var header = new Grid
        {
            BackgroundColor = AppUi.Navy,
            Padding = new Thickness(AppUi.S(8), AppUi.S(8)),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
        };
        header.Add(back, 0, 0);
        header.Add(title, 1, 0);

        var paidAt = _detail.Bill.PaidAt?.ToString("HH:mm dd/MM/yyyy")
            ?? _detail.Bill.BusinessDate?.ToString("dd/MM/yyyy")
            ?? string.Empty;

        var summary = AppUi.CardView(new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(paidAt) ? "Bill đã thanh toán" : $"Đã thanh toán: {paidAt}",
                    TextColor = AppUi.Muted,
                    FontSize = AppUi.S(12)
                },
                new Label
                {
                    Text = "Tổng tiền: " + AppUi.Money(_detail.Bill.Total),
                    TextColor = AppUi.Blue,
                    FontSize = AppUi.S(18),
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.End
                }.Row(1),
                new Label
                {
                    Text = _detail.Bill.Discount > 0 ? "Giảm giá: " + AppUi.Money(_detail.Bill.Discount) : string.Empty,
                    TextColor = AppUi.Warning,
                    FontSize = AppUi.S(11),
                    IsVisible = _detail.Bill.Discount > 0,
                    HorizontalTextAlignment = TextAlignment.End
                }.Row(2)
            }
        }, 10);

        var table = BuildLineTable();
        var print = AppUi.PrimaryButton("In lại bill");
        print.Clicked += async (_, _) => await ReprintAsync(print);
        var close = AppUi.SecondaryButton("Đóng");
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = AppUi.S(8)
        };
        actions.Add(close, 0, 0);
        actions.Add(print, 1, 0);

        var body = new Grid
        {
            Padding = new Thickness(AppUi.S(8)),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = AppUi.S(8)
        };
        body.Add(summary, 0, 0);
        body.Add(table, 0, 1);
        body.Add(actions, 0, 2);

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
        return root;
    }

    private View BuildLineTable()
    {
        var rows = _detail.Lines
            .Select((line, index) => new PrintedBillLineCard(index + 1, line))
            .ToList();

        var header = new Grid
        {
            BackgroundColor = AppUi.Navy,
            Padding = new Thickness(AppUi.S(8), AppUi.S(6)),
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
                    Padding = new Thickness(AppUi.S(8), AppUi.S(7)),
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

    private static ColumnDefinitionCollection BuildLineColumns() =>
    [
        new ColumnDefinition(GridLength.Star),
        new ColumnDefinition(new GridLength(AppUi.S(34))),
        new ColumnDefinition(new GridLength(AppUi.S(46))),
        new ColumnDefinition(new GridLength(AppUi.S(72))),
        new ColumnDefinition(new GridLength(AppUi.S(78)))
    ];

    private static Label HeaderLabel(string text) => new()
    {
        Text = text,
        TextColor = Colors.White,
        FontSize = AppUi.S(11),
        FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private static Label CellLabel(TextAlignment alignment, bool bold = false) => new()
    {
        TextColor = AppUi.Ink,
        FontSize = AppUi.S(11),
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        HorizontalTextAlignment = alignment,
        VerticalTextAlignment = TextAlignment.Center,
        LineBreakMode = LineBreakMode.TailTruncation
    };

    private async Task ReprintAsync(Button button)
    {
        button.IsEnabled = false;
        try
        {
            var result = await _api.PrintOrderAsync(_detail.Bill.OrderId, "bill", force: true);
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
            button.IsEnabled = true;
        }
    }

    private sealed record PrintedBillLineCard(int Index, PrintedBillLineDto Source)
    {
        public string ProductName => Source.ProductName;
        public string QuantityText => Source.Quantity.ToString();
        public string UnitName => Source.UnitName;
        public string UnitPriceText => AppUi.Money(Source.UnitPrice);
        public string LineTotalText => AppUi.Money(Source.LineTotal);
    }
}

internal static class PrintedBillDetailPageGridExtensions
{
    public static T Row<T>(this T view, int row) where T : BindableObject
    {
        Grid.SetRow(view, row);
        return view;
    }
}
