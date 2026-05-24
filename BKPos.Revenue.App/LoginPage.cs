using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using MauiEntry = Microsoft.Maui.Controls.Entry;
using MauiScrollView = Microsoft.Maui.Controls.ScrollView;

namespace BKPos.Revenue.App;

public sealed class LoginPage : ContentPage
{
    private readonly RevenueApiClient _api;
    private readonly RevenueSessionStore _session;
    private readonly IServiceProvider _services;
    private readonly MauiEntry _workerUrl = new() { Placeholder = "Revenue Cloud URL", Keyboard = Keyboard.Url, ReturnType = ReturnType.Next };
    private readonly MauiEntry _tenantId = new() { Placeholder = "Tenant ID", ReturnType = ReturnType.Next };
    private readonly MauiEntry _username = new() { Placeholder = "Tên đăng nhập", ReturnType = ReturnType.Next };
    private readonly MauiEntry _password = new() { Placeholder = "Mật khẩu", IsPassword = true, ReturnType = ReturnType.Done };
    private readonly Label _status = new() { TextColor = AppColors.Red, FontSize = 13 };
    private readonly Button _loginButton = new() { Text = "Đăng nhập", BackgroundColor = AppColors.Blue, TextColor = Colors.White, CornerRadius = 14, HeightRequest = 50, FontAttributes = FontAttributes.Bold };

    public LoginPage(RevenueApiClient api, RevenueSessionStore session, IServiceProvider services)
    {
        _api = api;
        _session = session;
        _services = services;
        Title = "BKPos Revenue";
        BackgroundColor = AppColors.Navy;
        Microsoft.Maui.Controls.NavigationPage.SetHasNavigationBar(this, false);
        On<iOS>().SetUseSafeArea(true);
        Build();
        LoadSaved();
    }

    private void Build()
    {
        _loginButton.Clicked += async (_, _) => await LoginAsync();
        _password.Completed += async (_, _) => await LoginAsync();

        var header = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = "BKPos", TextColor = Colors.White, FontSize = 36, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Revenue Cloud", TextColor = Color.FromArgb("#93C5FD"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Theo dõi doanh thu mọi lúc, mọi nơi", TextColor = Color.FromArgb("#CBD5E1"), FontSize = 14 }
            }
        };

        var card = new Border
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            BackgroundColor = Colors.White,
            Padding = new Thickness(18),
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Đăng nhập", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy },
                    Field(_workerUrl),
                    Field(_tenantId),
                    Field(_username),
                    Field(_password),
                    _loginButton,
                    _status
                }
            }
        };

        var footer = new Label
        {
            Text = "Bảo Khang Laptop - Phone/Zalo: 0396 529 103",
            TextColor = Color.FromArgb("#CBD5E1"),
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var formScroll = new MauiScrollView
        {
            Content = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Spacing = 18,
                Children = { card }
            }
        };
        Grid.SetRow(formScroll, 1);

        var footerHost = new Grid { Children = { footer } };
        Grid.SetRow(footerHost, 2);

        Content = new Grid
        {
            Padding = new Thickness(18, 22),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                header,
                formScroll,
                footerHost
            }
        };
    }

    private static Border Field(MauiEntry entry)
    {
        entry.HeightRequest = 46;
        entry.FontSize = 15;
        entry.TextColor = AppColors.Navy;
        entry.PlaceholderColor = AppColors.Muted;
        entry.BackgroundColor = Colors.Transparent;
        return new Border
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Padding = new Thickness(12, 0),
            Content = entry
        };
    }

    private void LoadSaved()
    {
        _workerUrl.Text = _session.WorkerUrl;
        _tenantId.Text = _session.TenantId;
        _username.Text = _session.Username;
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(_workerUrl.Text)
            || string.IsNullOrWhiteSpace(_tenantId.Text)
            || string.IsNullOrWhiteSpace(_username.Text))
        {
            _status.Text = "Vui lòng nhập Revenue Cloud URL, Tenant ID và tài khoản.";
            return;
        }

        try
        {
            _loginButton.IsEnabled = false;
            _status.TextColor = AppColors.Muted;
            _status.Text = "Đang đăng nhập...";
            await _api.LoginAsync(_workerUrl.Text, _tenantId.Text, _username.Text, _password.Text ?? string.Empty);
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


