using BKPos.Core.Interfaces;
using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Pages;

public sealed class LoginPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly IHardwareIdProvider _hardwareIdProvider;
    private readonly MobileLoginSettings _loginSettings;
    private readonly MobilePrintSettings _printSettings;
    private readonly MobileOrientationSettings _orientationSettings = new();
    private readonly Entry _username = AppUi.Entry("Tên đăng nhập");
    private readonly Entry _password = AppUi.Entry("Mật khẩu", password: true);
    private readonly CheckBox _remember = new() { Color = AppUi.Blue };
    private readonly CheckBox _autoLogin = new() { Color = AppUi.Blue };
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Blue, IsVisible = false };
    private readonly Button _loginButton = AppUi.PrimaryButton("Đăng nhập");
    private readonly Button _exitButton = AppUi.DangerButton("Thoát");
    private bool _loaded;
    private bool _loggingIn;

    public LoginPage(
        ApiClient api,
        IHardwareIdProvider hardwareIdProvider,
        MobileLoginSettings loginSettings,
        MobilePrintSettings printSettings)
    {
        _api = api;
        _hardwareIdProvider = hardwareIdProvider;
        _loginSettings = loginSettings;
        _printSettings = printSettings;
        Title = "Đăng nhập";
        BackgroundColor = AppUi.Background;
        Shell.SetNavBarIsVisible(this, false);
        Content = AppKeyboardHost.Wrap(BuildContent());
    }

    public LoginPage(ApiClient api, IHardwareIdProvider hardwareIdProvider)
        : this(api, hardwareIdProvider, new MobileLoginSettings(), new MobilePrintSettings())
    {
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded)
        {
            _loaded = true;
            await LoadSavedLoginAsync();
        }
    }

    private View BuildContent()
    {
        var settings = AppUi.IconButton("⚙");
        settings.WidthRequest = AppUi.S(42);
        settings.HeightRequest = AppUi.S(42);
        settings.Clicked += async (_, _) => await Navigation.PushAsync(new SettingsPage(_api, _printSettings));

        var license = AppUi.IconButton("◇");
        license.WidthRequest = AppUi.S(42);
        license.HeightRequest = AppUi.S(42);
        license.Clicked += async (_, _) => await Navigation.PushAsync(new LicensePage(_api, _hardwareIdProvider));

        _remember.CheckedChanged += (_, e) =>
        {
            if (!e.Value)
            {
                _autoLogin.IsChecked = false;
            }

            _autoLogin.IsEnabled = e.Value;
        };

        _loginButton.Clicked += async (_, _) => await LoginAsync();
        _exitButton.Clicked += (_, _) => Application.Current?.Quit();

        var topBar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(AppUi.S(22), AppUi.S(6)),
            BackgroundColor = AppUi.Navy
        };
        topBar.Add(new Label
        {
            Text = "BKPos Mobile",
            FontSize = AppUi.S(20),
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        topBar.Add(license, 1, 0);
        topBar.Add(settings, 2, 0);

        _username.HeightRequest = AppUi.S(38);
        _password.HeightRequest = AppUi.S(38);
        _loginButton.HeightRequest = AppUi.S(42);
        _exitButton.HeightRequest = AppUi.S(42);
        _busy.HorizontalOptions = LayoutOptions.Center;

        var checksRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 4
        };
        checksRow.Add(CheckRow(_remember, "Lưu thông tin đăng nhập"), 0, 0);
        checksRow.Add(CheckRow(_autoLogin, "Tự động đăng nhập"), 1, 0);

        var actionGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };
        actionGrid.Add(_loginButton, 0, 0);
        actionGrid.Add(_exitButton, 1, 0);

        var card = AppUi.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Đăng nhập",
                    FontSize = AppUi.S(17),
                    FontAttributes = FontAttributes.Bold,
                    TextColor = AppUi.Navy,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                },
                _username,
                _password,
                checksRow,
                actionGrid,
                _busy
            }
        }, 16);

        card.MaximumWidthRequest = AppUi.S(430);
        card.HorizontalOptions = LayoutOptions.Center;
        card.VerticalOptions = LayoutOptions.Center;

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(topBar, 0, 0);
        root.Add(new ScrollView { Padding = new Thickness(AppUi.S(20), AppUi.S(8)), Content = card }, 0, 1);

        return root;
    }

    private static HorizontalStackLayout CheckRow(CheckBox box, string text)
    {
        box.WidthRequest = AppUi.S(34);
        box.HeightRequest = AppUi.S(34);
        box.Scale = 0.8;

        return new()
        {
            Spacing = 6,
            HeightRequest = AppUi.S(34),
            Children =
            {
                box,
                new Label { Text = text, TextColor = AppUi.Ink, VerticalTextAlignment = TextAlignment.Center, FontSize = AppUi.S(13) }
            }
        };
    }

    private async Task LoadSavedLoginAsync()
    {
        var saved = await _loginSettings.LoadAsync();
        _username.Text = saved.Username;
        _password.Text = saved.Password;
        _remember.IsChecked = saved.Remember;
        _autoLogin.IsEnabled = saved.Remember;
        _autoLogin.IsChecked = saved.AutoLogin;

        if (saved.AutoLogin && !string.IsNullOrWhiteSpace(saved.Username))
        {
            await Task.Delay(250);
            await LoginAsync();
        }
    }

    private async Task LoginAsync()
    {
        if (_loggingIn)
        {
            return;
        }

        var username = (_username.Text ?? string.Empty).Trim();
        var password = _password.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username))
        {
            await DisplayAlert("Thiếu thông tin", "Vui lòng nhập tên đăng nhập.", "OK");
            return;
        }

        SetBusy(true);
        try
        {
            if (string.IsNullOrWhiteSpace(_api.ServerUrl))
            {
                await DisplayAlert("Chưa cài đặt máy chủ", "Vào biểu tượng cài đặt để nhập IP máy chủ trước khi đăng nhập.", "OK");
                return;
            }

            var server = await _api.GetServerInfoAsync();
            if (!server.DbConnected)
            {
                await DisplayAlert("Máy chủ lỗi DB", "BKPos Mobile Server đang chạy nhưng chưa kết nối được database.", "OK");
                return;
            }

            var license = await _api.GetLicenseStatusAsync();
            if (!string.Equals(license.Status, "Activated", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Chưa kích hoạt bản quyền", license.Message ?? "Vui lòng kích hoạt bản quyền mobile trước khi đăng nhập.", "OK");
                return;
            }

            var currentHardwareId = _hardwareIdProvider.GetHardwareId();
            if (string.IsNullOrWhiteSpace(license.HardwareId)
                || !string.Equals(license.HardwareId.Trim(), currentHardwareId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert(
                    "Sai ID bản quyền",
                    $"Bản quyền mobile trên máy chủ không khớp với {GetDevicePlatformLabel()}. Vui lòng kích hoạt lại bằng ID máy đang hiển thị trong app.\n\nID máy hiện tại: {currentHardwareId}",
                    "OK");
                return;
            }

            if (!MobileActivationStore.IsActivatedFor(currentHardwareId, license.LicenseId))
            {
                await DisplayAlert(
                    "Chưa kích hoạt trên thiết bị",
                    "Vui lòng vào biểu tượng bản quyền, nhập key và bấm Kích hoạt trước khi đăng nhập.",
                    "OK");
                return;
            }

            await _api.LoginAsync(username, password);
            await _loginSettings.SaveAsync(username, password, _remember.IsChecked, _autoLogin.IsChecked);
            await NavigateAfterLoginAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không đăng nhập được", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task NavigateAfterLoginAsync()
    {
        var (mode, _) = _orientationSettings.Load();
        if (mode == MobileOrientationSettings.Landscape)
        {
            OrientationService.Current.LockLandscape();
            await Navigation.PushAsync(new SalesPage(_api, _loginSettings, _printSettings));
        }
        else if (mode == MobileOrientationSettings.Portrait)
        {
            OrientationService.Current.LockPortrait();
            await Navigation.PushAsync(new PortraitSalesPage(_api, _loginSettings, _printSettings, _orientationSettings));
        }
        else
        {
            await Navigation.PushAsync(new OrientationPickPage(_api, _loginSettings, _printSettings, _orientationSettings));
        }
    }

    private void SetBusy(bool busy)
    {
        _loggingIn = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        _loginButton.IsEnabled = !busy;
        _exitButton.IsEnabled = !busy;
        _username.IsEnabled = !busy;
        _password.IsEnabled = !busy;
    }

    private static string GetDevicePlatformLabel()
    {
        if (DeviceInfo.Current.Platform == DevicePlatform.iOS)
        {
            return "thiết bị iOS này";
        }

        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
        {
            return "thiết bị Android này";
        }

        return "thiết bị này";
    }
}
