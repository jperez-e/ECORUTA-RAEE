namespace ECORUTA_RAEE.Views;

internal static class CamposCuenta
{
    internal static void PrepararError(Label error)
    {
        error.Style = TarjetasDemo.Estilo("TextoVisual");
        error.SetAppThemeColor(Label.TextColorProperty, TarjetasDemo.ColorRecurso("ErrorColor"),
            TarjetasDemo.ColorRecurso("NightError"));
        error.IsVisible = !string.IsNullOrEmpty(error.Text);
        error.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == Label.TextProperty.PropertyName)
                error.IsVisible = !string.IsNullOrEmpty(error.Text);
        };
    }

    internal static void Configurar(Entry campo, Border borde, Label error)
    {
        PrepararError(error);
        campo.MinimumHeightRequest = 48;
        campo.BackgroundColor = Colors.Transparent;
        campo.Focused += (_, _) => Pintar();
        campo.Unfocused += (_, _) => Pintar();
        error.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == Label.TextProperty.PropertyName)
                Pintar();
        };
#if ANDROID
        campo.HandlerChanged += (_, _) =>
        {
            if (campo.Handler?.PlatformView is Android.Widget.EditText nativo)
                nativo.Background = null;
        };
#endif
        Pintar();

        void Pintar()
        {
            string claro = error.IsVisible ? "ErrorColor" : campo.IsFocused ? "PrimaryGreen" : "BorderColor";
            string oscuro = error.IsVisible ? "NightError" : campo.IsFocused ? "NightGreen" : "NightBorder";
            borde.SetAppTheme<Brush>(Border.StrokeProperty,
                new SolidColorBrush(TarjetasDemo.ColorRecurso(claro)),
                new SolidColorBrush(TarjetasDemo.ColorRecurso(oscuro)));
            borde.StrokeThickness = campo.IsFocused || error.IsVisible ? 2 : 1;
        }
    }
} 
