using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace BKPos.Mobile.App;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Unspecified,
    WindowSoftInputMode = SoftInput.AdjustResize | SoftInput.StateHidden,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Current = this;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (ReferenceEquals(Current, this))
            Current = null;
    }

    // Dismiss keyboard khi touch ra ngoài Entry, để button nhận tap ngay lần đầu thay vì bấm 2 lần
    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        if (ev?.Action == MotionEventActions.Down)
        {
            var focused = CurrentFocus;
            if (focused is EditText)
            {
                var rect = new Android.Graphics.Rect();
                focused.GetGlobalVisibleRect(rect);
                if (!rect.Contains((int)ev.RawX, (int)ev.RawY))
                {
                    focused.ClearFocus();
                    var imm = GetSystemService(InputMethodService) as InputMethodManager;
                    imm?.HideSoftInputFromWindow(focused.WindowToken, HideSoftInputFlags.None);
                }
            }
        }
        return base.DispatchTouchEvent(ev);
    }
}
