using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using MauiEntry = Microsoft.Maui.Controls.Entry;
using MauiNavigationPage = Microsoft.Maui.Controls.NavigationPage;
using MauiScrollView = Microsoft.Maui.Controls.ScrollView;

namespace BKPos.Revenue.App;

public sealed class SettingsPage : ContentPage
{
    private readonly RevenueSessionStore _session;
    private readonly MauiEntry _workerUrl = new() { Placeholder = "https://your-worker.workers.dev", Keyboard = Keyboard.Url, ReturnType = ReturnType.Next };
    private readonly MauiEntry _tenantId = new() { Placeholder = "TENANT-001", ReturnType = ReturnType.Done };
    private readonly Label _status = new() { LineBreakMode = LineBreakMode.WordWrap, IsVisible = false };

    public SettingsPage(RevenueSessionStore session)
    {
        _session = session;
        BackgroundColor = AppColors.Surface;
        MauiNavigationPage.SetHasNavigationBar(this, false);
        On<iOS>().SetUseSafeArea(true);
        Build();
        Load();
    }

    private void Build()
    {
        _status.FontSize = AppUi.S(13);

        var saveButton = new Button
        {
            Text = "Lưu cài đặt",
            BackgroundColor = AppColors.Blue,
            TextColor = Colors.White,
            CornerRadius = 14,
            HeightRequest = AppUi.S(52),
            FontAttributes = FontAttributes.Bold,
            FontSize = AppUi.S(16)
        };
        saveButton.Clicked += (_, _) => Save();

        var closeButton = new Button
        {
            Text = "Đóng",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Blue,
            FontSize = AppUi.S(16),
            HeightRequest = 44,
            Padding = Thickness.Zero
        };
        closeButton.Clicked += async (_, _) => await Navigation.PopModalAsync();
        Grid.SetColumn(closeButton, 1);

        var header = new Grid
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
                    Text = "Cài đặt kết nối",
                    FontSize = AppUi.S(22),
                    FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Navy,
                    VerticalOptions = LayoutOptions.Center
                },
                closeButton
            }
        };

        var card = new Border
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(20) },
            BackgroundColor = Colors.White,
            Padding = AppUi.CardPadding,
            Content = new VerticalStackLayout
            {
                Spacing = AppUi.S(14),
                Children =
                {
                    header,
                    new Label
                    {
                        Text = "Thông tin này do BKPos cung cấp khi kích hoạt dịch vụ Revenue Cloud.",
                        TextColor = AppColors.Muted,
                        FontSize = AppUi.S(13),
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    FieldLabel("Revenue Cloud URL"),
                    Field(_workerUrl),
                    FieldLabel("Tenant ID"),
                    Field(_tenantId),
                    new BoxView { HeightRequest = AppUi.IsSmallScreen ? 0 : 4, Color = Colors.Transparent },
                    saveButton,
                    _status
                }
            }
        };

        Content = new MauiScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = AppUi.PagePadding,
                Children = { card }
            }
        };
    }

    private static Label FieldLabel(string text)
        => new()
        {
            Text = text,
            TextColor = AppColors.Muted,
            FontSize = AppUi.S(12),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(4, 0, 0, -6)
        };

    private static Border Field(MauiEntry entry)
    {
        entry.HeightRequest = AppUi.S(48);
        entry.FontSize = AppUi.S(15);
        entry.TextColor = AppColors.Navy;
        entry.PlaceholderColor = AppColors.Muted;
        entry.BackgroundColor = Colors.Transparent;
        return new Border
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(12) },
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Padding = new Thickness(AppUi.S(14), 0),
            Content = entry
        };
    }

    private void Load()
    {
        _workerUrl.Text = _session.WorkerUrl;
        _tenantId.Text = _session.TenantId;
    }

    private void Save()
    {
        var url = _workerUrl.Text?.Trim() ?? string.Empty;
        var tid = _tenantId.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(tid))
        {
            _status.TextColor = AppColors.Red;
            _status.Text = "Vui lòng nhập đầy đủ URL và Tenant ID.";
            _status.IsVisible = true;
            return;
        }

        _session.WorkerUrl = url;
        _session.TenantId = tid;
        _status.TextColor = AppColors.Green;
        _status.Text = "Đã lưu thành công!";
        _status.IsVisible = true;
    }
}
