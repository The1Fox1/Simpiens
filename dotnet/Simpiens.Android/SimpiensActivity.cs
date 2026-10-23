using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Simpiens.Client;

namespace Simpiens.Android
{
    /// <summary>
    /// The Android entry point (Epic CLIENT, compile-only until M4): KNI's game activity hosting the shared client,
    /// portrait only (producer, 2026-10-18).
    /// </summary>
    [Activity(Label = "Simpiens",
        MainLauncher = true,
        Icon = "@drawable/icon",
        Theme = "@style/Theme.Splash",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.UiMode | ConfigChanges.SmallestScreenSize,
        ScreenOrientation = ScreenOrientation.Portrait)]
    public class SimpiensActivity : Microsoft.Xna.Framework.AndroidGameActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            var game = new SimpiensGame(new ClientOptions());
            SetContentView((View)game.Services.GetService(typeof(View)));
            game.Run();
        }
    }
}
