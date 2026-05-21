using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace BKPos.Revenue.App;

public sealed class LoginPage : ContentPage
{
    private readonly RevenueApiClient _api;
    private readonly RevenueSessionStore _session;
    private readonly IServiceProvider _services;
    private readonly Entry _workerUrl = new() { Placeholder = "Revenue Cloud Worker URL", Keyboard = Keyboard.Url };
    private readonly Entry _tenantId = new() { Placeholder = "Tenant ID" };
    private readonly Entry _username = new() { Placeholder = "Tên đăng nhập" };
    private readonly Entry _password = new() { Placeholder = "Mật khẩu", IsPassword = true };
    private readonly Label _status = new() { TextColor = AppColors.Red, FontSize = 13 };
    private readonly Button _loginButton = new() { Text = "Đăng nhập", BackgroundColor = AppColors.Blue, TextColor = Colors.White, CornerRadius = 12 };

    public LoginPage(RevenueApiClient api, RevenueSessionStore session, IServiceProvider services)
    {
        _api = api;
        _session = session;
        _services = services;
        Title = "BKPos Revenue";
        BackgroundColor = AppColors.Surface;
        NavigationPage.SetHasNavigationBar(this, false);
        Build();
        LoadSaved();
    }

    private void Build()
    {
        _loginButton.Clicked += async (_, _) => await LoginAsync();

        var card = new Border
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            BackgroundColor = AppColors.Card,
            Padding = new Thickness(22),
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    new Label { Text = "BKPos Revenue", FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy },
                    new Label { Text = "Theo dõi doanh thu online", FontSize = 14, TextColor = AppColors.Muted },
                    Field(_workerUrl),
                    Field(_tenantId),
                    Field(_username),
                    Field(_password),
                    _loginButton,
                    _status
                }
            }
        };
        Grid.SetRow(card, 1);

        Content = new Grid
        {
            Padding = new Thickness(20),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                card
            }
        };
    }

    private static Border Field(Entry entry)
    {
        entry.HeightRequest = 48;
        entry.FontSize = 15;
        entry.TextColor = AppColors.Navy;
        entry.PlaceholderColor = AppColors.Muted;
        return new Border
        {
            Stroke = Color.FromArgb("#D8E1EC"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Padding = new Thickness(10, 0),
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
            _status.Text = "Vui lòng nhập Worker URL, Tenant ID và tài khoản.";
            return;
        }

        try
        {
            _loginButton.IsEnabled = false;
            _status.Text = "Đang đăng nhập...";
            await _api.LoginAsync(_workerUrl.Text, _tenantId.Text, _username.Text, _password.Text ?? string.Empty);
            _status.Text = string.Empty;
            await Navigation.PushAsync(_services.GetRequiredService<DashboardPage>());
            Navigation.RemovePage(this);
        }
        catch (Exception ex)
        {
            _status.Text = "Không đăng nhập được: " + ex.Message;
        }
        finally
        {
            _loginButton.IsEnabled = true;
        }
    }
}
