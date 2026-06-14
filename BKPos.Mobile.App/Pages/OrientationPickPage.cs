using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Pages;

public sealed class OrientationPickPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly MobileLoginSettings _loginSettings;
    private readonly MobilePrintSettings _printSettings;
    private readonly MobileOrientationSettings _orientationSettings;
    private readonly CheckBox _remember = new() { Color = AppUi.Blue };

    public OrientationPickPage(
        ApiClient api,
        MobileLoginSettings loginSettings,
        MobilePrintSettings printSettings,
        MobileOrientationSettings orientationSettings)
    {
        _api = api;
        _loginSettings = loginSettings;
        _printSettings = printSettings;
        _orientationSettings = orientationSettings;
        BackgroundColor = AppUi.Background;
        Shell.SetNavBarIsVisible(this, false);
        Content = BuildContent();
    }

    private View BuildContent()
    {
        var topBar = new Grid
        {
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
        });

        var title = new Label
        {
            Text = "Chọn chế độ màn hình",
            FontSize = AppUi.S(20),
            FontAttributes = FontAttributes.Bold,
            TextColor = AppUi.Navy,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var subtitle = new Label
        {
            Text = "Lựa chọn có thể thay đổi bằng nút chuyển trong app.",
            FontSize = AppUi.S(13),
            TextColor = AppUi.Muted,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var btnLandscape = BuildModeButton("↔", "Màn hình ngang", "Phù hợp cho tablet\nvà màn hình rộng");
        var btnPortrait = BuildModeButton("↕", "Màn hình dọc", "Phù hợp cho điện thoại\nvà cầm tay một tay");

        btnLandscape.GestureRecognizers.Add(CreateTap(() => PickAsync(MobileOrientationSettings.Landscape)));
        btnPortrait.GestureRecognizers.Add(CreateTap(() => PickAsync(MobileOrientationSettings.Portrait)));

        var modeRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = AppUi.S(14)
        };
        modeRow.Add(btnLandscape, 0, 0);
        modeRow.Add(btnPortrait, 1, 0);

        _remember.WidthRequest = AppUi.S(34);
        _remember.HeightRequest = AppUi.S(34);
        _remember.Scale = 0.8;
        var rememberRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                _remember,
                new Label
                {
                    Text = "Ghi nhớ lựa chọn này",
                    TextColor = AppUi.Ink,
                    VerticalTextAlignment = TextAlignment.Center,
                    FontSize = AppUi.S(14)
                }
            }
        };

        var card = AppUi.CardView(new VerticalStackLayout
        {
            Spacing = AppUi.S(18),
            Children = { title, subtitle, modeRow, rememberRow }
        }, 20);
        card.MaximumWidthRequest = AppUi.S(480);
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
        root.Add(new ScrollView
        {
            Padding = new Thickness(AppUi.S(20)),
            Content = card
        }, 0, 1);

        return root;
    }

    private static Border BuildModeButton(string icon, string label, string description)
    {
        var iconLabel = new Label
        {
            Text = icon,
            FontSize = AppUi.S(36),
            TextColor = AppUi.Navy,
            HorizontalTextAlignment = TextAlignment.Center
        };
        var nameLabel = new Label
        {
            Text = label,
            FontSize = AppUi.S(15),
            FontAttributes = FontAttributes.Bold,
            TextColor = AppUi.Navy,
            HorizontalTextAlignment = TextAlignment.Center
        };
        var descLabel = new Label
        {
            Text = description,
            FontSize = AppUi.S(11),
            TextColor = AppUi.Muted,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };

        return new Border
        {
            BackgroundColor = AppUi.Surface,
            Stroke = AppUi.Border,
            StrokeThickness = 1.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(AppUi.S(14), AppUi.S(22)),
            Content = new VerticalStackLayout
            {
                Spacing = AppUi.S(6),
                Children = { iconLabel, nameLabel, descLabel }
            }
        };
    }

    private static TapGestureRecognizer CreateTap(Func<Task> handler)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await handler();
        return tap;
    }

    private async Task PickAsync(string mode)
    {
        _orientationSettings.Save(mode, _remember.IsChecked);

        if (mode == MobileOrientationSettings.Landscape)
        {
            OrientationService.Current.LockLandscape();
            var page = new SalesPage(_api, _loginSettings, _printSettings);
            Navigation.InsertPageBefore(page, this);
        }
        else
        {
            OrientationService.Current.LockPortrait();
            var page = new PortraitSalesPage(_api, _loginSettings, _printSettings, _orientationSettings);
            Navigation.InsertPageBefore(page, this);
        }

        await Navigation.PopAsync(false);
    }
}
