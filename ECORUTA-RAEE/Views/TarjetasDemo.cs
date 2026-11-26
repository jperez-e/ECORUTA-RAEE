namespace ECORUTA_RAEE.Views;

internal static class TarjetasDemo
{
    private static Style Estilo(string clave) => (Style)Application.Current!.Resources[clave];

    internal static Label Texto(string texto, string estilo = "Subtitle") =>
        new() { Text = texto, Style = Estilo(estilo), LineBreakMode = LineBreakMode.WordWrap };

    internal static Border Tarjeta(params View[] contenido)
    {
        var pila = new VerticalStackLayout { Spacing = 6 };
        foreach (var vista in contenido)
            pila.Add(vista);
        return new Border { Style = Estilo("CardBorder"), Padding = 12, Content = pila };
    }

    internal static Button Boton(string texto, Func<Task> accion, bool principal = false)
    {
        var boton = new Button { Text = texto, Style = Estilo(principal ? "PrimaryButton" : "SecondaryButton") };
        boton.Clicked += async (_, _) =>
        {
            if (!boton.IsEnabled)
                return;
            boton.IsEnabled = false;
            try { await accion(); }
            finally { boton.IsEnabled = true; }
        };
        return boton;
    }
}
