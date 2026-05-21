namespace BKPos.Revenue.App;

public partial class App : Application
{
    private readonly Page _rootPage;

    public App(LoginPage loginPage)
    {
        InitializeComponent();
        _rootPage = new NavigationPage(loginPage)
        {
            BarBackgroundColor = AppColors.Navy,
            BarTextColor = Colors.White
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(_rootPage);
}
