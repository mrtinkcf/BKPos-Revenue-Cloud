using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using MauiNavigationPage = Microsoft.Maui.Controls.NavigationPage;
using MauiScrollView = Microsoft.Maui.Controls.ScrollView;

namespace BKPos.Revenue.App;

public sealed class InvoiceDetailPage : ContentPage
{
    private readonly InvoiceDetailResponse _invoice;

    public InvoiceDetailPage(InvoiceDetailResponse invoice)
    {
        _invoice = invoice;
        BackgroundColor = AppColors.Surface;
        HideSoftInputOnTapped = true;
        MauiNavigationPage.SetHasNavigationBar(this, false);
        Content = BuildContent();
    }

    private View BuildContent()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        root.Add(BuildHeader(), 0, 0);
        root.Add(new MauiScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = AppUi.PagePadding,
                Spacing = AppUi.S(14),
                Children =
                {
                    BuildSummaryCard(),
                    BuildItemsCard(),
                    BuildPaymentsCard()
                }
            }
        }, 0, 1);

        return root;
    }

    private View BuildHeader()
    {
        var backButton = new Button
        {
            Text = "‹",
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            FontSize = AppUi.S(30),
            FontAttributes = FontAttributes.Bold,
            WidthRequest = AppUi.S(44),
            HeightRequest = AppUi.S(44),
            Padding = Thickness.Zero
        };
        backButton.Clicked += async (_, _) => await Navigation.PopAsync();

        var title = new Label
        {
            Text = "Chi tiết hóa đơn",
            TextColor = Colors.White,
            FontSize = AppUi.S(18),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };

        var subtitle = new Label
        {
            Text = $"{Safe(_invoice.TableName)} • {RevenueApiClient.Money(_invoice.Total)}",
            TextColor = Color.FromArgb("#CBD5E1"),
            FontSize = AppUi.S(12),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(title, 1);
        Grid.SetColumn(subtitle, 1);
        Grid.SetRow(subtitle, 1);

        return new Grid
        {
            BackgroundColor = AppColors.Navy,
            Padding = new Thickness(AppUi.S(8), AppUi.S(10), AppUi.S(14), AppUi.S(10)),
            ColumnSpacing = AppUi.S(8),
            RowSpacing = AppUi.S(2),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Children = { backButton, title, subtitle }
        };
    }

    private View BuildSummaryCard()
    {
        var grid = new Grid
        {
            ColumnSpacing = AppUi.S(12),
            RowSpacing = AppUi.S(8),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };

        grid.Add(InfoBlock("Bàn", Safe(_invoice.TableName)), 0, 0);
        grid.Add(InfoBlock("Trạng thái", StatusLabel(_invoice.Status), StatusColor(_invoice.Status)), 1, 0);
        grid.Add(InfoBlock("Ngày", FormatBusinessDate(_invoice.BusinessDate)), 0, 1);
        grid.Add(InfoBlock("Thu ngân", Safe(_invoice.Cashier)), 1, 1);
        grid.Add(InfoBlock("Tạm tính", RevenueApiClient.Money(_invoice.Subtotal)), 0, 2);
        grid.Add(InfoBlock("Giảm giá", RevenueApiClient.Money(_invoice.Discount), AppColors.Red), 1, 2);
        grid.Add(InfoBlock("Tổng cộng", RevenueApiClient.Money(_invoice.Total), AppColors.Blue), 0, 3);
        grid.Add(InfoBlock("Thanh toán", PaymentLabel(_invoice.PaymentMethod)), 1, 3);

        if (!string.IsNullOrWhiteSpace(_invoice.DiscountNote))
        {
            var note = InfoBlock("Ghi chú giảm giá", _invoice.DiscountNote);
            Grid.SetColumnSpan(note, 2);
            grid.Add(note, 0, 4);
        }

        return Card(grid);
    }

    private View BuildItemsCard()
    {
        var content = new VerticalStackLayout
        {
            Spacing = AppUi.S(12),
            Children =
            {
                new Label
                {
                    Text = "Món trong hóa đơn",
                    TextColor = AppColors.Navy,
                    FontSize = AppUi.S(17),
                    FontAttributes = FontAttributes.Bold
                }
            }
        };

        if (_invoice.Items.Count == 0)
        {
            content.Children.Add(new Label
            {
                Text = "Hóa đơn chưa có chi tiết món.",
                TextColor = AppColors.Muted,
                FontSize = AppUi.S(13)
            });
        }
        else
        {
            content.Children.Add(new MauiScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
                Content = BuildItemsGrid(_invoice.Items)
            });
        }

        content.Children.Add(BuildTotalRow());
        return Card(content);
    }

    private View BuildPaymentsCard()
    {
        var content = new VerticalStackLayout
        {
            Spacing = AppUi.S(10),
            Children =
            {
                new Label
                {
                    Text = "Thanh toán",
                    TextColor = AppColors.Navy,
                    FontSize = AppUi.S(17),
                    FontAttributes = FontAttributes.Bold
                }
            }
        };

        if (_invoice.Payments.Count == 0)
        {
            content.Children.Add(new Label
            {
                Text = "Chưa có thông tin thanh toán.",
                TextColor = AppColors.Muted,
                FontSize = AppUi.S(13)
            });
        }
        else
        {
            foreach (var payment in _invoice.Payments)
            {
                content.Children.Add(new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Children =
                    {
                        new Label
                        {
                            Text = $"{PaymentLabel(payment.Method)} • {FormatDateTime(payment.CreatedAt)}",
                            TextColor = AppColors.Navy,
                            FontSize = AppUi.S(13),
                            LineBreakMode = LineBreakMode.TailTruncation
                        },
                        new Label
                        {
                            Text = RevenueApiClient.Money(payment.Amount),
                            TextColor = AppColors.Blue,
                            FontSize = AppUi.S(14),
                            FontAttributes = FontAttributes.Bold,
                            HorizontalTextAlignment = TextAlignment.End
                        }.AtColumn(1)
                    }
                });
            }
        }

        return Card(content);
    }

    private View BuildTotalRow()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        grid.Add(new Label
        {
            Text = "Tổng chi tiết",
            TextColor = AppColors.Muted,
            FontSize = AppUi.S(13),
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        grid.Add(new Label
        {
            Text = RevenueApiClient.Money(_invoice.Total),
            TextColor = AppColors.Red,
            FontSize = AppUi.S(20),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);

        return grid;
    }

    private static Grid BuildItemsGrid(IReadOnlyList<InvoiceDetailItem> items)
    {
        var grid = new Grid
        {
            ColumnSpacing = 0,
            RowSpacing = 0,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(AppUi.S(42))),
                new ColumnDefinition(new GridLength(AppUi.S(210))),
                new ColumnDefinition(new GridLength(AppUi.S(58))),
                new ColumnDefinition(new GridLength(AppUi.S(64))),
                new ColumnDefinition(new GridLength(AppUi.S(96))),
                new ColumnDefinition(new GridLength(AppUi.S(104))),
                new ColumnDefinition(new GridLength(AppUi.S(180)))
            }
        };

        var headers = new[] { "STT", "Món", "SL", "ĐVT", "Đơn giá", "T.Tiền", "Ghi chú" };
        for (var column = 0; column < headers.Length; column++)
        {
            grid.Add(Cell(headers[column], true, TextAlignment.Center), column, 0);
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var row = i + 1;
            grid.Add(Cell(row.ToString(CultureInfo.InvariantCulture), false, TextAlignment.Center), 0, row);
            grid.Add(Cell(Safe(item.ProductName), false, TextAlignment.Start, wrap: true), 1, row);
            grid.Add(Cell(Quantity(item.Quantity), false, TextAlignment.Center), 2, row);
            grid.Add(Cell(string.IsNullOrWhiteSpace(item.UnitName) ? "-" : item.UnitName, false, TextAlignment.Center), 3, row);
            grid.Add(Cell(RevenueApiClient.Money(item.UnitPrice), false, TextAlignment.End), 4, row);
            grid.Add(Cell(RevenueApiClient.Money(item.LineTotal), false, TextAlignment.End), 5, row);
            grid.Add(Cell(Safe(item.Note), false, TextAlignment.Start, wrap: true), 6, row);
        }

        return grid;
    }

    private static Border Cell(string text, bool header, TextAlignment alignment, bool wrap = false)
    {
        return new Border
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            BackgroundColor = header ? Color.FromArgb("#1E3A5F") : Colors.White,
            Padding = new Thickness(AppUi.S(8), AppUi.S(9)),
            Content = new Label
            {
                Text = text,
                TextColor = header ? Colors.White : AppColors.Navy,
                FontSize = AppUi.S(header ? 12 : 11),
                FontAttributes = header ? FontAttributes.Bold : FontAttributes.None,
                HorizontalTextAlignment = alignment,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = wrap ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation
            }
        };
    }

    private static View InfoBlock(string label, string value, Color? valueColor = null)
    {
        return new VerticalStackLayout
        {
            Spacing = AppUi.S(4),
            Children =
            {
                new Label { Text = label, TextColor = AppColors.Muted, FontSize = AppUi.S(12) },
                new Label
                {
                    Text = value,
                    TextColor = valueColor ?? AppColors.Navy,
                    FontSize = AppUi.S(15),
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };
    }

    private static Border Card(View content)
    {
        return new Border
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(16) },
            BackgroundColor = AppColors.Card,
            Padding = AppUi.CardPadding,
            Content = content
        };
    }

    private static string Quantity(decimal value)
        => value % 1 == 0
            ? value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))
            : value.ToString("N2", CultureInfo.GetCultureInfo("vi-VN"));

    private static string FormatBusinessDate(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"))
            : Safe(value);

    private static string FormatDateTime(DateTimeOffset? value)
        => value is null ? "-" : value.Value.ToLocalTime().ToString("HH:mm dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));

    private static string PaymentLabel(string value)
        => Safe(value).ToLowerInvariant() switch
        {
            "cash" => "Tiền mặt",
            "transfer" => "Chuyển khoản",
            "card" => "Thẻ",
            "split" => "Nhiều phương thức",
            "other" => "Khác",
            _ => Safe(value)
        };

    private static string StatusLabel(string value)
        => Safe(value).ToLowerInvariant() switch
        {
            "paid" => "Đã thanh toán",
            "edited" => "Đã sửa",
            "cancelled" => "Đã hủy",
            _ => Safe(value)
        };

    private static Color StatusColor(string value)
        => Safe(value).ToLowerInvariant() switch
        {
            "cancelled" => AppColors.Red,
            "edited" => Color.FromArgb("#B45309"),
            _ => AppColors.Green
        };

    private static string Safe(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
