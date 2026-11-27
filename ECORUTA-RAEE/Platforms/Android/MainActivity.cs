
using Android.App;
using Android.Content.PM;
using Android.Content.Res;
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

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            ConfigurarBarraEstado();
        }

        private void ConfigurarBarraEstado()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(30) || Window?.InsetsController is not { } controlador)
                return;
            bool oscuro = (Resources!.Configuration!.UiMode & UiMode.NightMask) == UiMode.NightYes;
            int iconosOscuros = (int)WindowInsetsControllerAppearance.LightStatusBars;
            controlador.SetSystemBarsAppearance(oscuro ? 0 : iconosOscuros, iconosOscuros);
        }
    }
}
