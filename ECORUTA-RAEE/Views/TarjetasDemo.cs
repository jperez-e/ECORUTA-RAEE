namespace ECORUTA_RAEE.Views;

internal static class TarjetasDemo
{
    internal static Style Estilo(string clave) => (Style)Application.Current!.Resources[clave];
    internal static Color ColorRecurso(string clave) => (Color)Application.Current!.Resources[clave];

    internal static Label Texto(string texto, string estilo = "TextoVisual") =>
        new() { Text = texto, Style = Estilo(estilo), LineBreakMode = LineBreakMode.WordWrap };

    internal static Border Tarjeta(params View[] contenido)
    {
        var pila = new VerticalStackLayout { Spacing = 6 };
        foreach (var vista in contenido)
            pila.Add(vista);
        return new Border { Style = Estilo("TarjetaVisual"), Content = pila };
    }

    internal static Image Icono(string nombre, double tamano = 40) => new()
    {
        Source = "ui_" + nombre + ".png", WidthRequest = tamano, HeightRequest = tamano,
        HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
        InputTransparent = true
    };

    internal static View ConIcono(string icono, View contenido)
    {
        var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 10 };
        fila.Add(Icono(icono), 0, 0);
        fila.Add(contenido, 1, 0);
        return fila;
    }

    internal static Border Encabezado(string titulo, string detalle, string icono)
    {
        var texto = Texto(titulo, "OnGreen");
        texto.FontSize = 25;
        return new Border
        {
            Style = Estilo("GreenHero"),
            Content = ConIcono(icono, new VerticalStackLayout { Spacing = 5, Children = { texto, Texto(detalle, "OnGreen") } })
        };
    }

    internal static Border Cabecera(string titulo, string detalle, View? accion = null)
    {
        var texto = Texto(titulo, "OnGreen");
        texto.FontSize = 25;
        var contexto = Texto("Datos de esta instalación", "OnGreen");
        contexto.FontSize = 12;
        var contenido = new VerticalStackLayout { Spacing = 8, Children = { contexto, texto, Texto(detalle, "OnGreen") } };
        if (accion is not null)
            contenido.Add(accion);
        return new Border { Style = Estilo("CabeceraVisual"), Content = contenido };
    }

    internal static Label EstadoVisual(string texto, string tono)
    {
        var etiqueta = Texto(texto);
        etiqueta.FontAttributes = FontAttributes.Bold;
        AplicarTono(etiqueta, tono);
        return etiqueta;
    }

    internal static void AplicarTono(Label etiqueta, string tono)
    {
        (string claro, string oscuro) = tono switch
        {
            "azul" => ("InkBlue", "NightBlue"),
            "ambar" => ("WarningText", "NightAmber"),
            "violeta" => ("InkViolet", "NightViolet"),
            "verde" => ("PrimaryGreen", "NightGreen"),
            _ => ("TextSecondary", "NightMuted")
        };
        etiqueta.SetAppThemeColor(Label.TextColorProperty, ColorRecurso(claro), ColorRecurso(oscuro));
    }

    internal static Button Boton(string texto, Func<Task> accion, bool principal = false)
    {
        var boton = new Button { Text = texto, Style = Estilo(principal ? "PrincipalVisual" : "AccionVisual") };
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
