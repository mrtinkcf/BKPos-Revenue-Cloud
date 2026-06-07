using System.Collections.ObjectModel;
using BKPos.Core.Interfaces;
using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Pages;

public sealed class ServerSetupPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly INetworkDiscovery _discovery;
    private readonly IHardwareIdProvider _hardwareIdProvider;
    private readonly ObservableCollection<DiscoveredServer> _servers = [];
    private readonly Entry _serverEntry = AppUi.Entry("Ví dụ: 192.168.1.20", Keyboard.Url);
    private readonly Label _status = AppUi.Subtitle("Chưa kết nối máy chủ.");
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Accent };
    private readonly CollectionView _serverList;

    public ServerSetupPage(ApiClient api, INetworkDiscovery discovery, IHardwareIdProvider hardwareIdProvider)
    {
        _api = api;
        _discovery = discovery;
        _hardwareIdProvider = hardwareIdProvider;

        Title = "Kết nối";
        BackgroundColor = AppUi.Background;
        _serverEntry.Text = ToIpInput(_api.ServerUrl);
        _serverList = BuildServerList();
        Content = AppKeyboardHost.Wrap(BuildContent());
    }

    private View BuildContent()
    {
        var discover = AppUi.SecondaryButton("Tự tìm máy chủ");
        discover.Clicked += async (_, _) => await RunAsync("Đang dò BKPos Agent trong mạng LAN...", DiscoverAsync);

        var scan = AppUi.SecondaryButton("Quét QR");
        scan.Clicked += async (_, _) => await ScanQrAsync();

        var connect = AppUi.PrimaryButton("Kết nối");
        connect.Clicked += async (_, _) => await RunAsync("Đang kiểm tra API...", ConnectAsync);

        var buttons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        buttons.Add(discover, 0);
        buttons.Add(scan, 1);
        buttons.Add(connect, 2);

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 22),
                Spacing = 18,
                Children =
                {
                    AppUi.Title("Kết nối máy chủ"),
                    AppUi.Subtitle("Chọn máy tính đang chạy BKPos Mobile API trước khi đăng nhập."),
                    AppUi.CardView(new VerticalStackLayout
                    {
                        Spacing = 14,
                        Children =
                        {
                            new Label { Text = "IP máy chủ", FontAttributes = FontAttributes.Bold, TextColor = AppUi.Ink },
                            _serverEntry,
                            AppUi.Subtitle("Chỉ nhập IP. App sẽ tự thêm http:// và cổng 5050."),
                            buttons,
                            new HorizontalStackLayout { Spacing = 8, Children = { _busy, _status } }
                        }
                    }),
                    AppUi.CardView(new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label { Text = "Máy chủ tìm thấy", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = AppUi.Ink },
                            _serverList
                        }
                    })
                }
            }
        };
    }

    private CollectionView BuildServerList()
    {
        var list = new CollectionView
        {
            ItemsSource = _servers,
            SelectionMode = SelectionMode.Single,
            HeightRequest = 220,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { FontAttributes = FontAttributes.Bold, TextColor = AppUi.Ink };
                name.SetBinding(Label.TextProperty, nameof(DiscoveredServer.Name));
                var url = new Label { FontSize = 13, TextColor = AppUi.Muted };
                url.SetBinding(Label.TextProperty, nameof(DiscoveredServer.Ip), stringFormat: "IP: {0}");
                return AppUi.CardView(new VerticalStackLayout { Spacing = 3, Children = { name, url } }, 12);
            })
        };
        list.SelectionChanged += (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is DiscoveredServer server)
            {
                _serverEntry.Text = ToIpInput(server.Url);
            }
        };
        return list;
    }

    private async Task DiscoverAsync()
    {
        _servers.Clear();
        await foreach (var server in _discovery.DiscoverAsync(TimeSpan.FromSeconds(6)))
        {
            _servers.Add(server);
        }

        _status.Text = _servers.Count == 0
            ? "Không tìm thấy máy chủ. Hãy quét QR hoặc nhập IP thủ công."
            : $"Tìm thấy {_servers.Count} máy chủ.";
    }

    private async Task ScanQrAsync()
    {
        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
        {
            _status.Text = "Chưa được cấp quyền camera để quét QR.";
            return;
        }

        await Navigation.PushModalAsync(new QrScanPage(value =>
        {
            _serverEntry.Text = ToIpInput(value);
            _status.Text = "Đã nhận địa chỉ từ QR. Bấm Kết nối để kiểm tra.";
        }));
    }

    private async Task ConnectAsync()
    {
        await _api.SaveServerUrlAsync(_serverEntry.Text ?? string.Empty);
        var info = await _api.GetServerInfoAsync();
        if (!info.DbConnected)
        {
            _status.Text = "API trả lời nhưng chưa kết nối được database.";
            return;
        }

        _status.Text = $"Đã kết nối {info.Name} tại {ToIpInput(_api.ServerUrl)}.";
        await Navigation.PushAsync(new LoginPage(_api, _hardwareIdProvider));
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

    private async Task RunAsync(string message, Func<Task> action)
    {
        _status.Text = message;
        _busy.IsRunning = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _status.Text = AppUi.ToVietnameseError(ex);
        }
        finally
        {
            _busy.IsRunning = false;
        }
    }
}
