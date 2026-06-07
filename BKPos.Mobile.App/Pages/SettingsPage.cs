using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly MobilePrintSettings _printSettings;
    private readonly Entry _serverIp = AppUi.Entry("Ví dụ: 192.168.1.20", Keyboard.Url);
    private readonly CheckBox _autoKitchenPrint = new() { Color = AppUi.Blue };
    private readonly Entry _autoKitchenMinutes = AppUi.Entry("2", Keyboard.Numeric);
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Blue, IsVisible = false };
    private readonly Button _testButton = AppUi.SecondaryButton("Kiểm tra kết nối");
    private readonly Button _saveButton = AppUi.PrimaryButton("Lưu");
    private bool _working;

    public SettingsPage(ApiClient api)
        : this(api, new MobilePrintSettings())
    {
    }

    public SettingsPage(ApiClient api, MobilePrintSettings printSettings)
    {
        _api = api;
        _printSettings = printSettings;
        Title = "Cài đặt";
        BackgroundColor = AppUi.Background;
        Shell.SetNavBarIsVisible(this, false);
        _serverIp.Text = ToIpInput(_api.ServerUrl);
        _autoKitchenPrint.IsChecked = _printSettings.AutoKitchenPrintEnabled;
        _autoKitchenMinutes.Text = _printSettings.AutoKitchenPrintDelayMinutes.ToString();
        Content = AppKeyboardHost.Wrap(BuildContent());
    }

    public SettingsPage(ApiClient api, BKPos.Core.Interfaces.IHardwareIdProvider _)
        : this(api, new MobilePrintSettings())
    {
    }

    private View BuildContent()
    {
        var back = AppUi.IconButton("‹", AppUi.Navy);
        back.WidthRequest = AppUi.S(40);
        back.HeightRequest = AppUi.S(40);
        back.Clicked += async (_, _) => await Navigation.PopAsync();
        _testButton.Clicked += async (_, _) => await TestAsync(stayOnPage: true);
        _saveButton.Clicked += async (_, _) => await SaveAsync();
        _serverIp.HeightRequest = AppUi.S(44);
        _autoKitchenMinutes.HeightRequest = AppUi.S(44);
        _autoKitchenMinutes.WidthRequest = AppUi.S(96);
        _testButton.HeightRequest = AppUi.S(46);
        _saveButton.HeightRequest = AppUi.S(46);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        var header = new Grid
        {
            Padding = new Thickness(AppUi.S(18), AppUi.S(12)),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            BackgroundColor = AppUi.Surface
        };
        header.Add(back, 0, 0);
        header.Add(new Label
        {
            Text = "Cài đặt máy chủ",
            FontSize = AppUi.S(20),
            FontAttributes = FontAttributes.Bold,
            TextColor = AppUi.Navy,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);

        var buttons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };
        buttons.Add(_testButton, 0, 0);
        buttons.Add(_saveButton, 1, 0);

        var card = AppUi.CardView(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                AppUi.SectionTitle("IP server"),
                _serverIp,
                BuildAutoKitchenPrintRow(),
                buttons,
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    HorizontalOptions = LayoutOptions.Center,
                    HeightRequest = 18,
                    Children = { _busy }
                }
            }
        }, 16);
        card.HorizontalOptions = LayoutOptions.Fill;
        card.VerticalOptions = LayoutOptions.Start;

        root.Add(header, 0, 0);
        root.Add(new ScrollView { Padding = new Thickness(AppUi.S(24), AppUi.S(14)), Content = card }, 0, 1);
        return root;
    }

    private View BuildAutoKitchenPrintRow()
    {
        var minutesRow = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "sau",
                    TextColor = AppUi.Muted,
                    FontSize = AppUi.S(13),
                    VerticalTextAlignment = TextAlignment.Center
                },
                _autoKitchenMinutes,
                new Label
                {
                    Text = "phút",
                    TextColor = AppUi.Muted,
                    FontSize = AppUi.S(13),
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        grid.Add(CheckRow(_autoKitchenPrint, "Tự động in bar/bếp"), 0, 0);
        grid.Add(minutesRow, 1, 0);
        return grid;
    }

    private static HorizontalStackLayout CheckRow(CheckBox box, string text)
    {
        box.WidthRequest = AppUi.S(34);
        box.HeightRequest = AppUi.S(34);
        box.Scale = 0.8;

        return new HorizontalStackLayout
        {
            Spacing = 6,
            HeightRequest = AppUi.S(38),
            Children =
            {
                box,
                new Label
                {
                    Text = text,
                    TextColor = AppUi.Ink,
                    VerticalTextAlignment = TextAlignment.Center,
                    FontSize = AppUi.S(13),
                    FontAttributes = FontAttributes.Bold
                }
            }
        };
    }

    private async Task SaveAsync()
    {
        SavePrintSettings();
        if (await TestAsync(stayOnPage: false))
        {
            await DisplayAlert("Đã lưu", "Đã lưu IP máy chủ.", "OK");
            await Navigation.PopAsync();
        }
    }

    private void SavePrintSettings()
    {
        var delay = int.TryParse((_autoKitchenMinutes.Text ?? string.Empty).Trim(), out var minutes)
            ? minutes
            : 2;
        _printSettings.Save(_autoKitchenPrint.IsChecked, delay);
        _autoKitchenMinutes.Text = _printSettings.AutoKitchenPrintDelayMinutes.ToString();
    }

    private async Task<bool> TestAsync(bool stayOnPage)
    {
        if (_working)
        {
            return false;
        }

        var ip = (_serverIp.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            await DisplayAlert("Thiếu IP", "Vui lòng nhập IP máy chủ.", "OK");
            return false;
        }

        SetBusy(true);
        try
        {
            await _api.SaveServerUrlAsync(ip);
            var info = await _api.GetServerInfoAsync();
            if (!info.DbConnected)
            {
                SetBusy(false);
                await DisplayAlert("Máy chủ lỗi DB", "Kết nối API thành công nhưng server chưa kết nối được database.", "OK");
                return false;
            }

            SetBusy(false);
            if (stayOnPage)
            {
                await DisplayAlert("Kết nối thành công", $"Đã kết nối BKPos Mobile Server tại {ToIpInput(_api.ServerUrl)}.", "OK");
            }

            return true;
        }
        catch (Exception ex)
        {
            SetBusy(false);
            await DisplayAlert("Không kết nối được", AppUi.ToVietnameseError(ex), "OK");
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _working = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        _testButton.IsEnabled = !busy;
        _saveButton.IsEnabled = !busy;
        _serverIp.IsEnabled = !busy;
        _autoKitchenPrint.IsEnabled = !busy;
        _autoKitchenMinutes.IsEnabled = !busy;
    }

    private static string? ToIpInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.StartsWith("bkpos://", StringComparison.OrdinalIgnoreCase))
        {
            text = "http://" + text["bkpos://".Length..];
        }

        if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            text = "http://" + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host)
            ? uri.Host
            : value.Trim();
    }
}
