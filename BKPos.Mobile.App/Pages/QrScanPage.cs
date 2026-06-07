using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace BKPos.Mobile.App.Pages;

public sealed class QrScanPage : ContentPage
{
    private readonly Action<string> _onDetected;
    private bool _handled;

    public QrScanPage(Action<string> onDetected)
    {
        _onDetected = onDetected;
        Title = "Quét QR máy chủ";
        BackgroundColor = Colors.Black;

        var camera = new CameraBarcodeReaderView
        {
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                AutoRotate = true,
                Multiple = false
            },
            IsDetecting = true
        };
        camera.BarcodesDetected += Camera_BarcodesDetected;

        Content = new Grid
        {
            Children =
            {
                camera,
                new Border
                {
                    BackgroundColor = Color.FromArgb("#CC2B1D14"),
                    Padding = 14,
                    Margin = 18,
                    VerticalOptions = LayoutOptions.End,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 18 },
                    Content = new Label
                    {
                        Text = "Đưa QR bkpos://IP:PORT vào khung camera",
                        TextColor = Colors.White,
                        FontSize = 16,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }

    private void Camera_BarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results.FirstOrDefault()?.Value;
        if (_handled || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _handled = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _onDetected(value.Trim());
            await Navigation.PopModalAsync();
        });
    }
}
