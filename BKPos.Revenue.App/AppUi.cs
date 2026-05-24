namespace BKPos.Revenue.App;

/// <summary>
/// Responsive sizing helper.
/// Baseline: 390pt wide / 844pt tall (iPhone 14).
/// Scales proportionally for iPhone SE (375×667), Pro Max (430×932), and Android variants.
/// </summary>
internal static class AppUi
{
    private static double _w;
    private static double _h;

    private static void Init()
    {
        if (_w != 0) return;
        var info = DeviceDisplay.MainDisplayInfo;
        _w = info.Density > 0 ? info.Width / info.Density : 390;
        _h = info.Density > 0 ? info.Height / info.Density : 844;
    }

    public static double ScreenWidth  { get { Init(); return _w; } }
    public static double ScreenHeight { get { Init(); return _h; } }

    // Scale factor from 390pt baseline, clamped to [0.88, 1.20]
    private static double Scale => Math.Clamp(ScreenWidth / 390.0, 0.88, 1.20);

    /// Scale any size value proportionally to the device screen width.
    public static double S(double baseSize) =>
        Math.Round(baseSize * Scale, MidpointRounding.AwayFromZero);

    /// Chart canvas height = 18 % of screen height, clamped [100, 200] pt.
    public static double ChartHeight =>
        Math.Clamp(ScreenHeight * 0.18, 100, 200);

    /// True on compact phones (e.g. iPhone SE, 667 pt tall).
    public static bool IsSmallScreen => ScreenHeight < 700;

    /// Card inner padding — tighter on small screens.
    public static Thickness CardPadding =>
        IsSmallScreen ? new Thickness(16, 16) : new Thickness(22, 24);

    /// Outer page padding (horizontal scales, top/bottom stays small).
    public static Thickness PagePadding =>
        new Thickness(S(20), IsSmallScreen ? 4 : 8);

    /// Vertical spacing inside login card — compact on small screens.
    public static double CardSpacing => IsSmallScreen ? 10 : 14;
}
