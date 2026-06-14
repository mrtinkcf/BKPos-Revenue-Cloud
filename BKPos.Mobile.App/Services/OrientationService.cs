namespace BKPos.Mobile.App.Services;

public static class OrientationService
{
    private static IOrientationService _instance = new NullOrientationService();
    public static IOrientationService Current => _instance;
    public static void Initialize(IOrientationService service) => _instance = service;

    private sealed class NullOrientationService : IOrientationService
    {
        public void LockLandscape() { }
        public void LockPortrait() { }
    }
}
