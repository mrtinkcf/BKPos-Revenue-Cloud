using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using MauiEntry = Microsoft.Maui.Controls.Entry;
using MauiNavigationPage = Microsoft.Maui.Controls.NavigationPage;

namespace BKPos.Revenue.App;

public sealed class LoginPage : ContentPage
{
    private readonly RevenueApiClient _api;
    private readonly RevenueSessionStore _session;
    private readonly IServiceProvider _services;
    private readonly MauiEntry _username = new() { Placeholder = "Tên đăng nhập", ReturnType = ReturnType.Next };
    private readonly MauiEntry _password = new() { Placeholder = "Mật khẩu", IsPassword = true, ReturnType = ReturnType.Done };
    private readonly Label _status = new() { TextColor = AppColors.Red, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button _loginButton = new()
    {
        Text = "Đăng nhập",
        BackgroundColor = AppColors.Blue,
        TextColor = Colors.White,
        CornerRadius = 14,
        FontAttributes = FontAttributes.Bold
    };

    public LoginPage(RevenueApiClient api, RevenueSessionStore session, IServiceProvider services)
    {
        _api = api;
        _session = session;
        _services = services;
        BackgroundColor = AppColors.Navy;
        MauiNavigationPage.SetHasNavigationBar(this, false);
        On<iOS>().SetUseSafeArea(true);
        Build();
        LoadSaved();
    }

    private void Build()
    {
        _loginButton.HeightRequest = AppUi.S(52);
        _loginButton.FontSize = AppUi.S(16);
        _status.FontSize = AppUi.S(13);

        _loginButton.Clicked += async (_, _) => await LoginAsync();
        _password.Completed += async (_, _) => await LoginAsync();

        // Settings gear — top-right
        var settingsButton = new Button
        {
            Text = "⚙",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#94A3B8"),
            FontSize = AppUi.S(24),
            HeightRequest = 44,
            WidthRequest = 44,
            Padding = Thickness.Zero
        };
        settingsButton.Clicked += async (_, _) =>
        {
            var page = _services.GetRequiredService<SettingsPage>();
            await Navigation.PushModalAsync(new MauiNavigationPage(page)
            {
                BarBackgroundColor = Colors.White,
                BarTextColor = AppColors.Navy
            });
        };
        Grid.SetColumn(settingsButton, 1);

        // Branding — font sizes scale with screen
        var branding = new VerticalStackLayout
        {
            Spacing = AppUi.IsSmallScreen ? 2 : 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "BKPos", TextColor = Colors.White, FontSize = AppUi.S(AppUi.IsSmallScreen ? 28 : 34), FontAttributes = FontAttributes.Bold },
                new Label { Text = "Revenue Cloud", TextColor = Color.FromArgb("#93C5FD"), FontSize = AppUi.S(AppUi.IsSmallScreen ? 15 : 17), FontAttributes = FontAttributes.Bold },
                new Label { Text = "Theo dõi doanh thu mọi lúc, mọi nơi", TextColor = Color.FromArgb("#94A3B8"), FontSize = AppUi.S(13) }
            }
        };

        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(0, AppUi.S(6), 0, AppUi.IsSmallScreen ? 12 : 20),
            Children = { branding, settingsButton }
        };

        // Login card — username + password only
        var card = new Border
        {
            Stroke = Color.FromArgb("#1E3A5F"),
            StrokeShape = new RoundRectangle { CornerRadius = AppUi.S(24) },
            BackgroundColor = Colors.White,
            Padding = AppUi.CardPadding,
            Content = new VerticalStackLayout
            {
                Spacing = AppUi.CardSpacing,
                Children =
                {
                    new Label { Text = "Đăng nhập", FontSize = AppUi.S(22), FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy },
                    FieldLabel("Tên đăng nhập"),
                    Field(_username),
                    FieldLabel("Mật khẩu"),
                    Field(_password),
                    new BoxView { HeightRequest = AppUi.IsSmallScreen ? 0 : 4, Color = Colors.Transparent },
                    _loginButton,
                    _status
                }
            }
        };

        var footer = new Label
        {
            Text = "Bảo Khang Laptop  •  0396 529 103",
            TextColor = Color.FromArgb("#475569"),
            FontSize = AppUi.S(12),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var cardHost = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children = { card }
        };
        Grid.SetRow(cardHost, 1);

        var footerHost = new ContentView
        {
            Padding = new Thickness(0, AppUi.S(12), 0, 0),
            Content = footer
        };
        Grid.SetRow(footerHost, 2);

        Content = new Grid
        {
            Padding = AppUi.PagePadding,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children = { headerRow, cardHost, footerHost }
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
        entry.FontSize = AppUi.S(16);
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

    private void LoadSaved()
    {
        _username.Text = _session.Username;
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.WorkerUrl) || string.IsNullOrWhiteSpace(_session.TenantId))
        {
            _status.Text = "Chưa cấu hình kết nối. Nhấn ⚙ để nhập URL và Tenant ID.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_username.Text))
        {
            _status.Text = "Vui lòng nhập tên đăng nhập.";
            return;
        }

        try
        {
            _loginButton.IsEnabled = false;
            _status.TextColor = AppColors.Muted;
            _status.Text = "Đang đăng nhập...";
            await _api.LoginAsync(_session.WorkerUrl, _session.TenantId, _username.Text, _password.Text ?? string.Empty);
            _status.Text = string.Empty;
            await Navigation.PushAsync(_services.GetRequiredService<DashboardPage>());
            Navigation.RemovePage(this);
        }
        catch (Exception ex)
        {
            _status.TextColor = AppColors.Red;
            _status.Text = "Không đăng nhập được: " + ex.Message;
        }
        finally
        {
            _loginButton.IsEnabled = true;
        }
    }
}
