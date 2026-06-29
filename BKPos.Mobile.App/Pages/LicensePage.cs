using System.Security.Cryptography;
using System.Text;
using BKPos.Core.Interfaces;
using BKPos.Mobile.App.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;

namespace BKPos.Mobile.App.Pages;

public sealed class LicensePage : ContentPage
{
    private const string SellerPasswordSha256 = "550724979CB2E6F76C8C6BB5B6697B9788E3B8D36ED8C6FE1420271E91FBB7EB";
    private const string SellerOnlyMessage = "Chỉ người quản trị có thể dùng chức năng này. Vui lòng liên hệ Phone/Zalo: 0396529103";

    private readonly ApiClient _api;
    private readonly IHardwareIdProvider _hardwareIdProvider;
    private readonly Label _hardwareId = new()
    {
        TextColor = AppUi.Ink,
        FontSize = AppUi.S(13),
        FontAttributes = FontAttributes.Bold,
        LineBreakMode = LineBreakMode.WordWrap,
        VerticalTextAlignment = TextAlignment.Center
    };
    private readonly Entry _licenseKey = AppUi.Entry("Key bản quyền BKP3");
    private readonly ActivityIndicator _busy = new() { Color = AppUi.Blue, IsVisible = false };

    // Border-based buttons: tránh góc trắng Android và lỗi bàn phím bấm 2 lần
    private Border? _requestBorder;
    private Label? _requestLabel;
    private Border? _claimBorder;
    private Label? _claimLabel;
    private Border? _manualBorder;
    private Border? _activateBorder;

    private Entry? _popupPasswordEntry;
    private Label? _popupEyeLabel;
    private Grid? _popupOverlay;
    private Grid? _manualOverlay;
    private Func<Task>? _pendingPopupAction;
    private bool _hasPendingRequest;
    private bool _popupPasswordVisible;

    public LicensePage(ApiClient api, IHardwareIdProvider hardwareIdProvider)
    {
        _api = api;
        _hardwareIdProvider = hardwareIdProvider;
        Title = "Bản quyền";
        BackgroundColor = AppUi.Background;
        Shell.SetNavBarIsVisible(this, false);
        Content = AppKeyboardHost.Wrap(BuildContent());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _hardwareId.Text = _hardwareIdProvider.GetHardwareId();
            _ = RefreshRequestStateAsync();
        }
        catch (Exception ex)
        {
            _ = DisplayAlert("Không lấy được ID máy", AppUi.ToVietnameseError(ex), "OK");
        }
    }

    private View BuildContent()
    {
        // Border + TapGestureRecognizer: không bị Android trắng góc, không bị bàn phím chặn lần đầu
        static (Border btn, Label lbl) Btn(string text, Color bg, Color fg, Func<Task> onTap)
        {
            var lbl = new Label
            {
                Text = text,
                TextColor = fg,
                FontSize = AppUi.S(13),
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.NoWrap
            };
            var btn = new Border
            {
                BackgroundColor = bg,
                StrokeThickness = 0,
                HeightRequest = AppUi.S(42),
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = lbl,
                HorizontalOptions = LayoutOptions.Fill
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await onTap();
            btn.GestureRecognizers.Add(tap);
            return (btn, lbl);
        }

        (_requestBorder, _requestLabel) = Btn("Gửi yêu cầu kích hoạt", AppUi.Navy, Colors.White, () =>
        {
            if (!_hasPendingRequest)
                ShowPasswordPopup(SubmitRequestAsync);
            return Task.CompletedTask;
        });

        // Kích hoạt tự động: xanh lá, sáng khi đã gửi yêu cầu, mờ khi chưa
        (_claimBorder, _claimLabel) = Btn("Kích hoạt tự động",
            Color.FromArgb("#DCFCE7"), Color.FromArgb("#16A34A"), AutoActivateAsync);

        // Kích hoạt thủ công: mở popup nhập key, không nhét textbox vào form chính.
        (_manualBorder, _) = Btn("Kích hoạt thủ công", AppUi.Blue, Colors.White, ShowManualPopupCore);

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
            Text = "⧉",
            BackgroundColor = AppUi.BlueSoft,
            TextColor = AppUi.Blue,
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            HeightRequest = AppUi.S(40),
            WidthRequest = AppUi.S(46),
            CornerRadius = 10,
            Padding = 0
        };
        copyHardware.Clicked += async (_, _) => await CopyHardwareIdAsync();

        var hardwareBox = new Border
        {
            BackgroundColor = AppUi.SurfaceAlt,
            Stroke = AppUi.Border,
            StrokeThickness = 1,
            Padding = new Thickness(AppUi.S(12), AppUi.S(8)),
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = _hardwareId
        };

        var hardwareRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        hardwareRow.Add(hardwareBox, 0, 0);
        hardwareRow.Add(copyHardware, 1, 0);

        _busy.HorizontalOptions = LayoutOptions.Center;

        var card = AppUi.CardView(new VerticalStackLayout
        {
            Spacing = AppUi.S(10),
            Children =
            {
                new Label { Text = "ID máy (chỉ đọc)", TextColor = AppUi.Muted, FontSize = 12, FontAttributes = FontAttributes.Bold },
                hardwareRow,
                _requestBorder!,
                _claimBorder!,
                _manualBorder!,
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

        _popupOverlay = BuildPasswordPopupOverlay();
        _popupOverlay.IsVisible = false;
        _popupOverlay.ZIndex = 20;
        root.Add(_popupOverlay, 0, 0);
        Grid.SetRowSpan(_popupOverlay, 2);

        _manualOverlay = BuildManualActivationPopupOverlay();
        _manualOverlay.IsVisible = false;
        _manualOverlay.ZIndex = 21;
        root.Add(_manualOverlay, 0, 0);
        Grid.SetRowSpan(_manualOverlay, 2);

        return root;
    }

    private Grid BuildPasswordPopupOverlay()
    {
        _popupPasswordEntry = new Entry
        {
            Placeholder = "Mật khẩu quản trị",
            IsPassword = true,
            BackgroundColor = AppUi.SurfaceAlt,
            TextColor = AppUi.Ink,
            PlaceholderColor = AppUi.Muted,
            ReturnType = ReturnType.Done,
            HeightRequest = AppUi.S(40)
        };
        _popupPasswordEntry.Completed += async (_, _) => await ConfirmPasswordAsync();

        _popupEyeLabel = new Label
        {
            Text = "👁",
            FontSize = AppUi.S(16),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = AppUi.Blue,
            InputTransparent = true
        };
        var eyeBtn = new Border
        {
            BackgroundColor = AppUi.BlueSoft,
            StrokeThickness = 0,
            WidthRequest = AppUi.S(40),
            HeightRequest = AppUi.S(40),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = _popupEyeLabel
        };
        var eyeTap = new TapGestureRecognizer();
        eyeTap.Tapped += (_, _) =>
        {
            SetPopupPasswordVisible(!_popupPasswordVisible);
        };
        eyeBtn.GestureRecognizers.Add(eyeTap);

        var entryRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 6
        };
        entryRow.Add(_popupPasswordEntry, 0, 0);
        entryRow.Add(eyeBtn, 1, 0);

        // Popup buttons cũng dùng Border+tap để fix lỗi bàn phím bấm 2 lần
        var cancelBorder = new Border
        {
            BackgroundColor = AppUi.Surface,
            Stroke = AppUi.Border,
            StrokeThickness = 1,
            HeightRequest = AppUi.S(40),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            HorizontalOptions = LayoutOptions.Fill,
            Content = new Label
            {
                Text = "Hủy",
                TextColor = AppUi.Ink,
                FontSize = AppUi.S(13),
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        var cancelTap = new TapGestureRecognizer();
        cancelTap.Tapped += (_, _) => HidePasswordPopup();
        cancelBorder.GestureRecognizers.Add(cancelTap);

        var confirmBorder = new Border
        {
            BackgroundColor = AppUi.Blue,
            StrokeThickness = 0,
            HeightRequest = AppUi.S(40),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            HorizontalOptions = LayoutOptions.Fill,
            Content = new Label
            {
                Text = "Xác nhận",
                TextColor = Colors.White,
                FontSize = AppUi.S(13),
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        var confirmTap = new TapGestureRecognizer();
        confirmTap.Tapped += async (_, _) => await ConfirmPasswordAsync();
        confirmBorder.GestureRecognizers.Add(confirmTap);

        var btnRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        btnRow.Add(cancelBorder, 0, 0);
        btnRow.Add(confirmBorder, 1, 0);

        var popupCard = AppUi.CardView(new VerticalStackLayout
        {
            Spacing = AppUi.S(12),
            Children =
            {
                new Label
                {
                    Text = "Nhập mật khẩu quản trị",
                    TextColor = AppUi.Navy,
                    FontSize = AppUi.S(15),
                    FontAttributes = FontAttributes.Bold
                },
                entryRow,
                btnRow
            }
        }, 16);

        popupCard.WidthRequest = Math.Min(AppUi.Scale * 360.0 - AppUi.S(28), AppUi.S(320));
        popupCard.HorizontalOptions = LayoutOptions.Center;
        popupCard.VerticalOptions = LayoutOptions.Center;

        var dimOverlay = new BoxView
        {
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.45),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        var dimTap = new TapGestureRecognizer();
        dimTap.Tapped += (_, _) => HidePasswordPopup();
        dimOverlay.GestureRecognizers.Add(dimTap);

        return new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { dimOverlay, popupCard }
        };
    }

    private Grid BuildManualActivationPopupOverlay()
    {
        _licenseKey.HeightRequest = AppUi.S(40);
        _licenseKey.ReturnType = ReturnType.Done;
        _licenseKey.Completed += async (_, _) => await ActivateFromManualPopupAsync();

        var cancelBorder = new Border
        {
            BackgroundColor = AppUi.Surface,
            Stroke = AppUi.Border,
            StrokeThickness = 1,
            HeightRequest = AppUi.S(40),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            HorizontalOptions = LayoutOptions.Fill,
            Content = new Label
            {
                Text = "Hủy",
                TextColor = AppUi.Ink,
                FontSize = AppUi.S(13),
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        var cancelTap = new TapGestureRecognizer();
        cancelTap.Tapped += (_, _) => HideManualPopup();
        cancelBorder.GestureRecognizers.Add(cancelTap);

        _activateBorder = new Border
        {
            BackgroundColor = AppUi.Blue,
            StrokeThickness = 0,
            HeightRequest = AppUi.S(40),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            HorizontalOptions = LayoutOptions.Fill,
            Content = new Label
            {
                Text = "Kích hoạt",
                TextColor = Colors.White,
                FontSize = AppUi.S(13),
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        var activateTap = new TapGestureRecognizer();
        activateTap.Tapped += async (_, _) => await ActivateFromManualPopupAsync();
        _activateBorder.GestureRecognizers.Add(activateTap);

        var btnRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        btnRow.Add(cancelBorder, 0, 0);
        btnRow.Add(_activateBorder, 1, 0);

        var popupCard = AppUi.CardView(new VerticalStackLayout
        {
            Spacing = AppUi.S(12),
            Children =
            {
                new Label
                {
                    Text = "Kích hoạt thủ công",
                    TextColor = AppUi.Navy,
                    FontSize = AppUi.S(15),
                    FontAttributes = FontAttributes.Bold
                },
                new Label
                {
                    Text = "Dán key bản quyền rồi bấm Kích hoạt.",
                    TextColor = AppUi.Muted,
                    FontSize = AppUi.S(12)
                },
                _licenseKey,
                btnRow
            }
        }, 16);

        popupCard.WidthRequest = Math.Min(AppUi.Scale * 360.0 - AppUi.S(28), AppUi.S(340));
        popupCard.HorizontalOptions = LayoutOptions.Center;
        popupCard.VerticalOptions = LayoutOptions.Center;

        var dimOverlay = new BoxView
        {
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.45),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        var dimTap = new TapGestureRecognizer();
        dimTap.Tapped += (_, _) => HideManualPopup();
        dimOverlay.GestureRecognizers.Add(dimTap);

        return new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { dimOverlay, popupCard }
        };
    }

    private void ShowPasswordPopup(Func<Task> onSuccess)
    {
        _pendingPopupAction = onSuccess;
        if (_popupPasswordEntry is not null)
        {
            _popupPasswordEntry.Text = string.Empty;
        }
        SetPopupPasswordVisible(false);
        if (_popupOverlay is not null)
            _popupOverlay.IsVisible = true;
        Dispatcher.Dispatch(() =>
        {
            SetPopupPasswordVisible(false);
            _popupPasswordEntry?.Focus();
        });
    }

    private void HidePasswordPopup()
    {
        if (_popupOverlay is not null)
            _popupOverlay.IsVisible = false;
        if (_popupPasswordEntry is not null)
            _popupPasswordEntry.Text = string.Empty;
        SetPopupPasswordVisible(false);
        _pendingPopupAction = null;
    }

    private void SetPopupPasswordVisible(bool visible)
    {
        _popupPasswordVisible = visible;

        if (_popupPasswordEntry is not null)
        {
            _popupPasswordEntry.IsPassword = !visible;
            ApplyNativePopupPasswordState(visible);
        }

        if (_popupEyeLabel is not null)
        {
            _popupEyeLabel.Text = visible ? "🙈" : "👁";
        }
    }

    private void ApplyNativePopupPasswordState(bool visible)
    {
        if (_popupPasswordEntry is null)
        {
            return;
        }

#if ANDROID
        if (_popupPasswordEntry.Handler?.PlatformView is Android.Widget.EditText editText)
        {
            editText.InputType = visible
                ? Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationVisiblePassword
                : Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword | Android.Text.InputTypes.TextFlagNoSuggestions;
            editText.TransformationMethod = visible ? null : Android.Text.Method.PasswordTransformationMethod.Instance;
            editText.ShowSoftInputOnFocus = false;
            editText.SetSelection(editText.Text?.Length ?? 0);
        }
#elif IOS
        if (_popupPasswordEntry.Handler?.PlatformView is UIKit.UITextField textField)
        {
            textField.SecureTextEntry = !visible;
        }
#endif
    }

    private async Task ConfirmPasswordAsync()
    {
        if (!IsSellerPasswordValid(_popupPasswordEntry?.Text))
        {
            await DisplayAlert("Không đủ quyền", SellerOnlyMessage, "OK");
            return;
        }

        var action = _pendingPopupAction;
        HidePasswordPopup();
        if (action is not null)
            await action();
    }

    private Task ShowManualPopupCore()
    {
        if (_manualOverlay is not null)
            _manualOverlay.IsVisible = true;
        Dispatcher.Dispatch(() => _licenseKey.Focus());
        return Task.CompletedTask;
    }

    private void HideManualPopup()
    {
        if (_manualOverlay is not null)
            _manualOverlay.IsVisible = false;
    }

    private async Task ActivateFromManualPopupAsync()
    {
        await ActivateAsync();
    }

    private async Task RefreshRequestStateAsync()
    {
        _hasPendingRequest = await MobileLicenseRequestStore.LoadAsync() is not null;
        ApplyRequestUi(false);
    }

    private void ApplyRequestUi(bool busy)
    {
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;

        // Nút gửi yêu cầu
        if (_requestLabel is not null)
            _requestLabel.Text = _hasPendingRequest ? "Đã gửi yêu cầu" : "Gửi yêu cầu kích hoạt";
        if (_requestBorder is not null)
        {
            _requestBorder.BackgroundColor = _hasPendingRequest ? AppUi.Border : AppUi.Navy;
            _requestBorder.InputTransparent = busy || _hasPendingRequest;
            _requestBorder.Opacity = busy ? 0.55 : 1.0;
        }
        if (_requestLabel is not null)
            _requestLabel.TextColor = _hasPendingRequest ? AppUi.Muted : Colors.White;

        // Kích hoạt tự động: xanh lá sáng khi đã gửi yêu cầu, mờ khi chưa
        if (_claimBorder is not null)
        {
            _claimBorder.BackgroundColor = _hasPendingRequest
                ? Color.FromArgb("#16A34A")
                : Color.FromArgb("#DCFCE7");
            _claimBorder.InputTransparent = busy;
            _claimBorder.Opacity = busy ? 0.55 : 1.0;
        }
        if (_claimLabel is not null)
            _claimLabel.TextColor = _hasPendingRequest ? Colors.White : Color.FromArgb("#16A34A");

        // Kích hoạt thủ công
        if (_manualBorder is not null)
        {
            _manualBorder.InputTransparent = busy;
            _manualBorder.Opacity = busy ? 0.55 : 1.0;
        }

        // Kích hoạt (manual panel)
        if (_activateBorder is not null)
        {
            _activateBorder.InputTransparent = busy;
            _activateBorder.Opacity = busy ? 0.55 : 1.0;
        }

        _licenseKey.IsEnabled = !busy;
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
            _hasPendingRequest = true;
            ApplyRequestUi(false);
            await DisplayAlert(
                "Đã gửi yêu cầu",
                $"Mã yêu cầu: {result.Request.RequestId}\nSau khi admin duyệt trên BKPos.KeyGen, bấm Kích hoạt tự động.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không gửi được yêu cầu", AppUi.ToVietnameseError(ex), "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task AutoActivateAsync()
    {
        if (await MobileLicenseRequestStore.LoadAsync() is not null)
        {
            await CheckRequestAsync();
            return;
        }

        await ClaimAsync();
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
                _hasPendingRequest = false;
                ApplyRequestUi(false);
                await DisplayAlert("Kích hoạt thành công", "Bản quyền mobile đã được kích hoạt.", "OK");
                await Navigation.PopAsync();
                return;
            }

            _licenseKey.Text = licenseKey;
            await ShowManualPopupCore();
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

    private void SetBusy(bool busy) => ApplyRequestUi(busy);

    private static bool IsSellerPasswordValid(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var expected = Convert.FromHexString(SellerPasswordSha256);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

