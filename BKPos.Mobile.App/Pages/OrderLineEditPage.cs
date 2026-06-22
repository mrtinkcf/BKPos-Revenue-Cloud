using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Pages;

internal sealed class OrderLineEditPage : ContentPage
{
    private readonly OrderLineCard _line;
    private readonly Func<int, string, Task> _onSave;
    private readonly Func<Task> _onDelete;
    private readonly Entry _noteEntry;
    private readonly Entry _quantityEntry;
    private readonly Label _lineTotalLabel;
    private int _quantity;
    private bool _isBusy;
    private bool _syncingQuantityText;

    public OrderLineEditPage(OrderLineCard line, Func<int, string, Task> onSave, Func<Task> onDelete)
    {
        _line = line;
        _onSave = onSave;
        _onDelete = onDelete;
        _quantity = Math.Max(1, line.Source.Quantity);
        _noteEntry = AppUi.Entry("Ghi chú bếp/bar");
        _noteEntry.Text = line.Note;
        _quantityEntry = new Entry
        {
            TextColor = AppUi.Ink,
            FontSize = AppUi.S(22),
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Colors.Transparent,
            ClearButtonVisibility = ClearButtonVisibility.Never,
            MaxLength = 4
        };
        _quantityEntry.TextChanged += OnQuantityTextChanged;
        _quantityEntry.Unfocused += (_, _) => CommitQuantityEntry();
        _lineTotalLabel = new Label
        {
            TextColor = AppUi.Blue,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End
        };

        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = AppUi.Background;
        Title = "Sửa món";
        Content = AppKeyboardHost.Wrap(BuildContent());
        UpdateQuantityLabels();
    }

    private View BuildContent()
    {
        // Navy header bar
        var backTap = new TapGestureRecognizer();
        backTap.Tapped += async (_, _) => await Navigation.PopModalAsync();
        var backBtn = new Label
        {
            Text = "‹ Quay lại",
            TextColor = Colors.White,
            FontSize = AppUi.S(14),
            VerticalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(0, 0, AppUi.S(8), 0)
        };
        backBtn.GestureRecognizers.Add(backTap);

        var topBar = new Grid
        {
            BackgroundColor = AppUi.Navy,
            Padding = new Thickness(AppUi.S(14), AppUi.S(10)),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 4
        };
        topBar.Add(backBtn, 0, 0);
        topBar.Add(new Label
        {
            Text = "Sửa món đã chọn",
            TextColor = Colors.White,
            FontSize = AppUi.S(16),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);

        // Product name + unit price on same row
        var nameLabel = new Label
        {
            Text = _line.ProductName,
            TextColor = AppUi.Navy,
            FontSize = AppUi.S(16),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        var priceLabel = new Label
        {
            Text = "Đơn giá: " + AppUi.Money(_line.Source.UnitPrice),
            TextColor = AppUi.Muted,
            FontSize = AppUi.S(12),
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.End
        };
        var infoRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        infoRow.Add(nameLabel, 0, 0);
        infoRow.Add(priceLabel, 1, 0);

        // Quantity controls — compact 44px
        var minus = QuantityButton("-", AppUi.BlueSoft, AppUi.Blue);
        minus.Clicked += (_, _) => ChangeQuantity(-1);
        var plus = QuantityButton("+", AppUi.Blue, Colors.White);
        plus.Clicked += (_, _) => ChangeQuantity(1);

        var quantityBox = new Border
        {
            BackgroundColor = AppUi.SurfaceAlt,
            Stroke = AppUi.Border,
            StrokeThickness = 1,
            HeightRequest = AppUi.S(44),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(8, 0),
            Content = _quantityEntry
        };
        var quantityRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        quantityRow.Add(minus, 0, 0);
        quantityRow.Add(quantityBox, 1, 0);
        quantityRow.Add(plus, 2, 0);

        // Thành tiền badge
        _lineTotalLabel.FontSize = AppUi.S(16);
        var totalBadge = new Border
        {
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(12, 6),
            Content = _lineTotalLabel
        };

        // Note entry
        var noteBox = new Border
        {
            BackgroundColor = AppUi.SurfaceAlt,
            Stroke = AppUi.Border,
            StrokeThickness = 1,
            Padding = new Thickness(10, 0),
            HeightRequest = AppUi.S(38),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = _noteEntry
        };

        // Action buttons
        var delete = AppUi.DangerButton("Xóa món");
        delete.HeightRequest = AppUi.S(40);
        delete.Clicked += async (_, _) => await DeleteAsync();
        var save = AppUi.PrimaryButton("Lưu");
        save.HeightRequest = AppUi.S(40);
        save.Clicked += async (_, _) => await SaveAsync();
        var actions = new Grid
        {
            Margin = new Thickness(0, 2, 0, 4),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        actions.Add(delete, 0, 0);
        actions.Add(save, 1, 0);

        var card = AppUi.CardView(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                infoRow,
                new Label { Text = "Số lượng", TextColor = AppUi.Muted, FontAttributes = FontAttributes.Bold, FontSize = 12 },
                quantityRow,
                totalBadge,
                new Label { Text = "Ghi chú in bếp/bar", TextColor = AppUi.Muted, FontAttributes = FontAttributes.Bold, FontSize = 12 },
                noteBox,
                actions
            }
        }, 12);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(topBar, 0, 0);
        root.Add(new ScrollView
        {
            Padding = new Thickness(12, 8, 12, 16),
            Content = card
        }, 0, 1);
        return root;
    }

    private static Button QuantityButton(string text, Color background, Color textColor) => new()
    {
        Text = text,
        BackgroundColor = background,
        TextColor = textColor,
        FontAttributes = FontAttributes.Bold,
        FontSize = AppUi.S(22),
        WidthRequest = AppUi.S(60),
        HeightRequest = AppUi.S(44),
        CornerRadius = (int)AppUi.S(14),
        Padding = 0
    };

    private void ChangeQuantity(int delta)
    {
        CommitQuantityEntry();
        _quantity = Math.Max(1, _quantity + delta);
        UpdateQuantityLabels();
    }

    private void UpdateQuantityLabels()
    {
        var quantityText = _quantity.ToString();
        if (_quantityEntry.Text != quantityText)
        {
            _syncingQuantityText = true;
            _quantityEntry.Text = quantityText;
            _syncingQuantityText = false;
        }

        _lineTotalLabel.Text = "Thành tiền: " + AppUi.Money(_quantity * _line.Source.UnitPrice);
    }

    private void OnQuantityTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingQuantityText)
        {
            return;
        }

        var text = e.NewTextValue ?? string.Empty;
        if (text.Length == 0)
        {
            _lineTotalLabel.Text = "Thành tiền: " + AppUi.Money(_quantity * _line.Source.UnitPrice);
            return;
        }

        var normalized = NormalizeQuantityText(text);
        if (normalized != text)
        {
            _syncingQuantityText = true;
            _quantityEntry.Text = normalized;
            _syncingQuantityText = false;
        }

        if (TryReadQuantity(normalized, out var quantity))
        {
            _quantity = quantity;
            _lineTotalLabel.Text = "Thành tiền: " + AppUi.Money(_quantity * _line.Source.UnitPrice);
        }
    }

    private void CommitQuantityEntry()
    {
        if (!TryReadQuantity(_quantityEntry.Text ?? string.Empty, out var quantity))
        {
            quantity = Math.Max(1, _quantity);
        }

        _quantity = quantity;
        UpdateQuantityLabels();
    }

    private static bool TryReadQuantity(string text, out int quantity)
    {
        if (int.TryParse(NormalizeQuantityText(text), out var value))
        {
            quantity = Math.Clamp(value, 1, 9999);
            return true;
        }

        quantity = 1;
        return false;
    }

    private static string NormalizeQuantityText(string text)
    {
        Span<char> buffer = stackalloc char[Math.Min(text.Length, 4)];
        var count = 0;
        foreach (var c in text)
        {
            if (char.IsDigit(c) && count < buffer.Length)
            {
                buffer[count++] = c;
            }
        }

        return count == 0 ? string.Empty : new string(buffer[..count]);
    }

    private async Task SaveAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            CommitQuantityEntry();
            await _onSave(_quantity, (_noteEntry.Text ?? string.Empty).Trim());
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_isBusy)
        {
            return;
        }

        var confirm = await DisplayAlert("Xóa món", "Xóa món này khỏi đơn?", "Xóa", "Hủy");
        if (!confirm)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await _onDelete();
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }
}
