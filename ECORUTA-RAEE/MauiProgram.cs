
using Microsoft.Extensions.Logging;

namespace ECORUTA_RAEE
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont(
                        "OpenSans-Regular.ttf",
                        "OpenSansRegular");

                    fonts.AddFont(
                        "OpenSans-Semibold.ttf",
                        "OpenSansSemibold");
                });

#if ANDROID
            // Quitar la línea inferior nativa de Android
            // únicamente a los campos del Login.

            Microsoft.Maui.Handlers.EntryHandler.Mapper
                .AppendToMapping(
                    "SinSubrayadoLogin",
                    (handler, view) =>
                    {
                        if (view is Entry entry &&
                            (entry.AutomationId == "LoginUsuario" ||
                             entry.AutomationId == "LoginContrasena"))
                        {
                            handler.PlatformView.BackgroundTintList =
                                Android.Content.Res.ColorStateList.ValueOf(
                                    Android.Graphics.Color.Transparent);
                        }
                    });
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
