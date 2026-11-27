namespace ECORUTA_RAEE.Views;

public sealed class MetricaVisual : ContentView
{
    private readonly Label _valor = TarjetasDemo.Texto("", "TituloVisual");
    private readonly Label _titulo = TarjetasDemo.Texto("");
    private readonly Image _icono = TarjetasDemo.Icono("panel", 24);
    private readonly Border _tarjeta;

    public static readonly BindableProperty ValorProperty =
        BindableProperty.Create(nameof(Valor), typeof(object), typeof(MetricaVisual),
            propertyChanged: (vista, _, valor) => ((MetricaVisual)vista)._valor.Text = valor?.ToString() ?? "0");
    public static readonly BindableProperty EtiquetaProperty =
        BindableProperty.Create(nameof(Etiqueta), typeof(string), typeof(MetricaVisual),
            propertyChanged: (vista, _, valor) => ((MetricaVisual)vista)._titulo.Text = (string?)valor);
    public static readonly BindableProperty IconoProperty =
        BindableProperty.Create(nameof(Icono), typeof(string), typeof(MetricaVisual),
            propertyChanged: (vista, _, valor) => ((MetricaVisual)vista)._icono.Source = $"ui_{valor}.png");
    public static readonly BindableProperty TonoProperty =
        BindableProperty.Create(nameof(Tono), typeof(string), typeof(MetricaVisual), "verde",
            propertyChanged: (vista, _, valor) => ((MetricaVisual)vista).AplicarTono((string)valor));

    public object? Valor { get => GetValue(ValorProperty); set => SetValue(ValorProperty, value); }
    public string Etiqueta { get => (string)GetValue(EtiquetaProperty); set => SetValue(EtiquetaProperty, value); }
    public string Icono { get => (string)GetValue(IconoProperty); set => SetValue(IconoProperty, value); }
    public string Tono { get => (string)GetValue(TonoProperty); set => SetValue(TonoProperty, value); }

    public MetricaVisual()
    {
        _valor.FontSize = 24;
        _titulo.FontSize = 13;
        _tarjeta = TarjetasDemo.Tarjeta(_icono, _valor, _titulo);
        _tarjeta.Padding = 10;
        _tarjeta.StrokeThickness = 0;
        Content = _tarjeta;
        AplicarTono("verde");
    }

    private void AplicarTono(string tono)
    {
        TarjetasDemo.AplicarTono(_valor, tono);
        var (claro, oscuro) = tono switch
        {
            "azul" => ("#E8EEF9", "#202D46"),
            "ambar" => ("#F8EFE3", "#3F3021"),
            "violeta" => ("#F0EAF9", "#332947"),
            "gris" => ("#EEF0F3", "#29323A"),
            _ => ("#E7F1EB", "#20382C")
        };
        _tarjeta.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb(claro), Color.FromArgb(oscuro));
    }
}
