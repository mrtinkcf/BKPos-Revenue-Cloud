using System.Security.Cryptography;
using System.Text;
using BKPos.Core.Interfaces;
using BKPos.Mobile.App.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace BKPos.Mobile.App.Pages;

public sealed class LicensePage : ContentPage
{
    private const string SellerPasswordSha256 = "550724979CB2E6F76C8C6BB5B6697B9788E3B8D36ED8C6FE1420271E91FBB7EB";
    private const string SellerOnlyMessage = "Chỉ người quản trị có thể dùng chức năng này. Vui lòng liên hệ Phone/Zalo: 0396529103";

    private readonly ApiClient _api;
    private readonly IHardwareIdProvider _hardwareIdProvider;
    private readonly Entry _hardwareId = AppUi.Entry("ID máy");
    private readonly Entry _licenseKey = AppUi.Entry("Key bản quyền BKP3");
    private readonly Entry _sellerPassword = AppUi.Entry("Mật khẩu quản trị", password: true);
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Blue, IsVisible = false };
    private readonly Button _requestButton = AppUi.NavyButton("Gửi yêu cầu");
    private readonly Button _checkRequestButton = AppUi.SecondaryButton("Kiểm tra duyệt");
    private readonly Button _claimButton = AppUi.SecondaryButton("Kích hoạt tự động");
    private readonly Button _activateButton = AppUi.PrimaryButton("Kích hoạt");

    public LicensePage(ApiClient api, IHardwareIdProvider hardwareIdProvider)
    {
        _api = api;
        _hardwareIdProvider = hardwareIdProvider;
        Title = "Bản quyền";
        BackgroundColor = AppUi.Background;
        Shell.SetNavBarIsVisible(this, false);
        _hardwareId.IsReadOnly = true;
        Content = AppKeyboardHost.Wrap(BuildContent());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _hardwareId.Text = _hardwareIdProvider.GetHardwareId();
        }
        catch (Exception ex)
        {
            _ = DisplayAlert("Không lấy được ID máy", AppUi.ToVietnameseError(ex), "OK");
        }
    }

    private View BuildContent()
    {
        _requestButton.Clicked += async (_, _) => await SubmitRequestAsync();
        _checkRequestButton.Clicked += async (_, _) => await CheckRequestAsync();
        _claimButton.Clicked += async (_, _) => await ClaimAsync();
        _activateButton.Clicked += async (_, _) => await ActivateAsync();

        var backTap = new TapGestureRecognizer();
        backTap.Tapped += async (_, _) => await Navigation.PopAsync();
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
            Text = "Kích hoạt bản quyền",
            TextColor = Colors.White,
            FontSize = AppUi.S(16),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);

        var copyHardware = new Button
        {
            Text = "Copy",
            BackgroundColor = AppUi.BlueSoft,
            TextColor = AppUi.Blue,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            HeightRequest = AppUi.S(40),
            WidthRequest = AppUi.S(64),
            CornerRadius = 10,
            Padding = new Thickness(8, 0)
        };
        copyHardware.Clicked += async (_, _) => await CopyHardwareIdAsync();

        _hardwareId.HeightRequest = AppUi.S(40);
        var hardwareRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        hardwareRow.Add(_hardwareId, 0, 0);
        hardwareRow.Add(copyHardware, 1, 0);

        _sellerPassword.HeightRequest = AppUi.S(40);
        _requestButton.HeightRequest = AppUi.S(40);
        _checkRequestButton.HeightRequest = AppUi.S(40);
        var requestButtons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        requestButtons.Add(_requestButton, 0, 0);
        requestButtons.Add(_checkRequestButton, 1, 0);

        _licenseKey.HeightRequest = AppUi.S(40);
        _claimButton.HeightRequest = AppUi.S(40);
        _activateButton.HeightRequest = AppUi.S(40);
        var activateButtons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        activateButtons.Add(_claimButton, 0, 0);
        activateButtons.Add(_activateButton, 1, 0);

        _busy.HorizontalOptions = LayoutOptions.Center;

        var card = AppUi.CardView(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "ID máy (chỉ đọc)", TextColor = AppUi.Muted, FontSize = 12, FontAttributes = FontAttributes.Bold },
                hardwareRow,
                new Label { Text = "Yêu cầu cấp tự động", TextColor = AppUi.Muted, FontSize = 12, FontAttributes = FontAttributes.Bold },
                new Label { Text = SellerOnlyMessage, TextColor = AppUi.Blue, FontSize = 11, LineBreakMode = LineBreakMode.WordWrap },
                _sellerPassword,
                requestButtons,
                new Label { Text = "Key bản quyền", TextColor = AppUi.Muted, FontSize = 12, FontAttributes = FontAttributes.Bold },
                _licenseKey,
                activateButtons,
                _busy
            }
        }, 14);

        card.WidthRequest = Math.Min(AppUi.Scale * 360.0 - AppUi.S(28), AppUi.S(520));
        card.HorizontalOptions = LayoutOptions.Center;
        card.VerticalOptions = LayoutOptions.Start;

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(topBar, 0, 0);
        root.Add(new ScrollView { Padding = new Thickness(AppUi.S(14), AppUi.S(10)), Content = card }, 0, 1);
        return root;
    }

    private async Task CopyHardwareIdAsync()
    {
        var id = (_hardwareId.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            await DisplayAlert("Chưa có ID máy", "Không lấy được ID máy để tạo key.", "OK");
            return;
        }

        await Clipboard.Default.SetTextAsync(id);
        await DisplayAlert("Đã copy ID máy", "Dán ID này vào BKPos.KeyGen để tạo key mobile.", "OK");
    }

    private async Task SubmitRequestAsync()
    {
        var hardwareId = (_hardwareId.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
        {
            await DisplayAlert("Chưa có ID máy", "Không lấy được ID máy để gửi yêu cầu.", "OK");
            return;
        }

        if (!IsSellerPasswordValid(_sellerPassword.Text))
        {
            await DisplayAlert("Không đủ quyền", SellerOnlyMessage, "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var payload = new CloudLicenseRequestSubmitDto(
                "Mobile",
                hardwareId,
                "Khách hàng BKPos",
                string.Empty,
                "BKPos Mobile Order",
                string.Empty,
                DeviceInfo.Current.Name,
                $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})",
                $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}",
                "Mobile Order gửi yêu cầu cấp bản quyền tự động.");

            var result = await _api.SubmitCloudLicenseRequestAsync(payload);
            if (result.Request is null || string.IsNullOrWhiteSpace(result.RequestSecret))
            {
                await DisplayAlert("Không gửi được", "Worker không trả mã yêu cầu hợp lệ.", "OK");
                return;
            }

            await MobileLicenseRequestStore.SaveAsync(result.Request.RequestId, result.RequestSecret, result.Request.ProductType);
            await DisplayAlert(
                "Đã gửi yêu cầu",
                $"Mã yêu cầu: {result.Request.RequestId}\nSau khi admin duyệt trên BKPos.KeyGen, bấm Kiểm tra duyệt.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không gửi được yêu cầu", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            _sellerPassword.Text = string.Empty;
            SetBusy(false);
        }
    }

    private async Task CheckRequestAsync()
    {
        var hardwareId = (_hardwareId.Text ?? string.Empty).Trim();
        var pending = await MobileLicenseRequestStore.LoadAsync();
        if (pending is null)
        {
            await DisplayAlert("Chưa có yêu cầu", "Máy này chưa gửi yêu cầu cấp bản quyền.", "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var status = await _api.GetCloudLicenseRequestStatusAsync(pending.RequestId, pending.RequestSecret);
            if (status.Request is null)
            {
                await DisplayAlert("Không kiểm tra được", "Worker không trả trạng thái yêu cầu.", "OK");
                return;
            }

            if (string.Equals(status.Request.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert(
                    "Yêu cầu bị từ chối",
                    string.IsNullOrWhiteSpace(status.Request.RejectionReason)
                        ? "Yêu cầu cấp bản quyền đã bị từ chối."
                        : status.Request.RejectionReason,
                    "OK");
                return;
            }

            if (!string.Equals(status.Request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Đang chờ duyệt", $"Mã yêu cầu: {pending.RequestId}", "OK");
                return;
            }

            var licenseKey = status.License?.LicenseKey ?? string.Empty;
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                await DisplayAlert("Thiếu key", "Yêu cầu đã duyệt nhưng Worker chưa trả License Key.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_api.ServerUrl))
            {
                _licenseKey.Text = licenseKey;
                await DisplayAlert("Đã duyệt", "Vào cài đặt nhập IP server, sau đó bấm Kích hoạt để hoàn tất.", "OK");
                return;
            }

            var activated = await _api.ActivateLicenseAsync(licenseKey, hardwareId);
            if (string.Equals(activated.License.Status, "Activated", StringComparison.OrdinalIgnoreCase))
            {
                MobileActivationStore.MarkActivated(hardwareId, activated.License.LicenseId);
                MobileLicenseRequestStore.Clear();
                await DisplayAlert("Kích hoạt thành công", "Bản quyền mobile đã được kích hoạt.", "OK");
                await Navigation.PopAsync();
                return;
            }

            _licenseKey.Text = licenseKey;
            await DisplayAlert("Chưa kích hoạt", activated.License.Message ?? activated.License.Status, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không kiểm tra được", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ClaimAsync()
    {
        var hardwareId = (_hardwareId.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
        {
            await DisplayAlert("Chưa có ID máy", "Không lấy được ID máy để kích hoạt tự động.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(_api.ServerUrl))
        {
            await DisplayAlert("Chưa cài đặt máy chủ", "Vào cài đặt để nhập IP server trước khi kích hoạt.", "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _api.ClaimLicenseAsync(hardwareId);
            if (string.Equals(result.License.Status, "Activated", StringComparison.OrdinalIgnoreCase))
            {
                MobileActivationStore.MarkActivated(hardwareId, result.License.LicenseId);
                await DisplayAlert("Kích hoạt thành công", "Bản quyền mobile đã được kích hoạt tự động.", "OK");
                await Navigation.PopAsync();
                return;
            }

            await DisplayAlert("Chưa kích hoạt", result.License.Message ?? result.License.Status, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không kích hoạt tự động được", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ActivateAsync()
    {
        var key = (_licenseKey.Text ?? string.Empty).Trim();
        var hardwareId = (_hardwareId.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            await DisplayAlert("Thiếu key", "Vui lòng dán key bản quyền.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(_api.ServerUrl))
        {
            await DisplayAlert("Chưa cài đặt máy chủ", "Vào cài đặt để nhập IP server trước khi kích hoạt.", "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _api.ActivateLicenseAsync(key, hardwareId);
            if (string.Equals(result.License.Status, "Activated", StringComparison.OrdinalIgnoreCase))
            {
                MobileActivationStore.MarkActivated(hardwareId, result.License.LicenseId);
                await DisplayAlert("Kích hoạt thành công", "Bản quyền mobile đã được kích hoạt.", "OK");
                await Navigation.PopAsync();
                return;
            }

            await DisplayAlert("Chưa kích hoạt", result.License.Message ?? result.License.Status, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không kích hoạt được", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        _requestButton.IsEnabled = !busy;
        _checkRequestButton.IsEnabled = !busy;
        _claimButton.IsEnabled = !busy;
        _activateButton.IsEnabled = !busy;
        _licenseKey.IsEnabled = !busy;
        _sellerPassword.IsEnabled = !busy;
    }

    private static bool IsSellerPasswordValid(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var expected = Convert.FromHexString(SellerPasswordSha256);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
