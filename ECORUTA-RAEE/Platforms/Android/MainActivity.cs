
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace ECORUTA_RAEE
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges =
            ConfigChanges.ScreenSize |
            ConfigChanges.Orientation |
            ConfigChanges.UiMode |
            ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize |
            ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(
            Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ConfigurarBarraEstado();
        }

        private void ConfigurarBarraEstado()
        {
            if (Window == null)
                return;

            // Permitir colorear la barra del sistema.
            Window.AddFlags(
                WindowManagerFlags.DrawsSystemBarBackgrounds);

            // Verde oscuro de EcoRuta-RAEE.
            Window.SetStatusBarColor(
                Android.Graphics.Color.ParseColor("#17452F"));

            // Mostrar iconos claros sobre fondo oscuro.
            var decorView = Window.DecorView;

            if (decorView != null)
            {
                decorView.SystemUiVisibility =
                    (StatusBarVisibility)(
                        (int)decorView.SystemUiVisibility &
                        ~(int)SystemUiFlags.LightStatusBar);
            }
        }
    }
}
